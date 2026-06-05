// ═══════════════════════════════════════════════════════════════════════════
// UserManagementService/Controllers/AuthController.cs
// ═══════════════════════════════════════════════════════════════════════════

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace UserManagementService.Controllers;

[ApiController]
[Route("api/ums/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<AuthController> _log;

    public AuthController(IConfiguration cfg, ILogger<AuthController> log)
    { _cfg = cfg; _log = log; }

    /// <summary>
    /// Issue a JWT Bearer token.
    /// This is the ONLY public endpoint in the platform — no token needed to call it.
    /// All other endpoints require the token returned here.
    ///
    /// Demo credentials (from appsettings.json MockUsers):
    ///   admin / Admin@123           → utility_admin
    ///   engineer01 / Engineer@123   → field_engineer
    ///   analyst01 / Analyst@123     → data_analyst
    ///   viewer01 / Viewer@123       → viewer
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), 200)]
    [ProducesResponseType(401)]
    public IActionResult Token([FromBody] LoginRequest req)
    {
        var users = _cfg.GetSection("MockUsers").Get<List<MockUser>>() ?? new();
        var user  = users.FirstOrDefault(u => u.Username == req.Username && u.Password == req.Password);

        if (user is null)
        {
            _log.LogWarning("Failed login: {User}", req.Username);
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var jwtSection = _cfg.GetSection("Jwt");
        var key        = jwtSection["Key"]      ?? throw new InvalidOperationException("Jwt:Key missing");
        var issuer     = jwtSection["Issuer"]   ?? "SE-MPS-UMS";
        var audience   = jwtSection["Audience"] ?? "SE-MPS-Platform";
        var expMins    = jwtSection.GetValue("ExpiryMinutes", 60);

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("userId",       user.Id),
            new Claim("displayName",  user.DisplayName),
            new Claim("organisation", user.Organisation),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: DateTime.UtcNow.AddMinutes(expMins),
            signingCredentials: creds);

        _log.LogInformation("Token issued: {User} ({Role})", user.Username, user.Role);
        return Ok(new TokenResponse
        {
            Token       = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType   = "Bearer",
            ExpiresIn   = expMins * 60,
            Role        = user.Role,
            DisplayName = user.DisplayName
        });
    }
}

// ── Models ────────────────────────────────────────────────────────────────────

public class LoginRequest
{
    /// <example>admin</example>
    public required string Username { get; set; }
    /// <example>Admin@123</example>
    public required string Password { get; set; }
}

public class TokenResponse
{
    public string Token       { get; set; } = "";
    public string TokenType   { get; set; } = "Bearer";
    public int    ExpiresIn   { get; set; }
    public string Role        { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class MockUser
{
    public string Id           { get; set; } = "";
    public string Username     { get; set; } = "";
    public string Password     { get; set; } = "";
    public string Role         { get; set; } = "";
    public string DisplayName  { get; set; } = "";
    public string Organisation { get; set; } = "";
}