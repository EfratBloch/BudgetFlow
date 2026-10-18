using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;

namespace YouthGroups.Core.Interfaces.Services;

// A guide may only touch her own participants; the coordinator may touch anyone's.
public interface IParticipantService
{
    Task<Result<IReadOnlyList<ParticipantDto>>> GetByGuideAsync(int guideId, CurrentUser caller, CancellationToken ct);
    Task<Result<ParticipantDto>> GetByIdAsync(int id, CurrentUser caller, CancellationToken ct);
    Task<Result<ParticipantDto>> CreateAsync(int guideId, ParticipantCreateDto dto, CurrentUser caller, CancellationToken ct);
    Task<Result> UpdateAsync(int id, ParticipantUpdateDto dto, CurrentUser caller, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CurrentUser caller, CancellationToken ct);
}
