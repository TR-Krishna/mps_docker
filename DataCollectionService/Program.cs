// ═══════════════════════════════════════════════════════════════════════════════
// SE MPS Mysuru — AMI Platform
// DataCollectionService  |  Program.cs + Controller  (MOCK)
// ═══════════════════════════════════════════════════════════════════════════════
//
// ROLE: Simulates the EcoSEnter HES data collection engine.
// In production: receives DLMS/COSEM data from meters (via DCU/Gateway or
// cellular), validates it, and pushes to MDMS.
//
// DATA PROFILES (from SE EcoSEnter HES, slide 15):
//   Load Survey (LS)       — interval energy data (15/30/60 min)
//   Billing Profile        — end-of-month cumulative energy
//   Instantaneous Profile  — real-time voltage, current, power factor
//   Event/Tamper Log       — magnetic tamper, cover open, reverse energy, etc.
//
// MOCK: returns realistic-looking generated data — no real meter connection.
// PORT: 5012  |  GATEWAY PATH: /api/data/**
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
        Title = "Data Collection Service — SE MPS (Mock)",
        Version = "v1",
        Description = "MOCK: Simulates DLMS meter data profiles (Load Survey, Billing, Instantaneous). In production this is the EcoSEnter data acquisition engine."
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
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Data Collection Mock v1"); c.DocumentTitle = "Data Collection Mock"; });
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
