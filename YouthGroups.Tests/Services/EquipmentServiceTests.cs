using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YouthGroups.Core.Common;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Service.Services;

namespace YouthGroups.Tests.Services;

// Taking and returning equipment, with the repository and the unit of work mocked (no database).
public class EquipmentServiceTests
{
    private const int GuideId = 2;
    private const int OtherGuideId = 3;

    private readonly Mock<IEquipmentRepository> _equipment = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITransaction> _transaction = new();
    private readonly EquipmentService _service;

    public EquipmentServiceTests()
    {
        _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transaction.Object);

        _service = new EquipmentService(_equipment.Object, _unitOfWork.Object, Mock.Of<IMapper>(),
            NullLogger<EquipmentService>.Instance);
    }

    [Fact]
    public async Task Take_AvailableItem_Succeeds()
    {
        var item = new Equipment { Id = 1, Name = "Projector 1", Status = EquipmentStatus.Available };
        _equipment.Setup(r => r.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _service.TakeAsync(1, new CurrentUser(GuideId, UserRole.Guide), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        item.Status.Should().Be(EquipmentStatus.Taken);
        item.HolderUserId.Should().Be(GuideId);
        item.TakenAt.Should().NotBeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Take_TakenItem_ReturnsConflict()
    {
        var item = new Equipment
        {
            Id = 1, Name = "Projector 1", Status = EquipmentStatus.Taken,
            HolderUserId = OtherGuideId, TakenAt = DateTime.UtcNow
        };
        _equipment.Setup(r => r.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _service.TakeAsync(1, new CurrentUser(GuideId, UserRole.Guide), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        item.HolderUserId.Should().Be(OtherGuideId); // still hers
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Return_ByGuideWhoIsNotTheHolder_ReturnsForbidden()
    {
        var item = new Equipment
        {
            Id = 1, Name = "Projector 1", Status = EquipmentStatus.Taken,
            HolderUserId = OtherGuideId, TakenAt = DateTime.UtcNow
        };
        _equipment.Setup(r => r.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _service.ReturnAsync(1, new CurrentUser(GuideId, UserRole.Guide), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        item.Status.Should().Be(EquipmentStatus.Taken);
        item.HolderUserId.Should().Be(OtherGuideId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
