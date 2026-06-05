// ═══════════════════════════════════════════════════════════════════════════
// MeterManagementService/Services/MeterService.cs
// ═══════════════════════════════════════════════════════════════════════════

using System.Text.Json;
using MeterManagementService.DTOs;
using MeterManagementService.Models;
using MeterManagementService.Repositories;
using Microsoft.Extensions.Caching.Distributed;

namespace MeterManagementService.Services;

public interface IMeterService
{
    Task<IEnumerable<MeterResponseDto>> GetAllAsync(
        MeterType? type, MeterStatus? status,
        CommunicationType? commType, string? zone, string? substationId);
    Task<MeterResponseDto?> GetByIdAsync(Guid id);
    Task<MeterResponseDto?> GetBySerialAsync(string serial);
    Task<MeterResponseDto>  CreateAsync(MeterCreateDto dto);
    Task<MeterResponseDto?> UpdateAsync(Guid id, MeterUpdateDto dto);
    Task<bool>              DeleteAsync(Guid id);
    Task<MeterResponseDto?> SetRelayAsync(Guid id, RelayCommandDto cmd, string operatorName);
}

public class MeterService : IMeterService
{
    private readonly IMeterRepository  _repo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MeterService> _log;

    private const string AllKey = "meters:all";
    private static readonly DistributedCacheEntryOptions CacheOpts =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };

    public MeterService(IMeterRepository repo, IDistributedCache cache, ILogger<MeterService> log)
    { _repo = repo; _cache = cache; _log = log; }

    public async Task<IEnumerable<MeterResponseDto>> GetAllAsync(
        MeterType? type, MeterStatus? status,
        CommunicationType? commType, string? zone, string? substationId)
    {
        bool isUnfiltered = type is null && status is null && commType is null
                         && zone is null && substationId is null;

        if (isUnfiltered)
        {
            var cached = await _cache.GetStringAsync(AllKey);
            if (cached is not null)
                return JsonSerializer.Deserialize<IEnumerable<MeterResponseDto>>(cached)
                       ?? Enumerable.Empty<MeterResponseDto>();
        }

        var meters = await _repo.GetAllAsync(type, status, commType, zone, substationId);
        var dtos   = meters.Select(Map).ToList();

        if (isUnfiltered)
            await _cache.SetStringAsync(AllKey, JsonSerializer.Serialize(dtos), CacheOpts);

        return dtos;
    }

    public async Task<MeterResponseDto?> GetByIdAsync(Guid id)
    { var m = await _repo.GetByIdAsync(id); return m is null ? null : Map(m); }

    public async Task<MeterResponseDto?> GetBySerialAsync(string serial)
    { var m = await _repo.GetBySerialAsync(serial); return m is null ? null : Map(m); }

    public async Task<MeterResponseDto> CreateAsync(MeterCreateDto dto)
    {
        if (await _repo.GetBySerialAsync(dto.SerialNumber) is not null)
            throw new InvalidOperationException(
                $"Meter with serial '{dto.SerialNumber}' is already registered.");

        var meter = new Meter
        {
            SerialNumber          = dto.SerialNumber,
            Model                 = dto.Model,
            MeterType             = dto.MeterType,
            IsSmart               = dto.IsSmart,
            IsPrepaid             = dto.IsPrepaid,
            AccuracyClass         = dto.AccuracyClass,
            VoltageRating         = dto.VoltageRating,
            CurrentRating         = dto.CurrentRating,
            Standards             = dto.Standards,
            CommunicationType     = dto.CommunicationType,
            DcuId                 = dto.DcuId,
            SimNumber             = dto.SimNumber,
            DlmsLogicalDeviceName = dto.DlmsLogicalDeviceName,
            ConsumerNumber        = dto.ConsumerNumber,
            InstallationAddress   = dto.InstallationAddress,
            Latitude              = dto.Latitude,
            Longitude             = dto.Longitude,
            Zone                  = dto.Zone,
            SubstationId          = dto.SubstationId,
            FirmwareVersion       = dto.FirmwareVersion,
            TodEnabled            = dto.TodEnabled,
            Notes                 = dto.Notes,
            Status                = MeterStatus.Commissioned
        };

        var created = await _repo.CreateAsync(meter);
        await _cache.RemoveAsync(AllKey);
        _log.LogInformation("Meter onboarded: {Serial} ({Model})", created.SerialNumber, created.Model);
        return Map(created);
    }

    public async Task<MeterResponseDto?> UpdateAsync(Guid id, MeterUpdateDto dto)
    {
        var meter = await _repo.GetByIdAsync(id);
        if (meter is null) return null;

        if (dto.Model              is not null) meter.Model              = dto.Model;
        if (dto.Status             is not null) meter.Status             = dto.Status.Value;
        if (dto.RelayStatus        is not null) meter.RelayStatus        = dto.RelayStatus.Value;
        if (dto.CommunicationType  is not null) meter.CommunicationType  = dto.CommunicationType.Value;
        if (dto.DcuId              is not null) meter.DcuId              = dto.DcuId;
        if (dto.SimNumber          is not null) meter.SimNumber          = dto.SimNumber;
        if (dto.DlmsLogicalDeviceName is not null) meter.DlmsLogicalDeviceName = dto.DlmsLogicalDeviceName;
        if (dto.ConsumerNumber     is not null) meter.ConsumerNumber     = dto.ConsumerNumber;
        if (dto.InstallationAddress is not null) meter.InstallationAddress = dto.InstallationAddress;
        if (dto.Latitude           is not null) meter.Latitude           = dto.Latitude;
        if (dto.Longitude          is not null) meter.Longitude          = dto.Longitude;
        if (dto.Zone               is not null) meter.Zone               = dto.Zone;
        if (dto.SubstationId       is not null) meter.SubstationId       = dto.SubstationId;
        if (dto.FirmwareVersion    is not null) meter.FirmwareVersion    = dto.FirmwareVersion;
        if (dto.TodEnabled         is not null) meter.TodEnabled         = dto.TodEnabled.Value;
        if (dto.Notes              is not null) meter.Notes              = dto.Notes;

        meter.LastConfiguredAt = DateTime.UtcNow;
        var updated = await _repo.UpdateAsync(meter);
        await _cache.RemoveAsync(AllKey);
        return Map(updated);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var ok = await _repo.DeleteAsync(id);
        if (ok) await _cache.RemoveAsync(AllKey);
        return ok;
    }

    public async Task<MeterResponseDto?> SetRelayAsync(Guid id, RelayCommandDto cmd, string operatorName)
    {
        var meter = await _repo.GetByIdAsync(id);
        if (meter is null) return null;

        if (!meter.IsSmart)
            throw new InvalidOperationException(
                $"Meter {meter.SerialNumber} (model: {meter.Model}) does not support remote relay control. Only smart meters support this.");

        meter.RelayStatus = cmd.Action.Trim().ToLower() switch
        {
            "connect"    => RelayStatus.Connected,
            "disconnect" => RelayStatus.Disconnected,
            _ => throw new ArgumentException(
                $"Invalid action '{cmd.Action}'. Allowed values: 'connect', 'disconnect'.")
        };

        var note = $"[{DateTime.UtcNow:u}] Relay {cmd.Action} by {operatorName}. Reason: {cmd.Reason ?? "not specified"}";
        meter.Notes = meter.Notes is null ? note : $"{meter.Notes}\n{note}";
        meter.LastConfiguredAt = DateTime.UtcNow;

        var updated = await _repo.UpdateAsync(meter);
        await _cache.RemoveAsync(AllKey);
        _log.LogInformation("Relay {Action} on {Serial} by {Operator}", cmd.Action, meter.SerialNumber, operatorName);
        return Map(updated);
    }

    private static MeterResponseDto Map(Meter m) => new()
    {
        Id = m.Id, SerialNumber = m.SerialNumber, Model = m.Model,
        MeterType = m.MeterType.ToString(), IsSmart = m.IsSmart, IsPrepaid = m.IsPrepaid,
        AccuracyClass = m.AccuracyClass, VoltageRating = m.VoltageRating,
        CurrentRating = m.CurrentRating, Standards = m.Standards,
        CommunicationType = m.CommunicationType.ToString(),
        DcuId = m.DcuId, SimNumber = m.SimNumber,
        DlmsLogicalDeviceName = m.DlmsLogicalDeviceName,
        ConsumerNumber = m.ConsumerNumber, InstallationAddress = m.InstallationAddress,
        Latitude = m.Latitude, Longitude = m.Longitude,
        Zone = m.Zone, SubstationId = m.SubstationId,
        Status = m.Status.ToString(), RelayStatus = m.RelayStatus.ToString(),
        FirmwareVersion = m.FirmwareVersion, TodEnabled = m.TodEnabled,
        OnboardedAt = m.OnboardedAt, LastCommunicatedAt = m.LastCommunicatedAt,
        LastBillingReadAt = m.LastBillingReadAt, LastConfiguredAt = m.LastConfiguredAt,
        Notes = m.Notes
    };
}
