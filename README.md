# NetLens — Network Diagnostic Tool for Windows

> **Real-time visibility of your WiFi network.**
> Windows desktop application (WinUI 3 / .NET 10) for continuous monitoring, diagnostics, and reporting of wireless network quality.

---

## What is NetLens?

NetLens is a **professional WiFi viewer for PC** designed for network technicians and IT professionals. It captures wireless connection metrics every **3 seconds** via the Windows WLAN API, analyzes them in real time with a rule engine, and emits alerts and exportable PDF reports.

The project is an **active-development functional prototype** built with Clean Architecture and DDD (Domain-Driven Design).

### For AI Assistants

If you are an AI reading this to understand or extend the codebase:
- **Entry point**: `src/NetLens.UI/App.xaml.cs` — Composition Root (DI wiring, DB init, Host startup)
- **Core data type**: `WirelessSnapshot` in `src/NetLens.Domain/Entities/WirelessSnapshot.cs` — the immutable telemetry frame captured every 3 seconds
- **Main telemetry loop**: `src/NetLens.Services/TelemetryBackgroundService.cs` — captures snapshots and publishes `TelemetryCollectedEvent`
- **Rule evaluation**: `src/NetLens.Application/Services/RuleEngine.cs` — runs all `IDiagnosticRule` implementations against each snapshot
- **Architecture doc**: [`ARCHITECTURE.md`](./ARCHITECTURE.md) — full layer-by-layer breakdown, patterns, and known pending items
- **Progress log**: [`PROGRESS.md`](./PROGRESS.md) — feature history and pending tasks

---

## Features

| Module | Status | Description |
|---|---|---|
| **Real-time Dashboard** | ✅ Working | RSSI, PHY Rate, Latency, Jitter, Packet Loss, CPU/RAM |
| **Diagnostic Rule Engine** | ✅ Working | 5 rules: LowRSSI, HighPacketLoss, GatewayLatency, DnsLatency, HighJitter |
| **Correlation Engine** | ✅ Working | Roaming Flap and Gateway Failover detection |
| **WiFi Explorer** | ✅ Working | Connected AP info + simulated neighboring networks |
| **Manual Diagnostics** | ✅ Working | On-demand scan with Health Score |
| **Network Discovery** | ✅ Working | ARP subnet scan + reverse DNS resolution |
| **Session History** | ✅ Working | SQLite / EF Core — last 50 sessions |
| **PDF Export** | ✅ Working | Reports with QuestPDF (Community License) |
| **Language Selector** | ✅ Working | English / Spanish via .resx resources; persisted to `netlens.settings.json` |
| **Packet Capture** | 🚧 Pending | `NullPacketCapture` stub; Npcap not yet integrated |

---

## Tech Stack

| Layer | Technology |
|---|---|
| **UI / Framework** | WinUI 3 (Windows App SDK 1.6) |
| **Language** | C# 13 / .NET 10 |
| **MVVM** | CommunityToolkit.Mvvm 8.3.2 |
| **Charts** | LiveChartsCore.SkiaSharpView.WinUI 2.0.0-rc3 |
| **DI / Hosting** | Microsoft.Extensions.Hosting 9.0 |
| **Database** | SQLite + Entity Framework Core 9 |
| **PDF** | QuestPDF (Community License) |
| **System APIs** | WlanAPI (P/Invoke), IP Helper API, PerformanceCounter |

---

## System Requirements

- **OS**: Windows 10 1903 (build 19041) or later
- **Runtime**: Windows App SDK 1.6 (self-contained)
- **Architectures**: x86, x64, ARM64
- **Permissions**: No elevation required for WiFi metrics; ARP scan may require network permissions

---

## Repository Structure

```
VisorWifiForPc/
├── NetLens.sln                     # Solution file
├── README.md                       # This file
├── ARCHITECTURE.md                 # Detailed architecture reference
├── PROGRESS.md                     # Development log and feature history
├── REPORTS/                        # Technical decision reports
│   └── DateTimeOffset_to_DateTime_Migration_Report.md
├── src/
│   ├── NetLens.Domain/             # Domain core: Entities, Value Objects, Rules
│   ├── NetLens.Application/        # Contracts (interfaces) and lightweight services
│   ├── NetLens.Network/            # Network adapters (WiFi, Discovery, Diagnostics)
│   ├── NetLens.Infrastructure/     # Repositories (EF Core / SQLite)
│   ├── NetLens.Database/           # DbContext and database entities
│   ├── NetLens.Services/           # Background services (Telemetry, Correlation)
│   ├── NetLens.Reporting/          # PDF report generation (QuestPDF)
│   └── NetLens.UI/                 # WinUI 3 presentation layer (MVVM)
│       ├── Resources/              # Localization .resx files (en/es)
│       ├── Services/               # UI-layer services (SettingsService, LocalizationService)
│       └── Views/                  # Pages: Dashboard, WiFi Explorer, Diagnostics, Discovery, History, Settings
└── tests/
    └── NetLens.Tests/              # Unit tests (pending — directory is empty)
```

---

## Build & Run

```powershell
# Restore dependencies
dotnet restore NetLens.sln

# Build in debug mode
dotnet build NetLens.sln -c Debug

# Run the UI application
dotnet run --project src/NetLens.UI/NetLens.UI.csproj
```

> **Note**: First run automatically creates `netlens.db` in the execution directory. If the schema changes, the DB is recreated automatically (versioned via `PRAGMA user_version`, currently v2). Settings are persisted to `netlens.settings.json`.

---

## Data Flow Summary

```
[WlanAPI / IP Helper / PingService]
          ↓  every 3 seconds
[WifiTelemetryCollector] → WirelessSnapshot
          ↓
[TelemetryBackgroundService] → publishes TelemetryCollectedEvent
          ↓                           ↓
[CorrelationEngine]          [DashboardViewModel]
 (Roaming Flap,               (real-time UI,
  Gateway Failover)            LiveCharts2 graphs)
          ↓
   [IEventBus] → other subscribers
```

---

## Third-Party Licenses

| Library | License |
|---|---|
| CommunityToolkit.Mvvm | MIT |
| LiveChartsCore | MIT |
| QuestPDF | Community (free for individuals/SMBs) |
| Microsoft.WindowsAppSDK | MIT |
| Entity Framework Core | Apache 2.0 |

---

## Localization

NetLens includes a language selector in the Settings page. Currently available in **English** and **Spanish**. Language choice is persisted in `netlens.settings.json` in the execution directory. The localization system uses `.resx` resource files (`Resources.resx` for English, `Resources.es.resx` for Spanish) managed by `LocalizationService`.

---

*Active prototype. See [PROGRESS.md](./PROGRESS.md) for the development log and [ARCHITECTURE.md](./ARCHITECTURE.md) for architecture details.*
