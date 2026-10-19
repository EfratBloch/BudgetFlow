using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;

namespace YouthGroups.Core.Interfaces.Services;

// One guide's details, participants, pots and recent meetings together - the coordinator's guide panel.
// The coordinator sees any guide; a guide only herself (403 otherwise).
public interface IGuideOverviewService
{
    Task<Result<GuideOverviewDto>> GetAsync(int guideId, CurrentUser caller, CancellationToken ct);
}
