using Microsoft.EntityFrameworkCore;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;

namespace YouthGroups.Data.Repositories;

public class MeetingRepository : Repository<Meeting>, IMeetingRepository
{
    public MeetingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<MeetingListItemDto>> GetPagedAsync(MeetingQueryDto q, CancellationToken ct)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (q.UserId.HasValue)
            query = query.Where(m => m.UserId == q.UserId.Value);
        if (q.Type.HasValue)
            query = query.Where(m => m.Type == q.Type.Value);
        if (q.From.HasValue)
        {
            var from = AsUtc(q.From.Value);
            query = query.Where(m => m.Date >= from);
        }
        if (q.To.HasValue)
        {
            var to = AsUtc(q.To.Value);
            query = query.Where(m => m.Date <= to);
        }

        var totalCount = await query.CountAsync(ct);

        query = q.Sort == "date_asc"
            ? query.OrderBy(m => m.Date).ThenBy(m => m.Id)
            : query.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id);

        // Paged and projected in the database: the reporter's name, the attendance count
        // and the expense total are computed in the same SQL query
        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(m => new MeetingListItemDto
            {
                Id = m.Id,
                UserId = m.UserId,
                ReporterName = m.User.FullName,
                Date = m.Date,
                Type = m.Type,
                Program = m.Program,
                ParticipantCount = m.Participants.Count(),
                TotalExpenses = m.Expenses.Sum(e => (decimal?)e.Amount) ?? 0
            })
            .ToListAsync(ct);

        return new PagedResult<MeetingListItemDto>
        {
            Items = items,
            Page = q.Page,
            PageSize = q.PageSize,
            TotalCount = totalCount
        };
    }

    public Task<Meeting?> GetWithDetailsAsync(int id, CancellationToken ct)
        => Set.AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Expenses)
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Meeting?> GetWithDetailsForUpdateAsync(int id, CancellationToken ct)
        => Set
            .Include(m => m.Expenses)
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    // SELECT "BudgetSource", SUM("Amount") ... GROUP BY "BudgetSource"
    public async Task<IReadOnlyDictionary<BudgetSource, decimal>> GetSpentBySourceAsync(
        int userId, int? excludeMeetingId, CancellationToken ct)
        => await Context.Expenses.AsNoTracking()
            .Where(e => e.Meeting.UserId == userId
                        && (excludeMeetingId == null || e.MeetingId != excludeMeetingId))
            .GroupBy(e => e.BudgetSource)
            .Select(g => new { Source = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.Source, x => x.Total, ct);

    // Npgsql only accepts UTC DateTimes for timestamptz columns. A date from the query
    // string ("2026-09-01") arrives with Kind=Unspecified, so treat it as UTC.
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
