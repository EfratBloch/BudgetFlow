using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.Interfaces.Repositories;

public interface IEquipmentRepository : IRepository<Equipment>
{
    // Read only, with the holder loaded; status null = all items
    Task<IReadOnlyList<Equipment>> GetAllAsync(EquipmentStatus? status, CancellationToken ct);

    // Taking and returning an item use GetForUpdateAsync (tracked) from IRepository.
    // A tracked entity remembers the xmin value it was read with, and EF Core puts
    // that value in the WHERE clause of the UPDATE.
}
