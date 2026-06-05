
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;


// ── Controller ────────────────────────────────────────────────────────────────
namespace DataCollectionService.Controllers;

[ApiController]
[Route("api/data")]
[Authorize]
[Produces("application/json")]
public class DataController : ControllerBase
{
    private static readonly Random Rng = new(42);

    /// <summary>
    /// Get Load Survey (interval energy) data for a meter.
    /// Returns 15-minute interval kWh/kVArh readings for the requested period.
    /// Profile: IS 16444 / DLMS OBIS 1.0.99.1.0.255
    /// </summary>
    /// <param name="serialNumber">Meter serial number</param>
    /// <param name="from">Start UTC datetime (ISO 8601)</param>
    /// <param name="to">End UTC datetime (ISO 8601). Max range: 31 days.</param>
    [HttpGet("load-survey/{serialNumber}")]
    [ProducesResponseType(200)]
    public IActionResult GetLoadSurvey(string serialNumber,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var records = new List<object>();
        var ts = from;
        while (ts <= to)
        {
            records.Add(new
            {
                timestamp     = ts,
                serial        = serialNumber,
                kwh_import    = Math.Round(Rng.NextDouble() * 0.5 + 0.1, 3),
                kwh_export    = Math.Round(Rng.NextDouble() * 0.02, 3),
                kvarh_import  = Math.Round(Rng.NextDouble() * 0.2 + 0.05, 3),
                kvarh_export  = Math.Round(Rng.NextDouble() * 0.01, 3),
                profile       = "LoadSurvey_15min",
                obis          = "1.0.99.1.0.255"
            });
            ts = ts.AddMinutes(15);
        }
        return Ok(new { serialNumber, from, to, intervalMinutes = 15, count = records.Count, data = records });
    }

    /// <summary>
    /// Get Billing Profile (cumulative monthly energy) for a meter.
    /// Returns end-of-month cumulative kWh, kVArh, maximum demand.
    /// Profile: DLMS OBIS 1.0.98.1.0.255
    /// </summary>
    /// <param name="serialNumber">Meter serial number</param>
    /// <param name="months">Number of months to return (max 24, default 3)</param>
    [HttpGet("billing/{serialNumber}")]
    [ProducesResponseType(200)]
    public IActionResult GetBilling(string serialNumber, [FromQuery] int months = 3)
    {
        months = Math.Clamp(months, 1, 24);
        var records = new List<object>();
        var now = DateTime.UtcNow;
        for (int i = months - 1; i >= 0; i--)
        {
            var billingDate = new DateTime(now.Year, now.Month, 1).AddMonths(-i).AddDays(-1);
            records.Add(new
            {
                billingDate      = billingDate,
                serial           = serialNumber,
                kwh_import_cumul = Math.Round(Rng.NextDouble() * 500 + 100, 2),
                kwh_export_cumul = Math.Round(Rng.NextDouble() * 5, 2),
                kvarh_import     = Math.Round(Rng.NextDouble() * 150 + 30, 2),
                max_demand_kw    = Math.Round(Rng.NextDouble() * 5 + 1, 2),
                md_timestamp     = billingDate.AddHours(-Rng.Next(1, 48)),
                profile          = "BillingProfile",
                obis             = "1.0.98.1.0.255"
            });
        }
        return Ok(new { serialNumber, months, count = records.Count, data = records });
    }

    /// <summary>
    /// Get Instantaneous Profile (real-time snapshot) for a meter.
    /// Returns voltage, current, power factor, frequency, active/reactive power.
    /// Profile: DLMS OBIS 1.0.94.91.0.255 (varies by meter model)
    /// </summary>
    [HttpGet("instantaneous/{serialNumber}")]
    [ProducesResponseType(200)]
    public IActionResult GetInstantaneous(string serialNumber)
    {
        return Ok(new
        {
            serialNumber  = serialNumber,
            capturedAt    = DateTime.UtcNow,
            voltage_v     = new { R = Math.Round(230 + Rng.NextDouble() * 10 - 5, 1), Y = Math.Round(230 + Rng.NextDouble() * 10 - 5, 1), B = Math.Round(230 + Rng.NextDouble() * 10 - 5, 1) },
            current_a     = new { R = Math.Round(Rng.NextDouble() * 20 + 2, 2), Y = Math.Round(Rng.NextDouble() * 20 + 2, 2), B = Math.Round(Rng.NextDouble() * 20 + 2, 2) },
            power_factor  = Math.Round(0.85 + Rng.NextDouble() * 0.14, 3),
            frequency_hz  = Math.Round(50.0 + Rng.NextDouble() * 0.2 - 0.1, 2),
            active_power_kw   = Math.Round(Rng.NextDouble() * 5 + 0.5, 3),
            reactive_power_kvar = Math.Round(Rng.NextDouble() * 2, 3),
            apparent_power_kva  = Math.Round(Rng.NextDouble() * 6 + 0.5, 3),
            profile       = "Instantaneous",
            obis          = "1.0.94.91.0.255",
            note          = "MOCK DATA — values are randomly generated"
        });
    }

    /// <summary>
    /// Trigger an on-demand read for a meter.
    /// In production: sends a DLMS GetRequest to the meter via EcoSEnter.
    /// Mock: returns a simulated acknowledgement.
    /// </summary>
    [HttpPost("trigger-read/{serialNumber}")]
    [Authorize(Roles = "utility_admin,field_engineer")]
    [ProducesResponseType(202)]
    public IActionResult TriggerRead(string serialNumber)
    {
        return Accepted(new
        {
            serialNumber = serialNumber,
            requestId    = Guid.NewGuid().ToString(),
            status       = "queued",
            message      = $"On-demand read queued for {serialNumber}. Results will be available in /api/data/instantaneous/{serialNumber} within 60 seconds.",
            note         = "MOCK — in production this triggers a DLMS GetRequest via EcoSEnter HES"
        });
    }
}
