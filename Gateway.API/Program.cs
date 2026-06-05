// ═══════════════════════════════════════════════════════════════════════════════
// SE MPS Mysuru — AMI Platform
// Gateway.API  |  Program.cs
// ═══════════════════════════════════════════════════════════════════════════════
//
// PURPOSE
//   The single entry point for ALL traffic into the AMI platform.
//   Clients (HES front-end, MDMS connectors, field tools) always call port 5000.
//   This gateway enforces security and routes each request to the right service.
//
// WHAT THIS GATEWAY DOES
//   1. JWT Validation   — tokens are ISSUED by UserManagementService (UMS).
//                         This gateway only VALIDATES them (signature + expiry).
//                         It does NOT issue tokens. Correct separation of concerns.
//   2. Rate Limiting    — two policies: global (all traffic) + strict (auth endpoint).
//   3. Routing (YARP)   — path-prefix routing to 5 downstream services.
//   4. Header Forwarding— original Authorization header passed to all services.
//
// EVERYTHING CONFIGURABLE WITHOUT CODE CHANGES
//   All settings (ports, rate limits, JWT secret, routes) live in appsettings.json.
//   See appsettings.json for full documentation of every setting.
//
// REAL-WORLD CONTEXT (EcoSEnter HES)
//   In Schneider Electric's EcoSEnter, the gateway sits between external clients
//   and the internal microservice mesh. UMS is a dedicated service that manages
//   users and issues tokens. The gateway is intentionally thin — it validates
//   and routes, nothing more.
// ═══════════════════════════════════════════════════════════════════════════════

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ── 1. YARP — all routing defined in appsettings.json ─────────────────────────
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(cfg.GetSection("ReverseProxy"));

// ── 2. JWT Validation ─────────────────────────────────────────────────────────
var jwt = cfg.GetSection("Jwt");
var signingKey = jwt["Key"] ?? throw new InvalidOperationException(
    "Jwt:Key is missing. Set it in appsettings.json or via environment variable ASPNETCORE_Jwt__Key");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt["Issuer"]   ?? "SE-MPS-UMS",
            ValidAudience            = jwt["Audience"] ?? "SE-MPS-Platform",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew                = TimeSpan.FromSeconds(30)
        };
        // Return structured JSON on 401 instead of empty response
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsync(
                    "{\"status\":401,\"title\":\"Unauthorized\"," +
                    "\"detail\":\"A valid JWT Bearer token is required. " +
                    "Obtain one from POST /api/ums/auth/token\"}");
            }
        };
    });

builder.Services.AddAuthorization();

// ── 3. Rate Limiting — values from appsettings.json ───────────────────────────
var rlGlobal = cfg.GetSection("RateLimiting:Global");
var rlAuth   = cfg.GetSection("RateLimiting:Auth");

builder.Services.AddRateLimiter(options =>
{
    // Global policy: applies to all proxied routes
    options.AddSlidingWindowLimiter("gateway-global", o =>
    {
        o.PermitLimit          = rlGlobal.GetValue("PermitLimit",   200);
        o.Window               = TimeSpan.FromMinutes(rlGlobal.GetValue("WindowMinutes", 1));
        o.SegmentsPerWindow    = 6;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit           = rlGlobal.GetValue("QueueLimit", 20);
    });

    // Strict policy: auth endpoint only — anti brute-force
    options.AddSlidingWindowLimiter("auth-strict", o =>
    {
        o.PermitLimit          = rlAuth.GetValue("PermitLimit",   10);
        o.Window               = TimeSpan.FromMinutes(rlAuth.GetValue("WindowMinutes", 1));
        o.SegmentsPerWindow    = 6;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit           = rlAuth.GetValue("QueueLimit", 0);
    });

    options.RejectionStatusCode = 429;
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.ContentType = "application/problem+json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"status\":429,\"title\":\"Too Many Requests\"," +
            "\"detail\":\"Rate limit exceeded. Reduce request frequency.\"}", token);
    };
});

// ── 4. Swagger (gateway surface) ──────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "SE MPS AMI Platform — API Gateway",
        Version = "v1",
        Description =
            "**Schneider Electric MPS Mysuru — Advanced Metering Infrastructure Platform**\n\n" +
            "This gateway validates JWT tokens issued by UserManagementService and routes " +
            "traffic to the correct downstream microservice.\n\n" +
            "**Step 1:** POST `/api/ums/auth/token` with credentials → copy `token`\n\n" +
            "**Step 2:** Click **Authorize** above → paste token\n\n" +
            "| Path prefix | Routes to | Port |\n" +
            "|---|---|---|\n" +
            "| `/api/ums/**` | UserManagementService | 5010 |\n" +
            "| `/api/v1/meters/**` | MeterManagementService | 5011 |\n" +
            "| `/api/data/**` | DataCollectionService | 5012 |\n" +
            "| `/api/events/**` | EventService | 5013 |\n" +
            "| `/api/schedules/**` | ScheduleService | 5014 |"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "JWT issued by UserManagementService (POST /api/ums/auth/token)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SE MPS Gateway v1");
        c.DocumentTitle = "SE MPS AMI Platform";
    });
}

// Pipeline order is critical — do not reorder
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy().RequireRateLimiting("gateway-global");

app.Run();
