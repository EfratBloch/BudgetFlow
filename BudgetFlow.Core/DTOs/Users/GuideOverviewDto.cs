using BudgetFlow.Core.Common;

namespace BudgetFlow.Core.DTOs;

// Response: GET /api/users/{id}/overview - everything the coordinator's guide panel shows, in one call
public class GuideOverviewDto
{
    public UserDto Guide { get; set; } = new();
    public List<ParticipantDto> Participants { get; set; } = new();

    // Both pots with Budget, Spent and Remaining
    public List<BudgetPotDto> Pots { get; set; } = new();

    // The first page of her meetings, newest first. Further pages: GET /api/meetings?userId={id}&page=2
    public PagedResult<MeetingListItemDto> RecentMeetings { get; set; } = new();
}
