// ═══════════════════════════════════════════════════════════════════════════════
// SE MPS Mysuru — AMI Platform
// UserManagementService  |  Program.cs  (MOCK)
// ═══════════════════════════════════════════════════════════════════════════════
//
// ROLE IN THE SYSTEM
//   The sole JWT issuer for the platform. Clients POST credentials here;
//   UMS validates them and returns a signed JWT that the Gateway will accept.
//
// THIS IS A MOCK
//   Users are loaded from appsettings.json MockUsers array.
//   A production UMS has a Users table, BCrypt password hashing, refresh tokens,
//   MFA, and password reset flows. All of that is intentionally out of scope
//   for this internship project — the interface (issue JWT) is identical.
//
// PORT: 5010
// ACCESSED VIA GATEWAY: /api/ums/**
// ═══════════════════════════════════════════════════════════════════════════════

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer    = jwt["Issuer"]   ?? "SE-MPS-UMS",
        ValidAudience  = jwt["Audience"] ?? "SE-MPS-Platform",
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing")))
    });
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "User Management Service — SE MPS (Mock)",
        Version = "v1",
        Description = "MOCK: Manages users and issues JWT tokens. In production this is the real EcoSEnter UMS."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "UMS Mock v1"); c.DocumentTitle = "UMS Mock"; });
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();