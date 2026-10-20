using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Repositories;
using BudgetFlow.Service.Budget;
using BudgetFlow.Service.Services;

namespace BudgetFlow.Tests.Services;

// An Activity meeting over the activity pot, with the repositories mocked (no database).
// The guide has 12 participants and already spent 250 on activities:
//   activity pot 300 - 250 = 50 left, birthday pot 4 x 12 = 48 left.
// The meeting costs 80, so the shortfall is 30, which the birthday pot can cover.
public class MeetingServiceTests
{
    private const int GuideId = 2;
    private const int ParticipantId = 10;
    private const int NewMeetingId = 99;

    private readonly Mock<IMeetingRepository> _meetings = new();
    private readonly Mock<IParticipantRepository> _participants = new();
    private readonly Mock<IEquipmentRepository> _equipment = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITransaction> _transaction = new();
    private readonly MeetingService _service;
    private readonly CurrentUser _guide = new(GuideId, UserRole.Guide);

    private Meeting? _added;

    public MeetingServiceTests()
    {
        _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transaction.Object);

        _participants.Setup(p => p.GetByIdsForUpdateAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Participant> { new() { Id = ParticipantId, GuideId = GuideId } });
        _participants.Setup(p => p.CountByGuideAsync(GuideId, It.IsAny<CancellationToken>())).ReturnsAsync(12);

        _meetings.Setup(m => m.GetSpentBySourceAsync(GuideId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<BudgetSource, decimal> { [BudgetSource.Activity] = 250m });

        // Keep the meeting the service adds, so the test can look at its expense rows
        _meetings.Setup(m => m.AddAsync(It.IsAny<Meeting>(), It.IsAny<CancellationToken>()))
            .Callback<Meeting, CancellationToken>((meeting, _) => { meeting.Id = NewMeetingId; _added = meeting; })
            .Returns(Task.CompletedTask);
        _meetings.Setup(m => m.GetWithDetailsAsync(NewMeetingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _added);

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<MeetingDto>(It.IsAny<object>())).Returns(new MeetingDto());

        var additions = new Mock<IBudgetAdditionRepository>(); // no additions: every sum is 0
        var pots = new BudgetPots(Mock.Of<IUserRepository>(), _participants.Object, _meetings.Object,
            additions.Object, TestBudget.Calculator);

        _service = new MeetingService(_meetings.Object, _participants.Object, _equipment.Object, Mock.Of<IUserRepository>(), _unitOfWork.Object, pots,
            mapper.Object, NullLogger<MeetingService>.Instance);
    }

    private static MeetingCreateDto ActivityMeeting(bool allowBirthdayDraw) => new()
    {
        Type = MeetingType.Activity,
        Date = DateTime.UtcNow.AddDays(-1),
        Program = "Treasure hunt",
        ParticipantIds = new List<int> { ParticipantId },
        Expenses = new List<ExpenseCreateDto> { new() { Amount = 80m, Description = "Prizes" } },
        AllowBirthdayDraw = allowBirthdayDraw
    };

    [Fact]
    public async Task Create_OverActivityBudget_WithoutBirthdayDraw_ReturnsConflictWithShortfall()
    {
        var result = await _service.CreateAsync(ActivityMeeting(allowBirthdayDraw: false), _guide, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Details.Should().NotBeNull();
        result.Details!["shortfall"].Should().Be(30m);
        result.Details["birthdayAvailable"].Should().Be(48m);

        _meetings.Verify(m => m.AddAsync(It.IsAny<Meeting>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_OverActivityBudget_WithBirthdayDraw_SplitsBetweenActivityAndBirthday()
    {
        var result = await _service.CreateAsync(ActivityMeeting(allowBirthdayDraw: true), _guide, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Deliberate change from the plan: the rest of the activity pot first, only the shortfall to Birthday
        _added.Should().NotBeNull();
        _added!.Expenses.Should().HaveCount(2);
        _added.Expenses.Should().ContainSingle(e => e.BudgetSource == BudgetSource.Activity)
            .Which.Amount.Should().Be(50m);
        _added.Expenses.Should().ContainSingle(e => e.BudgetSource == BudgetSource.Birthday)
            .Which.Amount.Should().Be(30m);
        _added.Expenses.Sum(e => e.Amount).Should().Be(80m);
    }

    [Fact]
    public async Task Create_ActivityWithStoreItem_RecordsTheItemWithoutTakingIt()
    {
        const int ProjectorId = 5;
        _equipment.Setup(e => e.GetByIdAsync(ProjectorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Equipment { Id = ProjectorId, Name = "Projector", Status = EquipmentStatus.Taken });

        var dto = ActivityMeeting(allowBirthdayDraw: true);
        dto.EquipmentId = ProjectorId;

        var result = await _service.CreateAsync(dto, _guide, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        _added!.EquipmentId.Should().Be(ProjectorId);
        // Only read - the item is never loaded for update, so its status and holder stay as they are
        _equipment.Verify(e => e.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_BirthdayWithStoreItem_ReturnsValidation()
    {
        var dto = new MeetingCreateDto
        {
            Type = MeetingType.Birthday,
            Date = DateTime.UtcNow.AddDays(-1),
            Program = "Birthday",
            ParticipantIds = new List<int> { ParticipantId },
            EquipmentId = 5
        };

        var result = await _service.CreateAsync(dto, _guide, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Validation);
        _meetings.Verify(m => m.AddAsync(It.IsAny<Meeting>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_GuidesMeetingWithParticipants_ReturnsValidation()
    {
        var coordinator = new CurrentUser(1, UserRole.Coordinator);
        var dto = new MeetingCreateDto
        {
            Type = MeetingType.GuidesMeeting,
            Date = DateTime.UtcNow.AddDays(-1),
            Program = "Monthly guides meeting",
            ParticipantIds = new List<int> { ParticipantId }
        };

        var result = await _service.CreateAsync(dto, coordinator, CancellationToken.None);

        // A guides meeting is attended by guides, never by participants
        result.Status.Should().Be(ResultStatus.Validation);
        _meetings.Verify(m => m.AddAsync(It.IsAny<Meeting>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
