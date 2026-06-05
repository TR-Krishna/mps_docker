// ═══════════════════════════════════════════════════════════════════════════
// MeterManagementService/Controllers/MeterController.cs
// ═══════════════════════════════════════════════════════════════════════════
//
// V1: Full CRUD + relay control
// V2: V1 + multi-dimensional filtering (type, status, commType, zone, substationId)
//
// ENDPOINTS:
//   GET    /api/v{ver}/meters                   List meters (cached 60s)
//   GET    /api/v{ver}/meters/{id}              Get by GUID
//   GET    /api/v{ver}/meters/serial/{sn}       Get by serial number
//   POST   /api/v{ver}/meters                   Onboard new meter
//   PUT    /api/v{ver}/meters/{id}              Update meter
//   DELETE /api/v{ver}/meters/{id}              Decommission meter
//   POST   /api/v{ver}/meters/{id}/relay        Remote relay control (smart meters only)
// ═══════════════════════════════════════════════════════════════════════════

using System.Security.Claims;
using Asp.Versioning;
using MeterManagementService.DTOs;
using MeterManagementService.Models;
using MeterManagementService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeterManagementService.Controllers;

// ─── V1 ───────────────────────────────────────────────────────────────────────
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/meters")]
[Authorize]
[Produces("application/json")]
public class MeterV1Controller : ControllerBase
{
    private readonly IMeterService _svc;
    private readonly ILogger<MeterV1Controller> _log;
    public MeterV1Controller(IMeterService svc, ILogger<MeterV1Controller> log) { _svc = svc; _log = log; }

    /// <summary>List all meters (results cached 60 s). Use v2 for filtering.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MeterResponseDto>), 200)]
    public async Task<IActionResult> GetAll()
        => Ok(await _svc.GetAllAsync(null, null, null, null, null));

    /// <summary>Get meter by system GUID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MeterResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var m = await _svc.GetByIdAsync(id);
        return m is null ? NotFound(new { message = $"Meter '{id}' not found." }) : Ok(m);
    }

    /// <summary>
    /// Get meter by physical serial number.
    /// Most useful for field engineers who have the meter serial but not the system ID.
    /// </summary>
    [HttpGet("serial/{serialNumber}")]
    [ProducesResponseType(typeof(MeterResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBySerial(string serialNumber)
    {
        var m = await _svc.GetBySerialAsync(serialNumber);
        return m is null ? NotFound(new { message = $"No meter with serial '{serialNumber}'." }) : Ok(m);
    }

    /// <summary>
    /// Onboard a new meter into the HES.
    /// Returns 409 if the SerialNumber is already registered.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "utility_admin,field_engineer")]
    [ProducesResponseType(typeof(MeterResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] MeterCreateDto dto)
    {
        try
        {
            var created = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Update meter configuration. Only non-null fields are changed.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "utility_admin,field_engineer")]
    [ProducesResponseType(typeof(MeterResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] MeterUpdateDto dto)
    {
        var updated = await _svc.UpdateAsync(id, dto);
        return updated is null ? NotFound(new { message = $"Meter '{id}' not found." }) : Ok(updated);
    }

    /// <summary>Remove a meter from the HES system (permanent).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "utility_admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _svc.DeleteAsync(id);
        return deleted ? NoContent() : NotFound(new { message = $"Meter '{id}' not found." });
    }

    /// <summary>
    /// Issue a remote relay connect/disconnect command to a smart meter.
    /// In production: triggers a DLMS SetRequest to the meter via EcoSEnter.
    /// In this mock: updates the RelayStatus field in the database.
    /// Only supported on IsSmart=true meters.
    /// </summary>
    [HttpPost("{id:guid}/relay")]
    [Authorize(Roles = "utility_admin,field_engineer")]
    [ProducesResponseType(typeof(MeterResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetRelay(Guid id, [FromBody] RelayCommandDto cmd)
    {
        var operatorName = User.FindFirstValue("displayName")
                        ?? User.FindFirstValue(ClaimTypes.Name)
                        ?? "unknown";
        try
        {
            var updated = await _svc.SetRelayAsync(id, cmd, operatorName);
            return updated is null ? NotFound(new { message = $"Meter '{id}' not found." }) : Ok(updated);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

// ─── V2 — adds advanced filtering ─────────────────────────────────────────────
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/meters")]
[Authorize]
[Produces("application/json")]
public class MeterV2Controller : ControllerBase
{
    private readonly IMeterService _svc;
    public MeterV2Controller(IMeterService svc) => _svc = svc;

    /// <summary>
    /// List meters with optional filtering. [NEW IN V2]
    /// All parameters are optional and combinable.
    /// </summary>
    /// <param name="type">MeterType: 1=SinglePhaseSmart, 3=ThreePhaseWholeCurrent, 5=ThreePhaseLTCT, 6=ThreePhaseHTCT, 7=ThreadThrough …</param>
    /// <param name="status">MeterStatus: 1=Commissioned, 2=Pending, 3=Faulty …</param>
    /// <param name="commType">CommunicationType: 1=RFMesh, 2=Cellular4G, 3=NBIoT …</param>
    /// <param name="zone">Zone identifier e.g. "MYSURU-NORTH"</param>
    /// <param name="substationId">Substation/feeder ID e.g. "SS-HEBBAL"</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MeterResponseDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] MeterType? type = null,
        [FromQuery] MeterStatus? status = null,
        [FromQuery] CommunicationType? commType = null,
        [FromQuery] string? zone = null,
        [FromQuery] string? substationId = null)
    => Ok(await _svc.GetAllAsync(type, status, commType, zone, substationId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    { var m = await _svc.GetByIdAsync(id); return m is null ? NotFound() : Ok(m); }

    [HttpGet("serial/{sn}")]
    public async Task<IActionResult> GetBySerial(string sn)
    { var m = await _svc.GetBySerialAsync(sn); return m is null ? NotFound() : Ok(m); }

    [HttpPost] [Authorize(Roles = "utility_admin,field_engineer")]
    public async Task<IActionResult> Create([FromBody] MeterCreateDto dto)
    {
        try { var c = await _svc.CreateAsync(dto); return CreatedAtAction(nameof(GetById), new { id = c.Id }, c); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")] [Authorize(Roles = "utility_admin,field_engineer")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MeterUpdateDto dto)
    { var u = await _svc.UpdateAsync(id, dto); return u is null ? NotFound() : Ok(u); }

    [HttpDelete("{id:guid}")] [Authorize(Roles = "utility_admin")]
    public async Task<IActionResult> Delete(Guid id)
    { return await _svc.DeleteAsync(id) ? NoContent() : NotFound(); }

    [HttpPost("{id:guid}/relay")] [Authorize(Roles = "utility_admin,field_engineer")]
    public async Task<IActionResult> SetRelay(Guid id, [FromBody] RelayCommandDto cmd)
    {
        var op = User.FindFirstValue("displayName") ?? "unknown";
        try { var u = await _svc.SetRelayAsync(id, cmd, op); return u is null ? NotFound() : Ok(u); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}
