// ═══════════════════════════════════════════════════════════════════════════════
// SE MPS Mysuru — AMI Platform
// EventService  |  Program.cs + Controller  (MOCK)
// ═══════════════════════════════════════════════════════════════════════════════
//
// ROLE: Stores and serves meter events — tamper alerts, outages, system alarms.
//
// EVENT TYPES (from EcoSEnter HES, slide 15 — "Event management"):
//   Tamper events    : magnetic tamper, neutral disturbance, cover open,
//                      terminal block cover removal, reverse energy
//   Outage events    : power failure, power restoration, voltage sag/swell
//   System alerts    : battery low, clock fault, firmware mismatch
//   Billing events   : demand reset, billing date change
//   Communication    : session established/lost, authentication failure
//
// PORT: 5013  |  GATEWAY PATH: /api/events/**
// ═══════════════════════════════════════════════════════════════════════════════

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        Title = "Event Service — SE MPS (Mock)",
        Version = "v1",
        Description = "MOCK: Tamper alerts, outage events, and system alarms from smart meters."
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
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Event Service Mock v1"); c.DocumentTitle = "Event Service Mock"; });
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
