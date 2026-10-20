using BudgetFlow.Core.Entities;

namespace BudgetFlow.Core.Interfaces.Repositories;

public interface IParticipantRepository : IRepository<Participant>
{
    Task<IReadOnlyList<Participant>> GetByGuideAsync(int guideId, CancellationToken ct);

    Task<int> CountByGuideAsync(int guideId, CancellationToken ct);
    Task<int> CountAllAsync(CancellationToken ct);

    // Tracked - the result is attached to a meeting as its attendance
    Task<IReadOnlyList<Participant>> GetByIdsForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
}
