using AutoMapper;
using Microsoft.Extensions.Logging;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.Service.Services;

// The equipment store - the limited resource guides compete for.
//
// How two guides are stopped from taking the same item (optimistic concurrency):
//   Equipment.Version is mapped to PostgreSQL's xmin, which changes on every write to the row.
//   Take and Return load the item TRACKED, so EF remembers the xmin it was read with and
//   puts it in the WHERE clause:  UPDATE ... WHERE "Id" = @id AND xmin = @versionWeRead.
//   If another guide saved first, xmin changed, zero rows match, and the save fails with a
//   concurrency exception - which becomes a Conflict instead of a second "successful" take.
// Three things the mechanism depends on:
//   a. GetForUpdateAsync (tracked). An AsNoTracking load would not carry the original xmin.
//   b. The status check and the save run in the same transaction.
//   c. Only the concurrency exception is caught - never a bare Exception, which would also
//      hide real errors such as a lost database connection.
// The Data layer wraps EF Core's DbUpdateConcurrencyException in Core's
// ConcurrencyConflictException, so this layer catches it without referencing EF Core.
public class EquipmentService : IEquipmentService
{
    private const string ItemNotFound = "Item not found.";
    private const string ChangedMeanwhile = "This item was changed by someone else. Please reload and try again.";

    private readonly IEquipmentRepository _equipment;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<EquipmentService> _logger;

    public EquipmentService(IEquipmentRepository equipment, IUnitOfWork unitOfWork, IMapper mapper,
        ILogger<EquipmentService> logger)
    {
        _equipment = equipment;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<EquipmentDto>>> GetAllAsync(EquipmentStatus? status, CancellationToken ct)
    {
        var items = await _equipment.GetAllAsync(status, ct);
        return Result<IReadOnlyList<EquipmentDto>>.Ok(_mapper.Map<List<EquipmentDto>>(items));
    }

    public async Task<Result<EquipmentDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var item = await _equipment.GetByIdAsync(id, ct);
        if (item is null)
            return Result<EquipmentDto>.NotFound(ItemNotFound);

        return Result<EquipmentDto>.Ok(_mapper.Map<EquipmentDto>(item));
    }

    public async Task<Result<EquipmentDto>> CreateAsync(EquipmentCreateDto dto, CancellationToken ct)
    {
        var item = new Equipment
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Status = EquipmentStatus.Available // a new item always starts in the store
        };

        await _equipment.AddAsync(item, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<EquipmentDto>.Ok(_mapper.Map<EquipmentDto>(item));
    }

    public async Task<Result> UpdateAsync(int id, EquipmentUpdateDto dto, CancellationToken ct)
    {
        var item = await _equipment.GetForUpdateAsync(id, ct);
        if (item is null)
            return Result.NotFound(ItemNotFound);

        if (item.Status == EquipmentStatus.Taken)
        {
            // A taken item keeps its status; it goes to maintenance only after it is returned
            if (dto.InMaintenance)
                return Conflict("Update", id, "This item is on loan. It can be sent to maintenance after it is returned.");
        }
        else
        {
            item.Status = dto.InMaintenance ? EquipmentStatus.Maintenance : EquipmentStatus.Available;
        }

        item.Name = dto.Name.Trim();
        item.Description = dto.Description?.Trim();

        // The same token protects this edit from racing a take or a return
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict("Update", id, ChangedMeanwhile);
        }

        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var item = await _equipment.GetForUpdateAsync(id, ct);
        if (item is null)
            return Result.NotFound(ItemNotFound);

        if (item.Status == EquipmentStatus.Taken)
            return Conflict("Delete", id, "This item is on loan and cannot be deleted until it is returned.");

        _equipment.Remove(item);

        // A guide may have taken it between our read and the DELETE
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict("Delete", id, ChangedMeanwhile);
        }

        return Result.Ok();
    }

    public async Task<Result> TakeAsync(int equipmentId, CurrentUser caller, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        // TRACKED - do not use AsNoTracking here (rule a)
        var item = await _equipment.GetForUpdateAsync(equipmentId, ct);
        if (item is null)
            return Result.NotFound(ItemNotFound);

        if (item.Status != EquipmentStatus.Available)
            return Conflict("Take", equipmentId, "This item is already on loan");

        item.Status = EquipmentStatus.Taken;
        item.HolderUserId = caller.UserId;
        item.TakenAt = DateTime.UtcNow;

        // Same transaction as the check above (rule b); only the concurrency exception (rule c)
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict("Take", equipmentId, "Another guide just took this item. Please try again.");
        }

        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ReturnAsync(int equipmentId, CurrentUser caller, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        var item = await _equipment.GetForUpdateAsync(equipmentId, ct);
        if (item is null)
            return Result.NotFound(ItemNotFound);

        if (item.Status != EquipmentStatus.Taken)
            return Result.Validation("This item is not on loan.");

        if (item.HolderUserId != caller.UserId && !caller.IsCoordinator)
            return Result.Forbidden("Only the guide holding this item or the coordinator may return it.");

        item.Status = EquipmentStatus.Available;
        item.HolderUserId = null;
        item.TakenAt = null;

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict("Return", equipmentId, "This item was just returned or changed by someone else. Please reload.");
        }

        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    // Every conflict over an item is a Warning in the log: handled (the caller got a 409), but worth seeing
    private Result Conflict(string action, int equipmentId, string message)
    {
        _logger.LogWarning("Equipment conflict on {Action} of item {EquipmentId}: {Reason}", action, equipmentId, message);
        return Result.Conflict(message);
    }
}
