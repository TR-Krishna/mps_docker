// ═══════════════════════════════════════════════════════════════════════════
// MeterManagement.Tests/Integration/MeterApiIntegrationTests.cs
// ═══════════════════════════════════════════════════════════════════════════
// Integration tests: real in-memory HTTP server, real middleware pipeline,
// in-memory database (no PostgreSQL), full JWT flow.
// ═══════════════════════════════════════════════════════════════════════════

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MeterManagementService.Data;
using MeterManagementService.DTOs;
using MeterManagementService.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MeterManagement.Tests.Integration;

// ── Test server factory ───────────────────────────────────────────────────────
public class MeterApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureServices(services =>
        {
            // Remove PostgreSQL, replace with in-memory DB
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MeterDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<MeterDbContext>(o =>
                o.UseInMemoryDatabase("TestMeterDb_" + Guid.NewGuid()));

            // Seed test DB
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeterDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

// ── Tests ────────────────────────────────────────────────────────────────────
public class MeterApiIntegrationTests : IClassFixture<MeterApiFactory>
{
    private readonly HttpClient _client;
    public MeterApiIntegrationTests(MeterApiFactory factory)
        => _client = factory.CreateClient();

    // ── Helper: get JWT by calling the real UMS (mocked inline) ──────────
    // Since UMS is a separate service, integration tests call the Meter API
    // directly with a pre-constructed JWT matching test config.
    // We simulate this by embedding valid credentials in test config.
    private void AuthorizeAsAdmin()
    {
        // The test appsettings key: "SE-MPS-MYS-CHANGE-ME-MINIMUM-32-CHARS!!"
        // We construct a valid JWT with the same key
        var token = BuildTestJwt("admin", "utility_admin");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static string BuildTestJwt(string username, string role)
    {
        var key   = "SE-MPS-MYS-CHANGE-ME-MINIMUM-32-CHARS!!";
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var sigKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(bytes);
        var creds  = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            sigKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role),
            new System.Security.Claims.Claim("displayName", username),
        };
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "SE-MPS-UMS", audience: "SE-MPS-Platform",
            claims: claims, expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetMeters_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/meters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMeters_WithValidToken_Returns200WithArray()
    {
        AuthorizeAsAdmin();
        var response = await _client.GetAsync("/api/v1/meters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task PostMeter_CreateAndRetrieve_FullRoundTrip()
    {
        AuthorizeAsAdmin();
        var serial = $"AU-TEST-{Guid.NewGuid():N}".Substring(0, 20);

        var dto = new MeterCreateDto
        {
            SerialNumber  = serial,
            Model         = "AURORA",
            MeterType     = MeterType.SinglePhaseSmart,
            AccuracyClass = "Class 1.0",
            VoltageRating = "240V (P-N)",
            CurrentRating = "5-30A",
            Standards     = "IS 16444 Part 1",
            CommunicationType = CommunicationType.RFMesh,
            Zone          = "MYSURU-NORTH",
            IsSmart       = true
        };

        // POST — create
        var createResp = await _client.PostAsJsonAsync("/api/v1/meters", dto);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        createResp.Headers.Location.Should().NotBeNull();

        var created = await createResp.Content.ReadFromJsonAsync<MeterResponseDto>();
        created.Should().NotBeNull();
        created!.SerialNumber.Should().Be(serial);
        created.Model.Should().Be("AURORA");
        created.Zone.Should().Be("MYSURU-NORTH");
        created.Status.Should().Be("Commissioned");

        // GET by ID
        var getResp = await _client.GetAsync($"/api/v1/meters/{created.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResp.Content.ReadFromJsonAsync<MeterResponseDto>();
        fetched!.Id.Should().Be(created.Id);
        fetched.SerialNumber.Should().Be(serial);
    }

    [Fact]
    public async Task PostMeter_DuplicateSerial_Returns409()
    {
        AuthorizeAsAdmin();
        var serial = $"DUPE-{Guid.NewGuid():N}".Substring(0, 15);
        var dto = new MeterCreateDto
        {
            SerialNumber  = serial, Model = "AURORA",
            MeterType     = MeterType.SinglePhaseSmart,
            AccuracyClass = "Class 1.0", VoltageRating = "240V (P-N)",
            CurrentRating = "5-30A", Standards = "IS 16444 Part 1"
        };

        var r1 = await _client.PostAsJsonAsync("/api/v1/meters", dto);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);

        var r2 = await _client.PostAsJsonAsync("/api/v1/meters", dto);
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetById_UnknownGuid_Returns404()
    {
        AuthorizeAsAdmin();
        var response = await _client.GetAsync($"/api/v1/meters/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMeter_Returns204_ThenGetReturns404()
    {
        AuthorizeAsAdmin();
        var serial = $"DEL-{Guid.NewGuid():N}".Substring(0, 15);
        var dto = new MeterCreateDto
        {
            SerialNumber  = serial, Model = "REGOR",
            MeterType     = MeterType.ThreePhaseWholeCurrent,
            AccuracyClass = "Class 1.0", VoltageRating = "3×240V (P-N)",
            CurrentRating = "10-60A", Standards = "IS 16444 Part 1"
        };

        var create = await _client.PostAsJsonAsync("/api/v1/meters", dto);
        var meter  = await create.Content.ReadFromJsonAsync<MeterResponseDto>();

        var del = await _client.DeleteAsync($"/api/v1/meters/{meter!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await _client.GetAsync($"/api/v1/meters/{meter.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task V2GetMeters_WithTypeFilter_ReturnsOnlyMatchingType()
    {
        AuthorizeAsAdmin();
        // Create one AURORA (SinglePhaseSmart=1) and one REGOR (ThreePhaseWholeCurrent=3)
        var s1 = $"FLT1-{Guid.NewGuid():N}".Substring(0, 15);
        var s2 = $"FLT2-{Guid.NewGuid():N}".Substring(0, 15);

        await _client.PostAsJsonAsync("/api/v1/meters", new MeterCreateDto
        {
            SerialNumber = s1, Model = "AURORA", MeterType = MeterType.SinglePhaseSmart,
            AccuracyClass = "Class 1.0", VoltageRating = "240V (P-N)",
            CurrentRating = "5-30A", Standards = "IS 16444 Part 1"
        });
        await _client.PostAsJsonAsync("/api/v1/meters", new MeterCreateDto
        {
            SerialNumber = s2, Model = "REGOR", MeterType = MeterType.ThreePhaseWholeCurrent,
            AccuracyClass = "Class 1.0", VoltageRating = "3×240V (P-N)",
            CurrentRating = "10-60A", Standards = "IS 16444 Part 1"
        });

        var resp = await _client.GetAsync("/api/v2/meters?type=1"); // SinglePhaseSmart
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var meters = await resp.Content.ReadFromJsonAsync<List<MeterResponseDto>>();
        meters.Should().NotBeNull();
        meters!.Should().OnlyContain(m => m.MeterType == "SinglePhaseSmart");
    }
}
