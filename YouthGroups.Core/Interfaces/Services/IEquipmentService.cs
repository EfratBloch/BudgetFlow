using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Enums;

namespace YouthGroups.Core.Interfaces.Services;

public interface IEquipmentService
{
    Task<Result<IReadOnlyList<EquipmentDto>>> GetAllAsync(EquipmentStatus? status, CancellationToken ct);
    Task<Result<EquipmentDto>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<EquipmentDto>> CreateAsync(EquipmentCreateDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int id, EquipmentUpdateDto dto, CancellationToken ct);

    // Conflict while the item is taken
    Task<Result> DeleteAsync(int id, CancellationToken ct);

    // The limited resource. Conflict when the item is not available, or when another
    // guide took it between our read and our save (concurrency token mismatch).
    Task<Result> TakeAsync(int equipmentId, CurrentUser caller, CancellationToken ct);

    // The holder or the coordinator only
    Task<Result> ReturnAsync(int equipmentId, CurrentUser caller, CancellationToken ct);
}
