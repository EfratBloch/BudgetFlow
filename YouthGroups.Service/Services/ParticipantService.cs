using AutoMapper;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.Service.Services;

// A guide may only touch her own participants; the coordinator may touch anyone's.
public class ParticipantService : IParticipantService
{
    private const string NotYours = "A guide may only manage her own participants.";
    private const string ParticipantNotFound = "Participant not found.";
    private const string DuplicateNationalId = "A participant with this national ID already exists.";

    private readonly IParticipantRepository _participants;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ParticipantService(
        IParticipantRepository participants,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _participants = participants;
        _users = users;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<ParticipantDto>>> GetByGuideAsync(
        int guideId, CurrentUser caller, CancellationToken ct)
    {
        if (!CanAccess(guideId, caller))
            return Result<IReadOnlyList<ParticipantDto>>.Forbidden(NotYours);

        if (!await IsGuideAsync(guideId, ct))
            return Result<IReadOnlyList<ParticipantDto>>.NotFound("Guide not found.");

        var participants = await _participants.GetByGuideAsync(guideId, ct);
        return Result<IReadOnlyList<ParticipantDto>>.Ok(_mapper.Map<List<ParticipantDto>>(participants));
    }

    public async Task<Result<ParticipantDto>> GetByIdAsync(int id, CurrentUser caller, CancellationToken ct)
    {
        var participant = await _participants.GetByIdAsync(id, ct);
        if (participant is null)
            return Result<ParticipantDto>.NotFound(ParticipantNotFound);

        if (!CanAccess(participant.GuideId, caller))
            return Result<ParticipantDto>.Forbidden(NotYours);

        return Result<ParticipantDto>.Ok(_mapper.Map<ParticipantDto>(participant));
    }

    public async Task<Result<ParticipantDto>> CreateAsync(
        int guideId, ParticipantCreateDto dto, CurrentUser caller, CancellationToken ct)
    {
        // A guide adds only to her own group; the coordinator may add to any guide's group
        if (!CanAccess(guideId, caller))
            return Result<ParticipantDto>.Forbidden(NotYours);

        // Also stops the coordinator from adding participants to her own account
        if (!await IsGuideAsync(guideId, ct))
            return Result<ParticipantDto>.NotFound("Guide not found.");

        if (await _participants.NationalIdExistsAsync(dto.NationalId, null, ct))
            return Result<ParticipantDto>.Conflict(DuplicateNationalId);

        var participant = new Participant
        {
            FullName = dto.FullName.Trim(),
            NationalId = dto.NationalId,
            Phone = dto.Phone.Trim(),
            GuideId = guideId
        };

        await _participants.AddAsync(participant, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ParticipantDto>.Ok(_mapper.Map<ParticipantDto>(participant));
    }

    public async Task<Result> UpdateAsync(int id, ParticipantUpdateDto dto, CurrentUser caller, CancellationToken ct)
    {
        // Tracked, so changing the properties is enough
        var participant = await _participants.GetForUpdateAsync(id, ct);
        if (participant is null)
            return Result.NotFound(ParticipantNotFound);

        if (!CanAccess(participant.GuideId, caller))
            return Result.Forbidden(NotYours);

        if (await _participants.NationalIdExistsAsync(dto.NationalId, id, ct))
            return Result.Conflict(DuplicateNationalId);

        // GuideId is not in the DTO - a participant is never moved to another guide here
        participant.FullName = dto.FullName.Trim();
        participant.NationalId = dto.NationalId;
        participant.Phone = dto.Phone.Trim();

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CurrentUser caller, CancellationToken ct)
    {
        var participant = await _participants.GetForUpdateAsync(id, ct);
        if (participant is null)
            return Result.NotFound(ParticipantNotFound);

        if (!CanAccess(participant.GuideId, caller))
            return Result.Forbidden(NotYours);

        // Her attendance rows go with her (cascade); the meetings and their expenses stay
        _participants.Remove(participant);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static bool CanAccess(int guideId, CurrentUser caller)
        => caller.IsCoordinator || caller.UserId == guideId;

    private async Task<bool> IsGuideAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        return user is { Role: UserRole.Guide };
    }
}
