using AutoMapper;
using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Repositories;
using BudgetFlow.Core.Interfaces.Services;
using BudgetFlow.Service.Budget;

namespace BudgetFlow.Service.Services;

// The coordinator's guide panel: puts together what the other services already compute,
// so the client needs one request instead of four. No rules of its own - the pots come
// from BudgetPots and the meetings from the same paged query as GET /api/meetings.
public class GuideOverviewService : IGuideOverviewService
{
    private const int RecentMeetingsCount = 10;

    private readonly IUserRepository _users;
    private readonly IParticipantRepository _participants;
    private readonly IMeetingRepository _meetings;
    private readonly BudgetPots _pots;
    private readonly IMapper _mapper;

    public GuideOverviewService(
        IUserRepository users,
        IParticipantRepository participants,
        IMeetingRepository meetings,
        BudgetPots pots,
        IMapper mapper)
    {
        _users = users;
        _participants = participants;
        _meetings = meetings;
        _pots = pots;
        _mapper = mapper;
    }

    public async Task<Result<GuideOverviewDto>> GetAsync(int guideId, CurrentUser caller, CancellationToken ct)
    {
        // Checked before the lookup, so a guide cannot learn which ids exist
        if (!caller.IsCoordinator && caller.UserId != guideId)
            return Result<GuideOverviewDto>.Forbidden("A guide may only view her own overview.");

        var guide = await _users.GetByIdAsync(guideId, ct);
        if (guide is not { Role: UserRole.Guide })
            return Result<GuideOverviewDto>.NotFound("Guide not found.");

        var participants = await _participants.GetByGuideAsync(guideId, ct);

        var recentMeetings = await _meetings.GetPagedAsync(new MeetingQueryDto
        {
            UserId = guideId,
            Sort = "date_desc",
            Page = 1,
            PageSize = RecentMeetingsCount
        }, ct);

        return Result<GuideOverviewDto>.Ok(new GuideOverviewDto
        {
            Guide = _mapper.Map<UserDto>(guide),
            Participants = _mapper.Map<List<ParticipantDto>>(participants),
            Pots = new List<BudgetPotDto>
            {
                await _pots.GetAsync(BudgetSource.Activity, guideId, null, ct),
                await _pots.GetAsync(BudgetSource.Birthday, guideId, null, ct)
            },
            RecentMeetings = recentMeetings
        });
    }
}
