// ═══════════════════════════════════════════════════════════════════════════
// MeterManagement.Tests/Unit/MeterServiceTests.cs
// ═══════════════════════════════════════════════════════════════════════════
// Unit tests for MeterService business logic.
// All dependencies are mocked — no database, no cache, no HTTP needed.
// Fast: runs in milliseconds. Isolated: each test owns its mocks.
// ═══════════════════════════════════════════════════════════════════════════

using FluentAssertions;
using MeterManagementService.DTOs;
using MeterManagementService.Models;
using MeterManagementService.Repositories;
using MeterManagementService.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MeterManagement.Tests.Unit;

public class MeterServiceTests
{
    private readonly Mock<IMeterRepository> _mockRepo;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ILogger<MeterService>> _mockLog;
    private readonly IMeterService _svc;

    public MeterServiceTests()
    {
        _mockRepo  = new Mock<IMeterRepository>();
        _mockCache = new Mock<IDistributedCache>();
        _mockLog   = new Mock<ILogger<MeterService>>();
        _svc       = new MeterService(_mockRepo.Object, _mockCache.Object, _mockLog.Object);
    }

    // ── Helper: create a realistic AURORA meter ───────────────────────────
    private static Meter MakeAurora(string serial = "AU240001") => new()
    {
        Id            = Guid.NewGuid(),
        SerialNumber  = serial,
        Model         = "AURORA",
        MeterType     = MeterType.SinglePhaseSmart,
        IsSmart       = true,
        IsPrepaid     = false,
        AccuracyClass = "Class 1.0",
        VoltageRating = "240V (P-N)",
        CurrentRating = "5-30A",
        Standards     = "IS 16444 Part 1, IS 15959",
        CommunicationType = CommunicationType.RFMesh,
        Status        = MeterStatus.Commissioned,
        RelayStatus   = RelayStatus.Connected,
        OnboardedAt   = DateTime.UtcNow.AddDays(-30)
    };

    // ─────────────────────────────────────────────────────────────────────
    // GetAllAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsCachedDtos_WhenCacheHit()
    {
        // Arrange: serialise 2 DTOs into the mock cache
        var cached = new List<MeterResponseDto>
        {
            new() { Id = Guid.NewGuid(), SerialNumber = "AU240001", Model = "AURORA",
                    MeterType = "SinglePhaseSmart", Status = "Commissioned",
                    RelayStatus = "Connected", OnboardedAt = DateTime.UtcNow }
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached));
        _mockCache.Setup(c => c.GetAsync("meters:all", default)).ReturnsAsync(bytes);

        // Act
        var result = await _svc.GetAllAsync(null, null, null, null, null);

        // Assert: cache returned, repository never called
        result.Should().HaveCount(1);
        result.First().SerialNumber.Should().Be("AU240001");
        _mockRepo.Verify(r => r.GetAllAsync(null, null, null, null, null), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_QueriesRepoAndSetsCache_WhenCacheMiss()
    {
        // Arrange: cache miss
        _mockCache.Setup(c => c.GetAsync("meters:all", default)).ReturnsAsync((byte[]?)null);
        var meters = new List<Meter> { MakeAurora("AU240001"), MakeAurora("AU240002") };
        _mockRepo.Setup(r => r.GetAllAsync(null, null, null, null, null)).ReturnsAsync(meters);
        _mockCache.Setup(c => c.SetAsync("meters:all", It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), default)).Returns(Task.CompletedTask);

        // Act
        var result = await _svc.GetAllAsync(null, null, null, null, null);

        // Assert
        result.Should().HaveCount(2);
        _mockRepo.Verify(r => r.GetAllAsync(null, null, null, null, null), Times.Once);
        _mockCache.Verify(c => c.SetAsync("meters:all", It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────
    // GetByIdAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMeterNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Meter?)null);
        var result = await _svc.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMappedDto_WithCorrectFields()
    {
        var meter = MakeAurora("AU240005");
        meter.Zone = "MYSURU-NORTH";
        meter.DlmsLogicalDeviceName = "SAG0012345678";
        _mockRepo.Setup(r => r.GetByIdAsync(meter.Id)).ReturnsAsync(meter);

        var result = await _svc.GetByIdAsync(meter.Id);

        result.Should().NotBeNull();
        result!.SerialNumber.Should().Be("AU240005");
        result.Model.Should().Be("AURORA");
        result.MeterType.Should().Be("SinglePhaseSmart");
        result.AccuracyClass.Should().Be("Class 1.0");
        result.Zone.Should().Be("MYSURU-NORTH");
        result.DlmsLogicalDeviceName.Should().Be("SAG0012345678");
        result.IsSmart.Should().BeTrue();
        result.IsPrepaid.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────
    // CreateAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Throws_WhenSerialNumberDuplicate()
    {
        var existing = MakeAurora("AU240001");
        _mockRepo.Setup(r => r.GetBySerialAsync("AU240001")).ReturnsAsync(existing);

        var dto = new MeterCreateDto
        {
            SerialNumber  = "AU240001", Model = "AURORA",
            MeterType     = MeterType.SinglePhaseSmart,
            AccuracyClass = "Class 1.0", VoltageRating = "240V (P-N)",
            CurrentRating = "5-30A", Standards = "IS 16444 Part 1"
        };

        await _svc.Invoking(s => s.CreateAsync(dto))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AU240001*already registered*");

        _mockRepo.Verify(r => r.CreateAsync(It.IsAny<Meter>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_SucceedsAndInvalidatesCache()
    {
        _mockRepo.Setup(r => r.GetBySerialAsync("RG3P0001")).ReturnsAsync((Meter?)null);
        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<Meter>()))
                 .ReturnsAsync((Meter m) => m);
        _mockCache.Setup(c => c.RemoveAsync("meters:all", default)).Returns(Task.CompletedTask);

        var dto = new MeterCreateDto
        {
            SerialNumber  = "RG3P0001", Model = "REGOR",
            MeterType     = MeterType.ThreePhaseWholeCurrent,
            AccuracyClass = "Class 1.0", VoltageRating = "3×240V (P-N)",
            CurrentRating = "10-60A", Standards = "IS 16444 Part 1",
            Zone = "MYSURU-SOUTH", CommunicationType = CommunicationType.RFMesh
        };

        var result = await _svc.CreateAsync(dto);

        result.Should().NotBeNull();
        result.SerialNumber.Should().Be("RG3P0001");
        result.Model.Should().Be("REGOR");
        result.Zone.Should().Be("MYSURU-SOUTH");
        result.Status.Should().Be("Commissioned");
        _mockCache.Verify(c => c.RemoveAsync("meters:all", default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────
    // DeleteAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_AndNoCache_WhenNotFound()
    {
        _mockRepo.Setup(r => r.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);
        var result = await _svc.DeleteAsync(Guid.NewGuid());
        result.Should().BeFalse();
        _mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrueAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.DeleteAsync(id)).ReturnsAsync(true);
        _mockCache.Setup(c => c.RemoveAsync("meters:all", default)).Returns(Task.CompletedTask);

        var result = await _svc.DeleteAsync(id);

        result.Should().BeTrue();
        _mockCache.Verify(c => c.RemoveAsync("meters:all", default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────
    // SetRelayAsync — domain rule: only smart meters support relay
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetRelayAsync_Throws_WhenMeterIsNotSmart()
    {
        var nonSmart = MakeAurora("ER300001");
        nonSmart.IsSmart = false;
        nonSmart.Model   = "ER300P";
        _mockRepo.Setup(r => r.GetByIdAsync(nonSmart.Id)).ReturnsAsync(nonSmart);

        await _svc.Invoking(s => s.SetRelayAsync(nonSmart.Id,
            new RelayCommandDto { Action = "disconnect", Reason = "test" }, "admin"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ER300P*does not support remote relay*");
    }

    [Fact]
    public async Task SetRelayAsync_Throws_WhenActionIsInvalid()
    {
        var meter = MakeAurora("AU240010");
        _mockRepo.Setup(r => r.GetByIdAsync(meter.Id)).ReturnsAsync(meter);
        _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Meter>()))
                 .ReturnsAsync((Meter m) => m);
        _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask);

        await _svc.Invoking(s => s.SetRelayAsync(meter.Id,
            new RelayCommandDto { Action = "explode" }, "engineer"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*explode*");
    }

    [Fact]
    public async Task SetRelayAsync_Disconnects_SmartMeter_Successfully()
    {
        var meter = MakeAurora("AU240011");
        _mockRepo.Setup(r => r.GetByIdAsync(meter.Id)).ReturnsAsync(meter);
        _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Meter>()))
                 .ReturnsAsync((Meter m) => m);
        _mockCache.Setup(c => c.RemoveAsync("meters:all", default)).Returns(Task.CompletedTask);

        var result = await _svc.SetRelayAsync(meter.Id,
            new RelayCommandDto { Action = "disconnect", Reason = "Non-payment" }, "admin");

        result.Should().NotBeNull();
        result!.RelayStatus.Should().Be("Disconnected");
        _mockCache.Verify(c => c.RemoveAsync("meters:all", default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────
    // UpdateAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMeterNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Meter?)null);
        var result = await _svc.UpdateAsync(Guid.NewGuid(), new MeterUpdateDto { Zone = "X" });
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_AppliesOnlyNonNullFields()
    {
        var meter = MakeAurora("AU240020");
        meter.Zone = "MYSURU-NORTH";
        _mockRepo.Setup(r => r.GetByIdAsync(meter.Id)).ReturnsAsync(meter);
        _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Meter>()))
                 .ReturnsAsync((Meter m) => m);
        _mockCache.Setup(c => c.RemoveAsync("meters:all", default)).Returns(Task.CompletedTask);

        // Only update Zone — other fields should stay unchanged
        var result = await _svc.UpdateAsync(meter.Id, new MeterUpdateDto { Zone = "MYSURU-SOUTH" });

        result.Should().NotBeNull();
        result!.Zone.Should().Be("MYSURU-SOUTH");
        result.Model.Should().Be("AURORA"); // unchanged
        result.AccuracyClass.Should().Be("Class 1.0"); // unchanged
    }
}
