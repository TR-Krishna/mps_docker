// ═══════════════════════════════════════════════════════════════════════════
// MeterManagementService/Models/Meter.cs
// ═══════════════════════════════════════════════════════════════════════════
//
// Domain model for Schneider Electric smart meters.
// Fields are derived directly from the SE MPS Mysuru product catalogue
// (slides 3–8) and EcoSEnter HES data model.
//
// PRODUCT RANGE covered by this model:
//   AURORA          Single-phase smart       IS 16444 Pt 1, Class 1.0
//   TAURUS          Single-phase prepaid     IS 15884
//   REGOR           3-phase whole-current    IS 16444 Pt 1, Class 1.0
//   ATRIA           3-phase prepaid          IS 15884, dual-source EB+DG
//   REGOR LTCT      3-phase LT CT            IS 16444 Pt 2, Class 0.5s, -/5A
//   ER300P HTCT     3-phase HT CT            IS 14697, Class 0.2s/0.5s
//   ORION           Thread-through           40–200A, optional GPRS
//   ER300P          3-phase non-smart        IS 13779, Class 1.0
// ═══════════════════════════════════════════════════════════════════════════

namespace MeterManagementService.Models;

/// <summary>
/// A meter registered in the AMI / HES system.
/// Maps to the "Meters" PostgreSQL table via EF Core.
/// </summary>
public class Meter
{
    // ── Identity & Classification ─────────────────────────────────────────
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Physical serial number stamped on the meter body.
    /// Globally unique. Used for all field operations and DLMS addressing.
    /// </summary>
    public required string SerialNumber { get; set; }

    /// <summary>
    /// Product model name. E.g. "AURORA", "REGOR", "REGOR LTCT", "ORION", "TAURUS", "ATRIA", "ER300P".
    /// Determines which data profiles (load survey, billing, tamper) are supported.
    /// </summary>
    public required string Model { get; set; }

    /// <summary>Meter phase/measurement category from the SE product catalogue.</summary>
    public MeterType MeterType { get; set; }

    /// <summary>
    /// Whether this is a smart meter (remote read) or a digital-only meter (local read).
    /// Non-smart meters have no remote communication capability.
    /// </summary>
    public bool IsSmart { get; set; } = true;

    /// <summary>
    /// Whether this is a prepaid meter (TAURUS / ATRIA).
    /// Prepaid meters manage credit balances and relay control based on balance.
    /// </summary>
    public bool IsPrepaid { get; set; } = false;

    // ── Technical Specifications (from catalogue) ─────────────────────────
    /// <summary>Accuracy class per IS standard. E.g. "Class 1.0", "Class 0.5s", "Class 0.2s".</summary>
    public required string AccuracyClass { get; set; }

    /// <summary>Nominal voltage. E.g. "240V (P-N)", "3×240V (P-N)", "3×63.5V (P-N)".</summary>
    public required string VoltageRating { get; set; }

    /// <summary>Current rating. E.g. "5-30A", "10-60A", "-/5A", "-/1A", "40-200A".</summary>
    public required string CurrentRating { get; set; }

    /// <summary>IS standards this meter is certified under. E.g. "IS 16444 Part 1, IS 15959".</summary>
    public required string Standards { get; set; }

    // ── Communication ─────────────────────────────────────────────────────
    /// <summary>Primary communication technology used to connect meter to HES.</summary>
    public CommunicationType CommunicationType { get; set; }

    /// <summary>
    /// Data Concentrator Unit ID for RF Mesh / NAN meters.
    /// Null for direct-cellular meters (they talk to HES directly via SIM).
    /// </summary>
    public string? DcuId { get; set; }

    /// <summary>SIM/IMSI number for cellular meters (4G or NB-IoT).</summary>
    public string? SimNumber { get; set; }

    /// <summary>DLMS logical device name used for COSEM addressing (e.g. "SAG0012345678").</summary>
    public string? DlmsLogicalDeviceName { get; set; }

    // ── Installation ──────────────────────────────────────────────────────
    /// <summary>Consumer account number from the utility billing system.</summary>
    public string? ConsumerNumber { get; set; }

    /// <summary>Physical installation address.</summary>
    public string? InstallationAddress { get; set; }

    /// <summary>GPS latitude of installation. Null if not yet surveyed.</summary>
    public double? Latitude { get; set; }

    /// <summary>GPS longitude of installation. Null if not yet surveyed.</summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Metering zone / feeder / substation name for grouping.
    /// E.g. "MYSURU-NORTH", "FEEDER-12", "SS-HEBBAL".
    /// </summary>
    public string? Zone { get; set; }

    /// <summary>Substation or distribution transformer identifier.</summary>
    public string? SubstationId { get; set; }

    // ── Status & Control ──────────────────────────────────────────────────
    /// <summary>Lifecycle status of this meter in the HES.</summary>
    public MeterStatus Status { get; set; } = MeterStatus.Commissioned;

    /// <summary>
    /// Current state of the meter's internal relay.
    /// Smart meters support remote connect/disconnect via HES command (DLMS).
    /// </summary>
    public RelayStatus RelayStatus { get; set; } = RelayStatus.Connected;

    /// <summary>Firmware version as reported by the meter (e.g. "2.1.4-AURORA").</summary>
    public string? FirmwareVersion { get; set; }

    // ── TOD / Tariff (for smart meters) ──────────────────────────────────
    /// <summary>
    /// Whether Time-of-Use / Time-of-Day tariff is configured on this meter.
    /// TOD meters have different tariff rates by time slot (e.g. peak 18:00–22:00).
    /// </summary>
    public bool TodEnabled { get; set; } = false;

    // ── Timestamps ────────────────────────────────────────────────────────
    public DateTime  OnboardedAt          { get; set; } = DateTime.UtcNow;
    public DateTime? LastCommunicatedAt   { get; set; }
    public DateTime? LastBillingReadAt    { get; set; }
    public DateTime? LastConfiguredAt     { get; set; }

    /// <summary>Notes from field engineers (installation remarks, fault history, etc.).</summary>
    public string? Notes { get; set; }
}

// ─── Enumerations ─────────────────────────────────────────────────────────────

/// <summary>
/// Meter type / phase configuration.
/// Directly maps to the SE MPS Mysuru product catalogue (PPT slides 3–8).
/// </summary>
public enum MeterType
{
    SinglePhaseSmart       = 1,   // AURORA — IS 16444 Pt 1 — residential
    SinglePhasePrepaid     = 2,   // TAURUS — IS 15884
    ThreePhaseWholeCurrent = 3,   // REGOR  — IS 16444 Pt 1 — commercial
    ThreePhasePrepaid      = 4,   // ATRIA  — IS 15884, dual-source EB+DG
    ThreePhaseLTCT         = 5,   // REGOR LTCT — IS 16444 Pt 2, -/5A CT
    ThreePhaseHTCT         = 6,   // ER300P HTCT — IS 14697, Class 0.2s, grid
    ThreadThrough          = 7,   // ORION — 40-200A, optional GPRS modem
    ThreePhaseDigital      = 8    // ER300P non-smart — IS 13779
}

/// <summary>
/// Communication technology used by the meter to reach the Head End System.
/// From PPT slide 4 and network architecture slide 13.
/// </summary>
public enum CommunicationType
{
    Optical    = 0,   // Local optical port only — no remote read
    RFMesh     = 1,   // RF Mesh/PLC → DCU/Gateway → WAN → HES (NAN path)
    Cellular4G = 2,   // 4G LTE SIM → cellular tower → HES (direct WAN)
    NBIoT      = 3,   // NB-IoT SIM → cellular tower → HES (low-power)
    RS485      = 4    // Wired RS-485 for CT meters in panel installations
}

/// <summary>Meter lifecycle status in the HES system.</summary>
public enum MeterStatus
{
    Inventory      = 0,   // In stock, not yet deployed
    Commissioned   = 1,   // Installed and communicating normally
    Pending        = 2,   // Installed, waiting for first communication
    Faulty         = 3,   // Communication failure — needs field investigation
    Decommissioned = 4    // Removed from service
}

/// <summary>
/// State of the meter's internal relay (load control).
/// Smart meters (AURORA, REGOR) support remote connect/disconnect via DLMS command.
/// </summary>
public enum RelayStatus
{
    Connected    = 0,   // Relay closed — consumer load is on
    Disconnected = 1    // Relay open — consumer disconnected (prepaid or remote action)
}
