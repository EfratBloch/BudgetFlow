using AutoMapper;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;
using YouthGroups.Service.Budget;

namespace YouthGroups.Service.Services;

// The coordinator adds money to one pot of one guide. Each time she picks:
//   - the guide pot that receives it: Activity or Birthday
//   - where it comes from: Extra (new money) or Transfer from her GuideMeetings / Annual pot
// Nothing is stored as a balance: the additions are rows, and BudgetPots adds them to the
// guide pot (and subtracts transfers from the coordinator pot) on every read.
// This feature is an addition to the PLAN, requested by the coordinator.
public class BudgetAdditionService : IBudgetAdditionService
{
    private readonly IBudgetAdditionRepository _additions;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BudgetPots _pots;
    private readonly IMapper _mapper;

    public BudgetAdditionService(
        IBudgetAdditionRepository additions,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        BudgetPots pots,
        IMapper mapper)
    {
        _additions = additions;
        _users = users;
        _unitOfWork = unitOfWork;
        _pots = pots;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<BudgetAdditionDto>>> GetAllAsync(int? guideId, CancellationToken ct)
    {
        var additions = await _additions.GetAllAsync(guideId, ct);
        return Result<IReadOnlyList<BudgetAdditionDto>>.Ok(_mapper.Map<List<BudgetAdditionDto>>(additions));
    }

    public async Task<Result<BudgetAdditionDto>> CreateAsync(int guideId, BudgetAdditionCreateDto dto, CancellationToken ct)
    {
        var targetPot = dto.TargetPot!.Value; // [Required] on the DTO
        var kind = dto.Kind!.Value;

        if (targetPot is not (BudgetSource.Activity or BudgetSource.Birthday))
            return Result<BudgetAdditionDto>.Validation("Money can be added only to a guide's activity or birthday budget.");

        if (kind == BudgetAdditionKind.Transfer
            && dto.FromPot is not (BudgetSource.GuideMeetings or BudgetSource.Annual))
            return Result<BudgetAdditionDto>.Validation("A transfer must come from the guide meetings or the annual budget.");

        if (kind == BudgetAdditionKind.Extra && dto.FromPot is not null)
            return Result<BudgetAdditionDto>.Validation("Extra money does not come from another budget - leave FromPot empty.");

        var guide = await _users.GetByIdAsync(guideId, ct);
        if (guide is not { Role: UserRole.Guide })
            return Result<BudgetAdditionDto>.NotFound("Guide not found.");

        // The remaining amount is read and the addition is written in the same transaction
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        if (kind == BudgetAdditionKind.Transfer)
        {
            var coordinator = await _users.GetCoordinatorAsync(ct);
            if (coordinator is null)
                return Result<BudgetAdditionDto>.NotFound("No coordinator is registered yet.");

            var fromPot = await _pots.GetAsync(dto.FromPot!.Value, coordinator.Id, null, ct);
            var available = Math.Max(fromPot.Remaining, 0m);

            if (dto.Amount > available)
                return Result<BudgetAdditionDto>.Conflict(
                    $"There is not enough left in the {fromPot.Label.ToLowerInvariant()} budget for this transfer.",
                    new Dictionary<string, object> { ["remaining"] = available });
        }

        var addition = new BudgetAddition
        {
            GuideId = guideId,
            TargetPot = targetPot,
            Amount = dto.Amount,
            Reason = dto.Reason.Trim(),
            Kind = kind,
            FromPot = dto.FromPot,
            CreatedAt = DateTime.UtcNow
        };

        await _additions.AddAsync(addition, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var result = _mapper.Map<BudgetAdditionDto>(addition);
        result.GuideName = guide.FullName; // Guide is not loaded on the new entity
        return Result<BudgetAdditionDto>.Ok(result);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        var addition = await _additions.GetForUpdateAsync(id, ct);
        if (addition is null)
            return Result.NotFound("Budget addition not found.");

        // Cancelling takes the money out of the guide's pot again - not allowed if she already spent it
        var pot = await _pots.GetAsync(addition.TargetPot, addition.GuideId, null, ct);
        if (pot.Remaining - addition.Amount < 0)
            return Result.Conflict(
                "The guide already spent part of this money, so the addition cannot be cancelled.",
                new Dictionary<string, object> { ["remaining"] = pot.Remaining });

        // A transfer's money goes back to the coordinator's pot by itself:
        // that pot's budget is computed from the transfers that still exist
        _additions.Remove(addition);
        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }
}
