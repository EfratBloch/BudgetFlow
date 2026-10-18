using Microsoft.EntityFrameworkCore;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Interfaces.Repositories;

namespace YouthGroups.Data.Repositories;

public class ParticipantRepository : Repository<Participant>, IParticipantRepository
{
    public ParticipantRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Participant>> GetByGuideAsync(int guideId, CancellationToken ct)
        => await Set.AsNoTracking()
            .Where(p => p.GuideId == guideId)
            .OrderBy(p => p.FullName)
            .ToListAsync(ct);

    public Task<int> CountByGuideAsync(int guideId, CancellationToken ct)
        => Set.CountAsync(p => p.GuideId == guideId, ct);

    public Task<int> CountAllAsync(CancellationToken ct)
        => Set.CountAsync(ct);

    public Task<bool> NationalIdExistsAsync(string nationalId, int? excludeParticipantId, CancellationToken ct)
        => Set.AnyAsync(p => p.NationalId == nationalId
                             && (excludeParticipantId == null || p.Id != excludeParticipantId), ct);

    // Tracked, because the result is attached to a meeting's attendance
    public async Task<IReadOnlyList<Participant>> GetByIdsForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
        => await Set.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
}
