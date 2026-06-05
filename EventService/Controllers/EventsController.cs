
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ── Controller ────────────────────────────────────────────────────────────────
namespace EventService.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    // Static mock event store — in production this is a database
    private static readonly List<MeterEvent> _store = GenerateSeedEvents();

    /// <summary>
    /// List all events with optional filtering.
    /// </summary>
    /// <param name="serialNumber">Filter by meter serial number</param>
    /// <param name="category">Filter by category: Tamper, Outage, System, Billing, Communication</param>
    /// <param name="severity">Filter by severity: Critical, High, Medium, Low</param>
    /// <param name="acknowledged">Filter by acknowledgement status</param>
    /// <param name="from">Filter events after this UTC datetime</param>
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? serialNumber = null,
        [FromQuery] string? category     = null,
        [FromQuery] string? severity     = null,
        [FromQuery] bool?   acknowledged = null,
        [FromQuery] DateTime? from       = null)
    {
        var q = _store.AsEnumerable();
        if (serialNumber is not null) q = q.Where(e => e.SerialNumber == serialNumber);
        if (category     is not null) q = q.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (severity     is not null) q = q.Where(e => e.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase));
        if (acknowledged is not null) q = q.Where(e => e.Acknowledged == acknowledged.Value);
        if (from         is not null) q = q.Where(e => e.Timestamp >= from.Value);
        var results = q.OrderByDescending(e => e.Timestamp).ToList();
        return Ok(new { count = results.Count, data = results });
    }

    /// <summary>Get a single event by ID.</summary>
    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var e = _store.FirstOrDefault(x => x.Id == id);
        return e is null ? NotFound(new { message = $"Event '{id}' not found." }) : Ok(e);
    }

    /// <summary>
    /// Get all unacknowledged critical/high severity tamper events.
    /// Shortcut for the HES dashboard alarm panel.
    /// </summary>
    [HttpGet("alarms/active")]
    public IActionResult GetActiveAlarms()
    {
        var alarms = _store
            .Where(e => !e.Acknowledged && (e.Severity == "Critical" || e.Severity == "High"))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
        return Ok(new { count = alarms.Count, data = alarms });
    }

    /// <summary>Acknowledge an event (mark as reviewed by operator).</summary>
    [HttpPost("{id}/acknowledge")]
    [Authorize(Roles = "utility_admin,field_engineer")]
    public IActionResult Acknowledge(string id)
    {
        var e = _store.FirstOrDefault(x => x.Id == id);
        if (e is null) return NotFound(new { message = $"Event '{id}' not found." });
        e.Acknowledged    = true;
        e.AcknowledgedAt  = DateTime.UtcNow;
        e.AcknowledgedBy  = User.FindFirst("displayName")?.Value ?? "unknown";
        return Ok(e);
    }

    private static List<MeterEvent> GenerateSeedEvents()
    {
        var serials = new[] { "AU240001", "AU240002", "RG3P0042", "RG3P0043", "OR200001" };
        var categories = new[]
        {
            ("Tamper",        "MagneticTamper",          "Critical"),
            ("Tamper",        "CoverOpen",               "High"),
            ("Tamper",        "NeutralDisturbance",       "Critical"),
            ("Tamper",        "ReverseEnergy",            "High"),
            ("Outage",        "PowerFailure",             "High"),
            ("Outage",        "PowerRestoration",         "Medium"),
            ("Outage",        "VoltageSag",               "Medium"),
            ("System",        "BatteryLow",               "Low"),
            ("System",        "ClockFault",               "Medium"),
            ("Billing",       "DemandReset",              "Low"),
            ("Communication", "SessionLost",              "Medium"),
            ("Communication", "AuthenticationFailure",    "High"),
        };
        var rng = new Random(99);
        var events = new List<MeterEvent>();
        for (int i = 0; i < 40; i++)
        {
            var (cat, type, sev) = categories[rng.Next(categories.Length)];
            events.Add(new MeterEvent
            {
                Id           = $"EVT-{i+1:D4}",
                SerialNumber = serials[rng.Next(serials.Length)],
                Timestamp    = DateTime.UtcNow.AddHours(-rng.Next(1, 720)),
                Category     = cat,
                EventType    = type,
                Severity     = sev,
                Description  = $"[MOCK] {type} detected on meter {serials[rng.Next(serials.Length)]}",
                Acknowledged = rng.Next(3) == 0,
                Source       = "EcoSEnter-Mock"
            });
        }
        return events.OrderByDescending(e => e.Timestamp).ToList();
    }
}

public class MeterEvent
{
    public string   Id           { get; set; } = "";
    public string   SerialNumber { get; set; } = "";
    public DateTime Timestamp    { get; set; }
    public string   Category     { get; set; } = "";   // Tamper, Outage, System, Billing, Communication
    public string   EventType    { get; set; } = "";
    public string   Severity     { get; set; } = "";   // Critical, High, Medium, Low
    public string   Description  { get; set; } = "";
    public string   Source       { get; set; } = "";
    public bool     Acknowledged { get; set; } = false;
    public DateTime? AcknowledgedAt { get; set; }
    public string?  AcknowledgedBy  { get; set; }
}
