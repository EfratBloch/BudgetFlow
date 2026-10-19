using FluentAssertions;
using Moq;
using BudgetFlow.Core.Common;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Repositories;
using BudgetFlow.Service.Budget;
using BudgetFlow.Service.Services;

namespace BudgetFlow.Tests.Services;

// The budget formulas as GET /api/users/{id}/budget computes them, with the repositories mocked.
// Nothing is spent and nothing is added, so every pot shows its bare formula.
public class BudgetServiceTests
{
    private const int CoordinatorId = 1;
    private const int GuideId = 2;

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IParticipantRepository> _participants = new();
    private readonly Mock<IMeetingRepository> _meetings = new();
    private readonly Mock<IBudgetAdditionRepository> _additions = new();
    private readonly BudgetService _service;

    public BudgetServiceTests()
    {
        _meetings.Setup(m => m.GetSpentBySourceAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<BudgetSource, decimal>());

        var calculator = TestBudget.Calculator;
        var pots = new BudgetPots(_users.Object, _participants.Object, _meetings.Object, _additions.Object, calculator);
        _service = new BudgetService(_users.Object, _meetings.Object, _additions.Object, calculator, pots);
    }

    [Fact]
    public async Task GuideBudget_With12Participants_IsActivity300AndBirthday48()
    {
        _users.Setup(u => u.GetByIdAsync(GuideId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = GuideId, FullName = "Rachel", Role = UserRole.Guide });
        _participants.Setup(p => p.CountByGuideAsync(GuideId, It.IsAny<CancellationToken>())).ReturnsAsync(12);

        var result = await _service.GetUserBudgetAsync(GuideId, new CurrentUser(GuideId, UserRole.Guide), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        var pots = result.Value.Pots.ToDictionary(p => p.Source, p => p.Budget);
        pots.Should().HaveCount(2);
        pots[BudgetSource.Activity].Should().Be(300m);
        pots[BudgetSource.Birthday].Should().Be(48m); // 4 x 12
    }

    [Fact]
    public async Task CoordinatorBudget_With5GuidesAnd60Participants_IsGuideMeetings400AndAnnual2700()
    {
        _users.Setup(u => u.GetByIdAsync(CoordinatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = CoordinatorId, FullName = "Sarah", Role = UserRole.Coordinator });
        _users.Setup(u => u.CountGuidesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);
        _participants.Setup(p => p.CountAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(60);

        var result = await _service.GetUserBudgetAsync(CoordinatorId,
            new CurrentUser(CoordinatorId, UserRole.Coordinator), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        var pots = result.Value.Pots.ToDictionary(p => p.Source, p => p.Budget);
        pots.Should().HaveCount(2);
        pots[BudgetSource.GuideMeetings].Should().Be(400m); // 20 x 5 guides x 4 meetings
        pots[BudgetSource.Annual].Should().Be(2700m);       // 35 x 60 + 2 x 5 x 60
    }
}
