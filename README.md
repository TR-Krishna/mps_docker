# SE MPS Mysuru — AMI Platform
## API Gateway & Microservices (Internship Project)

> **Schneider Electric MPS Mysuru · EcoSEnter HES Platform**
> .NET 8 · YARP · PostgreSQL · JWT · In-Memory Cache · xUnit · Moq

---
d

## What This Project Is

You are an intern at **Schneider Electric MPS Mysuru**, working on the **EcoSEnter** Head-End System (HES) — a production AMI (Advanced Metering Infrastructure) platform used by utilities to manage smart electricity meters across a city or region.

This project builds the **API Gateway layer** and a set of **mock microservices** that mimic the real EcoSEnter services you cannot access as an intern. The architecture you build here is the real architecture used in production — only the data is fake.

### The Real System (from the Product Catalogue)

```
Smart Meters (AURORA/REGOR/ORION)
  ? RF Mesh / PLC ? DCU/Gateway ? WAN
  or
  ? 4G/NB-IoT SIM ? Cellular Tower ? WAN
                                        
                                        
                              EcoSEnter Head-End System (HES)
                              [collects DLMS data, manages meters,
                               processes tamper/alarms, pushes to MDMS]
                                        ?
                              Meter Data Management System (MDMS)
                              [validates, stores, prepares for billing]
                                        ?
                              Billing System ? Consumer Portal / Mobile App
```

### What You Built

```
All clients
    ?  port 5000
???????????????????????????????????????????????????????????
?  Gateway.API                                            ?
?  JWT validation (from UMS) · Rate limiting · YARP proxy ?
???????????????????????????????????????????????????????????
   ?          ?          ?          ?          ?
5010        5011        5012       5013       5014
UMS         Meters      Data       Events    Schedules
(issues     (AURORA     (Load      (Tamper   (Collection
 JWTs)      REGOR etc)  Survey,    Outage,    schedules)
            [real DB]   Billing,   Alarms)
                        Instant.)
```

**Key architectural point from the meeting:** UMS is a separate service that *issues* JWTs. The gateway only *validates* them. This is the real EcoSEnter pattern.

---

## Service Map

| Service | Port | Role | DB | Mock? |
|---|---|---|---|---|
| Gateway.API | 5000 | JWT validation, routing, rate limiting | None | No — this is real |
| UserManagementService | 5010 | Issues JWTs, manages users | None (appsettings) | Yes |
| MeterManagementService | 5011 | Smart meter CRUD + relay control | PostgreSQL | Partially — real DB, fake meters |
| DataCollectionService | 5012 | Load survey, billing, instantaneous profiles | None (generated) | Yes |
| EventService | 5013 | Tamper, outage, system alarms | None (in-memory) | Yes |
| ScheduleService | 5014 | Data collection schedules | None (in-memory) | Yes |

---

## Prerequisites (No Docker Required)

| Tool | Version | Download |
|---|---|---|
| .NET 8 SDK | 8.x | https://dotnet.microsoft.com/download/dotnet/8 |
| PostgreSQL | 14+ | https://www.postgresql.org/download/ |
| Visual Studio 2022 | 17.x (Community free) | https://visualstudio.microsoft.com/ |

### Verify

```bash
dotnet --version   # must show 8.x.x
psql --version     # must show 14.x or higher
```

### PostgreSQL Setup

After installing PostgreSQL, create the meter database:

```sql
-- Run in psql, pgAdmin, or DBeaver
CREATE DATABASE "SE_MPS_MeterDb";
```

Update the password in `MeterManagementService/appsettings.json`:
```json
"ConnectionStrings": {
  "MeterDb": "Host=localhost;Port=5432;Database=SE_MPS_MeterDb;Username=postgres;Password=YOUR_PASSWORD"
}
```

---

## Quick Start

### Step 1 — Restore packages

```bash
cd src
dotnet restore SE_MPS_AMI_Platform.sln
```

### Step 2 — Apply database migrations

```bash
cd MeterManagementService
dotnet ef database update
# Creates the Meters table in SE_MPS_MeterDb
# If dotnet ef not installed: dotnet tool install -g dotnet-ef
```

### Step 3 — Start all services

Open 5 terminals (or use Visual Studio Multiple Startup Projects):

```bash
# Terminal 1 — UserManagementService
cd src/UserManagementService && dotnet run

# Terminal 2 — MeterManagementService
cd src/MeterManagementService && dotnet run

# Terminal 3 — DataCollectionService
cd src/DataCollectionService && dotnet run

# Terminal 4 — EventService
cd src/EventService && dotnet run

# Terminal 5 — ScheduleService
cd src/ScheduleService && dotnet run

# Terminal 6 — Gateway (start this last)
cd src/Gateway.API && dotnet run
```

### Visual Studio — Start All At Once

1. Right-click Solution ? **Set Startup Projects**
2. Select **Multiple startup projects**
3. Set all 6 projects to **Start**
4. Press **F5**

### Step 4 — Get a token

```bash
curl -X POST http://localhost:5000/api/ums/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}'
```

Copy the `token` value.

### Step 5 — Call the API through the gateway

```bash
# List meters
curl http://localhost:5000/api/v1/meters \
  -H "Authorization: Bearer YOUR_TOKEN"

# Onboard an AURORA meter
curl -X POST http://localhost:5000/api/v1/meters \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "AU240001",
    "model": "AURORA",
    "meterType": 1,
    "accuracyClass": "Class 1.0",
    "voltageRating": "240V (P-N)",
    "currentRating": "5-30A",
    "standards": "IS 16444 Part 1, IS 15959",
    "communicationType": 1,
    "zone": "MYSURU-NORTH"
  }'
```

---

## API Reference

### Authentication

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/ums/auth/token` | None | Get JWT |

**Credentials:**

| Username | Password | Role | Access |
|---|---|---|---|
| admin | Admin@123 | utility_admin | Full access |
| engineer01 | Engineer@123 | field_engineer | Read + configure meters |
| analyst01 | Analyst@123 | data_analyst | Read-only data + events |
| viewer01 | Viewer@123 | viewer | Read-only |

---

### Meter Management — V1 (`/api/v1/meters`)

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | `/api/v1/meters` | All | List all meters (cached 60s) |
| GET | `/api/v1/meters/{id}` | All | Get by system GUID |
| GET | `/api/v1/meters/serial/{sn}` | All | Get by physical serial number |
| POST | `/api/v1/meters` | admin, engineer | Onboard new meter |
| PUT | `/api/v1/meters/{id}` | admin, engineer | Update meter config |
| DELETE | `/api/v1/meters/{id}` | admin only | Remove meter |
| POST | `/api/v1/meters/{id}/relay` | admin, engineer | Remote connect/disconnect |

### Meter Management — V2 (`/api/v2/meters`)

Same as V1 plus query filters on GET:

| Parameter | Values | Example |
|---|---|---|
| `type` | 1=SinglePhaseSmart, 3=ThreePhaseWholeCurrent, 5=LTCT, 6=HTCT, 7=ThreadThrough | `?type=1` |
| `status` | 1=Commissioned, 2=Pending, 3=Faulty | `?status=1` |
| `commType` | 1=RFMesh, 2=Cellular4G, 3=NBIoT | `?commType=1` |
| `zone` | Zone string | `?zone=MYSURU-NORTH` |
| `substationId` | Substation ID | `?substationId=SS-HEBBAL` |

### Meter Model Fields

```json
{
  "serialNumber":           "AU240001",        // physical serial, unique
  "model":                  "AURORA",          // AURORA/REGOR/ORION/TAURUS/ATRIA/ER300P
  "meterType":              1,                 // see enum below
  "accuracyClass":          "Class 1.0",
  "voltageRating":          "240V (P-N)",
  "currentRating":          "5-30A",
  "standards":              "IS 16444 Part 1, IS 15959",
  "communicationType":      1,                 // 1=RFMesh, 2=Cellular4G, 3=NBIoT
  "isSmart":                true,
  "isPrepaid":              false,
  "dcuId":                  "DCU-MYS-001",    // RF Mesh meters only
  "simNumber":              null,              // cellular meters only
  "dlmsLogicalDeviceName":  "SAG0012345678",  // DLMS/COSEM addressing
  "consumerNumber":         "KESCO-001234",
  "installationAddress":    "12, MG Road, Mysuru",
  "latitude":               12.2958,
  "longitude":              76.6394,
  "zone":                   "MYSURU-NORTH",
  "substationId":           "SS-HEBBAL",
  "firmwareVersion":        "2.1.4-AURORA",
  "todEnabled":             false,
  "notes":                  "Phase 1 rollout"
}
```

**MeterType enum:**

| Value | Name | Product | Standard |
|---|---|---|---|
| 1 | SinglePhaseSmart | AURORA | IS 16444 Pt 1 |
| 2 | SinglePhasePrepaid | TAURUS | IS 15884 |
| 3 | ThreePhaseWholeCurrent | REGOR | IS 16444 Pt 1 |
| 4 | ThreePhasePrepaid | ATRIA | IS 15884 |
| 5 | ThreePhaseLTCT | REGOR LTCT | IS 16444 Pt 2 |
| 6 | ThreePhaseHTCT | ER300P HTCT | IS 14697 |
| 7 | ThreadThrough | ORION | — |
| 8 | ThreePhaseDigital | ER300P | IS 13779 |

---

### Data Collection (`/api/data`)

| Method | Path | Description |
|---|---|---|
| GET | `/api/data/load-survey/{sn}?from=&to=` | 15-min interval energy data |
| GET | `/api/data/billing/{sn}?months=3` | Monthly billing profiles |
| GET | `/api/data/instantaneous/{sn}` | Real-time voltage, current, PF |
| POST | `/api/data/trigger-read/{sn}` | Trigger on-demand read |

### Events (`/api/events`)

| Method | Path | Description |
|---|---|---|
| GET | `/api/events` | All events (filterable) |
| GET | `/api/events/alarms/active` | Unacknowledged critical+high alarms |
| POST | `/api/events/{id}/acknowledge` | Acknowledge an event |

### Schedules (`/api/schedules`)

| Method | Path | Description |
|---|---|---|
| GET | `/api/schedules` | List all schedules |
| POST | `/api/schedules` | Create a schedule |
| PATCH | `/api/schedules/{id}/toggle` | Enable/disable a schedule |

---

## Running Tests

```bash
cd tests/MeterManagement.Tests
dotnet test --verbosity normal
```

**Expected:** 11 tests — 11 passed, 0 failed.

Unit tests (8): business logic only, no DB, no HTTP.
Integration tests (7): real in-memory HTTP server, real middleware, in-memory SQLite DB.

---

## Configuration Reference

### The One Rule: Everything in appsettings.json

No code changes are needed to:
- Add a new microservice route ? edit `Gateway.API/appsettings.json` ReverseProxy section
- Change rate limits ? edit `RateLimiting` section
- Rotate JWT secret ? change `Jwt.Key` (must match in Gateway + UMS + all services)
- Add a new user ? edit `UserManagementService/appsettings.json` MockUsers array
- Change DB password ? edit `MeterManagementService/appsettings.json` ConnectionStrings

### Secrets Management

**Development (local):**
```bash
cd MeterManagementService
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "your-real-key-here"
dotnet user-secrets set "ConnectionStrings:MeterDb" "Host=localhost;..."
```

**Production:**
```bash
export ASPNETCORE_Jwt__Key="your-production-key"
export ASPNETCORE_ConnectionStrings__MeterDb="Host=prod-server;..."
```

### JWT Key Requirements
- Minimum 32 characters
- Must be **identical** in all 6 services
- Generate: `openssl rand -base64 32`

---

## Adding a New Microservice (No Code Changes)

When a new microservice exists (e.g., MDMSService on port 5015):

1. Add to `Gateway.API/appsettings.json`:
```json
"Routes": {
  "mdms": {
    "ClusterId": "mdms",
    "AuthorizationPolicy": "default",
    "Match": { "Path": "/api/mdms/{**catch-all}" },
    "Transforms": [{ "RequestHeadersCopy": "true" }]
  }
},
"Clusters": {
  "mdms": { "Destinations": { "d1": { "Address": "http://localhost:5015" } } }
}
```

2. Restart Gateway.API.

That is all. No code changes anywhere.

---

## Migrating to Docker (When Available)

### Redis Cache (one line change)

In `MeterManagementService/Program.cs`, change:
```csharp
builder.Services.AddDistributedMemoryCache();
```
to:
```csharp
builder.Services.AddStackExchangeRedisCache(o => {
    o.Configuration  = "localhost:6379";
    o.InstanceName   = "SE-MPS:";
});
```

Add NuGet package: `Microsoft.Extensions.Caching.StackExchangeRedis`

### Docker Compose (when ready)

When Docker is available, a `docker-compose.yml` can start PostgreSQL and Redis alongside all services. The connection strings in appsettings.json change from `localhost` to the container service names. No other changes.

---

## Known Limitations (Out of Scope for Internship)

| Feature | Notes |
|---|---|
| Real DLMS/COSEM protocol | Actual meter communication via DCU — handled by EcoSEnter |
| Redis caching | One-line swap when Docker available |
| Real user database | MockUsers in appsettings — sufficient for this project |
| Refresh tokens | 60-min expiry, re-login acceptable |
| HTTPS/TLS | HTTP for local dev; cert needed for production |
| Message queue (RabbitMQ) | Event-driven notifications between services |
| Pagination | No large dataset expected at this stage |
