// ???????????????????????????????????????????????????????????????????????????
// MeterManagementService/DTOs/MeterDtos.cs
// ???????????????????????????????????????????????????????????????????????????

using System.ComponentModel.DataAnnotations;
using MeterManagementService.Models;

namespace MeterManagementService.DTOs;

// ?? CREATE ????????????????????????????????????????????????????????????????
/// <summary>Payload for onboarding a new meter into the HES.</summary>
public class MeterCreateDto
{
    [Required][MaxLength(30)]  public required string SerialNumber  { get; set; }
    [Required][MaxLength(50)]  public required string Model         { get; set; }
    [Required]                 public MeterType       MeterType     { get; set; }
    [Required][MaxLength(20)]  public required string AccuracyClass { get; set; }
    [Required][MaxLength(40)]  public required string VoltageRating { get; set; }
    [Required][MaxLength(30)]  public required string CurrentRating { get; set; }
    [Required][MaxLength(150)] public required string Standards     { get; set; }

    public bool IsSmart   { get; set; } = true;
    public bool IsPrepaid { get; set; } = false;

    public CommunicationType CommunicationType    { get; set; } = CommunicationType.RFMesh;
    public string?           DcuId                { get; set; }
    public string?           SimNumber            { get; set; }
    public string?           DlmsLogicalDeviceName{ get; set; }
    public string?           ConsumerNumber       { get; set; }
    public string?           InstallationAddress  { get; set; }
    public double?           Latitude             { get; set; }
    public double?           Longitude            { get; set; }
    public string?           Zone                 { get; set; }
    public string?           SubstationId         { get; set; }
    public string?           FirmwareVersion      { get; set; }
    public bool              TodEnabled           { get; set; } = false;
    public string?           Notes                { get; set; }
}

// ?? UPDATE ????????????????????????????????????????????????????????????????
/// <summary>All fields optional — only provided fields are changed.</summary>
public class MeterUpdateDto
{
    [MaxLength(50)]  public string? Model              { get; set; }
    public MeterStatus?            Status              { get; set; }
    public RelayStatus?            RelayStatus         { get; set; }
    public CommunicationType?      CommunicationType   { get; set; }
    public string?                 DcuId               { get; set; }
    public string?                 SimNumber           { get; set; }
    public string?                 DlmsLogicalDeviceName{ get; set; }
    public string?                 ConsumerNumber      { get; set; }
    public string?                 InstallationAddress { get; set; }
    public double?                 Latitude            { get; set; }
    public double?                 Longitude           { get; set; }
    public string?                 Zone                { get; set; }
    public string?                 SubstationId        { get; set; }
    public string?                 FirmwareVersion     { get; set; }
    public bool?                   TodEnabled          { get; set; }
    public string?                 Notes               { get; set; }
}

// ?? RESPONSE ?????????????????????????????????????????????????????????????
/// <summary>Full meter details returned by API responses.</summary>
public class MeterResponseDto
{
    public Guid    Id                    { get; set; }
    public string  SerialNumber          { get; set; } = "";
    public string  Model                 { get; set; } = "";
    public string  MeterType             { get; set; } = "";
    public bool    IsSmart               { get; set; }
    public bool    IsPrepaid             { get; set; }
    public string  AccuracyClass         { get; set; } = "";
    public string  VoltageRating         { get; set; } = "";
    public string  CurrentRating         { get; set; } = "";
    public string  Standards             { get; set; } = "";
    public string  CommunicationType     { get; set; } = "";
    public string? DcuId                 { get; set; }
    public string? SimNumber             { get; set; }
    public string? DlmsLogicalDeviceName { get; set; }
    public string? ConsumerNumber        { get; set; }
    public string? InstallationAddress   { get; set; }
    public double? Latitude              { get; set; }
    public double? Longitude             { get; set; }
    public string? Zone                  { get; set; }
    public string? SubstationId          { get; set; }
    public string  Status                { get; set; } = "";
    public string  RelayStatus           { get; set; } = "";
    public string? FirmwareVersion       { get; set; }
    public bool    TodEnabled            { get; set; }
    public DateTime  OnboardedAt         { get; set; }
    public DateTime? LastCommunicatedAt  { get; set; }
    public DateTime? LastBillingReadAt   { get; set; }
    public DateTime? LastConfiguredAt    { get; set; }
    public string?   Notes               { get; set; }
}

// ?? RELAY COMMAND ?????????????????????????????????????????????????????????
/// <summary>Remote relay connect/disconnect command payload.</summary>
public class RelayCommandDto
{
    /// <summary>"connect" or "disconnect"</summary>
    [Required] public required string Action { get; set; }
    /// <summary>Reason for audit trail (e.g. "Prepaid balance exhausted", "Non-payment").</summary>
    [MaxLength(200)] public string? Reason { get; set; }
}
