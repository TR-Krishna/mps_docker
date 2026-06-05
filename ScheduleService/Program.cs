// ═══════════════════════════════════════════════════════════════════════════════
// SE MPS Mysuru — AMI Platform
// ScheduleService  |  Program.cs + Controller  (MOCK)
// ═══════════════════════════════════════════════════════════════════════════════
//
// ROLE: Manages data collection schedules for the HES.
// In EcoSEnter, schedules define WHEN the HES polls meters for each data profile:
//   Load Survey    — typically every 15/30/60 min via automated collection
//   Billing        — on billing date (e.g. every 1st of month at midnight)
//   Instantaneous  — on-demand or periodic (every 4 hours for smart meters)
//   Tamper/Events  — pushed by meter (event-driven, not polled)
//
// PORT: 5014  |  GATEWAY PATH: /api/schedules/**
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
        Title = "Schedule Management Service — SE MPS (Mock)",
        Version = "v1",
        Description = "MOCK: Manages data collection schedules (Load Survey, Billing, Instantaneous) for the EcoSEnter HES."
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
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Schedule Service Mock v1"); c.DocumentTitle = "Schedule Service Mock"; });
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
