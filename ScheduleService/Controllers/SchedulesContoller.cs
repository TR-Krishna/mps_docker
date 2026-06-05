using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// ── Controller ────────────────────────────────────────────────────────────────
namespace ScheduleService.Controllers;

[ApiController]
[Route("api/schedules")]
[Authorize]
[Produces("application/json")]
public class SchedulesController : ControllerBase
{
    // Static mock schedule store
    private static readonly List<CollectionSchedule> _schedules = new()
    {
        new() { Id = "SCH-001", Name = "Load Survey 15-min (All Zones)",
            ProfileType = "LoadSurvey", IntervalMinutes = 15,
            CronExpression = "*/15 * * * *", Active = true, ZoneFilter = null,
            Description = "15-minute interval energy data for all smart meters" },
        new() { Id = "SCH-002", Name = "Billing Read (Monthly)",
            ProfileType = "Billing", IntervalMinutes = 43200,
            CronExpression = "0 0 1 * *", Active = true, ZoneFilter = null,
            Description = "End-of-month billing data on 1st at midnight" },
        new() { Id = "SCH-003", Name = "Instantaneous Read (4-hourly)",
            ProfileType = "Instantaneous", IntervalMinutes = 240,
            CronExpression = "0 */4 * * *", Active = true, ZoneFilter = null,
            Description = "Real-time snapshot every 4 hours for health monitoring" },
        new() { Id = "SCH-004", Name = "Load Survey 30-min (MYSURU-NORTH)",
            ProfileType = "LoadSurvey", IntervalMinutes = 30,
            CronExpression = "*/30 * * * *", Active = false, ZoneFilter = "MYSURU-NORTH",
            Description = "30-min interval for MYSURU-NORTH zone (disabled)" },
    };

    /// <summary>List all collection schedules.</summary>
    [HttpGet]
    public IActionResult GetAll([FromQuery] bool? active = null)
    {
        var q = _schedules.AsEnumerable();
        if (active is not null) q = q.Where(s => s.Active == active.Value);
        return Ok(new { count = q.Count(), data = q.ToList() });
    }

    /// <summary>Get a schedule by ID.</summary>
    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var s = _schedules.FirstOrDefault(x => x.Id == id);
        return s is null ? NotFound(new { message = $"Schedule '{id}' not found." }) : Ok(s);
    }

    /// <summary>Create a new collection schedule.</summary>
    [HttpPost]
    [Authorize(Roles = "utility_admin")]
    public IActionResult Create([FromBody] CreateScheduleDto dto)
    {
        var schedule = new CollectionSchedule
        {
            Id             = $"SCH-{_schedules.Count + 1:D3}",
            Name           = dto.Name,
            ProfileType    = dto.ProfileType,
            IntervalMinutes= dto.IntervalMinutes,
            CronExpression = dto.CronExpression,
            ZoneFilter     = dto.ZoneFilter,
            Description    = dto.Description,
            Active         = true,
            CreatedAt      = DateTime.UtcNow
        };
        _schedules.Add(schedule);
        return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, schedule);
    }

    /// <summary>Enable or disable a schedule.</summary>
    [HttpPatch("{id}/toggle")]
    [Authorize(Roles = "utility_admin")]
    public IActionResult Toggle(string id)
    {
        var s = _schedules.FirstOrDefault(x => x.Id == id);
        if (s is null) return NotFound(new { message = $"Schedule '{id}' not found." });
        s.Active = !s.Active;
        return Ok(s);
    }
}

public class CollectionSchedule
{
    public string  Id              { get; set; } = "";
    public string  Name            { get; set; } = "";
    public string  ProfileType     { get; set; } = ""; // LoadSurvey, Billing, Instantaneous
    public int     IntervalMinutes { get; set; }
    public string  CronExpression  { get; set; } = "";
    public bool    Active          { get; set; }
    public string? ZoneFilter      { get; set; }
    public string? Description     { get; set; }
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
}

public class CreateScheduleDto
{
    public required string Name            { get; set; }
    public required string ProfileType     { get; set; }
    public int             IntervalMinutes { get; set; }
    public required string CronExpression  { get; set; }
    public string?         ZoneFilter      { get; set; }
    public string?         Description     { get; set; }
}
