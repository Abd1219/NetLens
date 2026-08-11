# NetLens — System Architecture

> This document describes NetLens's technical architecture in detail.
> It is written to serve as a complete context reference for AI assistants, contributors, and code reviewers — readable without looking at the source code.

---

## Design Principles

NetLens follows **Clean Architecture** with these layers (inner to outer):

1. **Domain** — Pure core: entities, value objects, business rules. No external dependencies.
2. **Application** — Contracts (interfaces) and lightweight orchestration services.
3. **Infrastructure / Database** — Persistence implementations (EF Core + SQLite).
4. **Network** — Hardware and OS adapters (Win32 APIs via P/Invoke).
5. **Services** — Long-running background services.
6. **Reporting** — PDF report generation.
7. **UI** — WinUI 3 presentation (pure MVVM).

**Dependency Rule**: Inner layers never depend on outer layers. UI and services depend only on abstractions defined in Application.

---

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                      NetLens.UI (WinUI 3)                   │
│  Views / ViewModels / Converters / Styles                   │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│              NetLens.Application (Contracts)                │
│  IEventBus / ITelemetryCollector / ISessionRepository /    │
│  IReportGenerator / IRuleEngine / IPacketCapture           │
└──────┬──────────────┬────────────┬────────────┬────────────┘
       │              │            │            │
┌──────▼──────┐ ┌─────▼──────┐ ┌──▼──────┐ ┌──▼──────────┐
│NetLens.     │ │NetLens.    │ │NetLens. │ │NetLens.     │
│Network      │ │Database    │ │Services │ │Reporting    │
│(WiFi/       │ │(EF Core/   │ │(BG Svc) │ │(QuestPDF)   │
│Discovery/   │ │SQLite)     │ │         │ │             │
│Diagnostics) │ └────────────┘ └─────────┘ └─────────────┘
└─────────────┘
       │
       └──── all depend on ──────────────────────────────────┐
                                                             │
┌─────────────────────────────────────────────────────────────▼──┐
│                   NetLens.Domain                                │
│  Entities: WirelessSnapshot, DiagnosticSession, TimelineEvent  │
│  Value Objects: RSSI, PhyRate, Latency, Jitter, PacketLossRate │
│  Rules: IDiagnosticRule + 5 implementations                    │
│  Events: TelemetryCollectedEvent, CorrelationAlertEvent        │
└────────────────────────────────────────────────────────────────┘
```

---

## Module Reference

### NetLens.Domain

The system's heart. **Zero external dependencies.**

#### Entities (Aggregate Roots / Entities)

| Class | Type | Description |
|---|---|---|
| `DiagnosticSession` | Aggregate Root | Lifecycle of a diagnostic session. Coordinates snapshots and timeline. States: `Initializing → Monitoring → Ended` |
| `WirelessSnapshot` | Entity (sealed record) | Immutable frame of WiFi metrics captured at a point in time. Acts as the session's ledger entry |
| `TimelineEvent` | Entity | Event with timestamp, severity, and evidence. Added to the session's timeline |
| `DiscoveredDevice` | Entity | Device discovered on the subnet (ARP scan) |
| `CapturedPacket` | Entity | Captured network packet (currently stub) |
| `TracerouteHop` | Entity | Individual hop in a traceroute |

#### Value Objects (`Domain/Model/`)

All are **immutable** with constructor validation:

| Value Object | Range / Rule |
|---|---|
| `RSSI` | -100 to 0 dBm |
| `PhyRate` | ≥ 0 Mbps |
| `Channel` | 1–196 |
| `Frequency` | MHz (2400–6000) |
| `SignalQuality` | 0–100% derived from RSSI |
| `Latency` | 0–∞ ms, supports Timeout |
| `Jitter` | ≥ 0 ms |
| `PacketLossRate` | 0–100% |
| `Bandwidth` | ≥ 0 Mbps |
| `HealthScoreValue` | 0–100 |
| `MacAddress` | Format `XX:XX:XX:XX:XX:XX` |
| `IPAddressValue` | IPv4 string |

#### Diagnostic Rules (`Domain/Rules/`)

Interface: `IDiagnosticRule.Evaluate(WirelessSnapshot) → DiagnosticResult?`

| Rule | Code | Threshold | Severity |
|---|---|---|---|
| `LowRSSIRule` | `LOW_RSSI` | RSSI < -75 dBm | Warning / Critical (<-85) |
| `HighPacketLossRule` | `HIGH_PACKET_LOSS` | > 5% | Warning / Critical (>15%) |
| `GatewayLatencyRule` | `HIGH_GATEWAY_LATENCY` | > 100 ms | Warning / Critical (>300ms) |
| `DnsLatencyRule` | `HIGH_DNS_LATENCY` | > 150 ms | Warning / Critical (>400ms) |
| `HighJitterRule` | `HIGH_JITTER` | > 30 ms | Warning / Critical (>80ms) |

---

### NetLens.Application

Contains only interfaces (contracts) and simple application services.

| Interface | Responsibility |
|---|---|
| `IEventBus` | Pub/Sub decoupling. `PublishAsync<T>()` / `Subscribe<T>()` / `Unsubscribe<T>()` |
| `ITelemetryCollector` | `CaptureSnapshotAsync()` → `WirelessSnapshot?` |
| `ISessionRepository` | `SaveSessionAsync()`, `GetRecentSessionsAsync()`, `GetSessionByIdAsync()` |
| `IReportGenerator` | `GeneratePdfReport(DiagnosticSession)` → `byte[]` |
| `IRuleEngine` | `Evaluate(WirelessSnapshot)` → `IReadOnlyList<DiagnosticResult>` |
| `IPacketCapture` | `StartCapture()` / `StopCapture()` — currently NullObject |

**Services in Application:**
- `EventBus`: Singleton implementation with `ConcurrentDictionary<Type, List<Delegate>>` of handlers per event type. Thread-safe.
- `RuleEngine`: Runs all registered `IDiagnosticRule` instances against a snapshot.
- `CorrelationEngine` (stub in Application — real implementation is in `NetLens.Services`).

---

### NetLens.Network

Network implementations that depend on Windows OS APIs.

#### WiFi (`Wifi/`)
- **`WlanApi.cs`**: Direct P/Invoke against `wlanapi.dll`. Exposes `WlanOpenHandle`, `WlanEnumInterfaces`, `WlanQueryInterface`, `WlanFreeMemory`. Maps native structs (`WLAN_CONNECTION_ATTRIBUTES`, `WLAN_INTERFACE_INFO_LIST`, etc.)
- **`WifiTelemetryCollector.cs`**: Implements `ITelemetryCollector`. Combines WlanAPI + IP Helper API + PingService to build a complete `WirelessSnapshot`. Runs 3 pings in parallel (gateway, DNS, internet).

#### Adapters (`Adapters/`)
- **`SystemMetricsCollector.cs`**: Reads CPU (PerformanceCounter) and RAM (GlobalMemoryStatusEx P/Invoke).

#### Discovery (`Discovery/`)
- **`SubnetScanner.cs`**: Scans subnet by CIDR range, pinging each host.
- **`ArpResolver.cs`**: Resolves MAC addresses via the system ARP table (SendARP P/Invoke).
- **`HostnameResolver.cs`**: Reverse DNS resolution of discovered IPs.

#### Diagnostics (`Diagnostics/`)
- **`PingService.cs`**: Sends N pings and returns `PingResult` with average latency, jitter, and packet loss.
- **`TracerouteService.cs`**: Implements manual traceroute with incremental TTL.

#### PacketCapture (`PacketCapture/`)
- **`NullPacketCapture.cs`**: Empty implementation (NullObject pattern). Placeholder until Npcap is integrated.

---

### NetLens.Database

Persistence with **Entity Framework Core 9 + SQLite**.

- **`NetLensDbContext`**: DbSets for `DiagnosticSessionRecord`, `WirelessSnapshotRecord`, `TimelineEventRecord`.
- **DB Entities**: Separate from domain entities. Use `DateTime` (UTC) instead of `DateTimeOffset` due to SQLite's ORDER BY limitation.

> ⚠️ **Technical decision**: The domain uses `DateTimeOffset` internally. The repository converts to/from `DateTime UTC` at the persistence boundary. See `REPORTS/DateTimeOffset_to_DateTime_Migration_Report.md`.

---

### NetLens.Infrastructure

- **`SessionRepository.cs`**: Implements `ISessionRepository`. Saves and retrieves `DiagnosticSession` from SQLite, mapping between domain and DB entities. Converts `DateTimeOffset` ↔ `DateTime UTC` on read/write.

---

### NetLens.Services

Long-running background services for the application's lifetime.

- **`TelemetryBackgroundService`** (`BackgroundService`): Loop that captures a `WirelessSnapshot` every **3 seconds**, records it in the active `DiagnosticSession`, and publishes `TelemetryCollectedEvent` on the `IEventBus`.
- **`CorrelationEngine`** (`BackgroundService + IEventHandler<TelemetryCollectedEvent>`): Maintains a 5-minute sliding window of snapshots. Detects:
  - **Roaming Flap**: > 3 BSSID changes in 60 seconds
  - **Gateway Failover**: Change in gateway IP address

---

### NetLens.Reporting

- **`DiagnosticReportGenerator`**: Implements `IReportGenerator`. Generates PDF using **QuestPDF** (Community License). Report includes: session metadata, most recent network status, timeline event table with evidence.

---

### NetLens.UI

**WinUI 3** presentation layer with pure **MVVM** using CommunityToolkit.Mvvm.

#### Views (`Views/`)
| View | ViewModel | Description |
|---|---|---|
| `DashboardPage` | `DashboardViewModel` | Real-time metrics + 3 LiveCharts2 graphs (RSSI, Latency, Packet Loss) |
| `WifiExplorerPage` | `WifiExplorerViewModel` | Connected AP info + neighboring networks table |
| `DiagnosticsPage` | `DiagnosticsViewModel` | Manual scan, Health Score, alert list |
| `DiscoveryPage` | `DiscoveryViewModel` | Subnet scan, device table |
| `HistoryPage` | `HistoryViewModel` | Past sessions list + PDF export |
| `SettingsPage` | `SettingsViewModel` | Language selector (English / Spanish) |

#### ViewModels
- All inherit `ObservableObject` (CommunityToolkit.Mvvm)
- Those receiving telemetry implement `IEventHandler<TelemetryCollectedEvent>` and subscribe to `IEventBus`
- UI updates are always dispatched to the main thread via `DispatcherQueue.TryEnqueue()`

#### UI-Layer Services (`Services/`)
- **`SettingsService`**: Reads/writes `netlens.settings.json`. Persists user preferences (language).
- **`LocalizationService`**: Loads `.resx` resource files at runtime; exposes `GetString(key)` for UI text. Language can be changed at runtime without restarting the app.

#### DI Composition (`App.xaml.cs`)
`App.xaml.cs` acts as the **Composition Root** using `Microsoft.Extensions.Hosting`. Registers all services, starts the Host, and creates the main window. SQLite DB is initialized via `EnsureDatabaseSchemaAsync()`: checks `PRAGMA user_version` and recreates the schema automatically if the version changed (currently v2).

---

## Event Flow

```
TelemetryBackgroundService
    │
    ├─ CaptureSnapshotAsync()  →  WifiTelemetryCollector
    │                               ├─ WlanAPI (RSSI, PHY Rate, SSID, BSSID)
    │                               ├─ IP Helper (Gateway, DNS, Local IP, MAC)
    │                               ├─ PingService x3 (latency, jitter, packet loss)
    │                               └─ SystemMetrics (CPU, RAM)
    │
    └─ PublishAsync(TelemetryCollectedEvent)
            │
            ├─► DashboardViewModel.HandleAsync()
            │       └─ RuleEngine.Evaluate() → alerts
            │       └─ DispatcherQueue → UpdateFromSnapshot() + charts
            │
            ├─► WifiExplorerViewModel.HandleAsync()
            │       └─ Updates SSID/BSSID/RSSI/Channel
            │
            └─► CorrelationEngine.HandleAsync()
                    ├─ Roaming Flap → PublishAsync(CorrelationAlertEvent)
                    └─ Gateway Failover → PublishAsync(CorrelationAlertEvent)
```

---

## Design Patterns

| Pattern | Where | Why |
|---|---|---|
| **NullObject** | `NullPacketCapture` | Compile/run without Npcap; easy future swap |
| **Aggregate Root** | `DiagnosticSession` | Controls the snapshot ledger lifecycle |
| **Immutable Value Objects** | All `Domain/Model/` | Type safety, no primitive obsession |
| **Event Bus Pub/Sub** | `EventBus` | Decouples telemetry from UI; multiple subscribers without direct dependencies |
| **Composition Root** | `App.xaml.cs` | Single place where the entire DI container is wired |
| **MVVM with Source Generators** | ViewModels | `[ObservableProperty]` eliminates `INotifyPropertyChanged` boilerplate |
| **Background Service loop** | `TelemetryBackgroundService` | Native integration with `IHostedService` and shutdown `CancellationToken` |

---

## Known Architectural Pending Items

- [x] `CorrelationEngine` in `NetLens.Application/Services/CorrelationEngine.cs` stub removed — real implementation clean in `NetLens.Services/`.
- [x] Neighboring networks in `WifiExplorerViewModel` use real BSS scan via native WlanAPI (`WlanGetNetworkBssList`) with simulated fallback when unavailable.
- [x] Channel/frequency are derived exactly via `WlanGetNetworkBssList` center frequencies in kHz converted to 2.4/5/6 GHz channels.
- [x] `tests/NetLens.Tests/` contains 58 passing unit tests covering Domain, Rules, EventBus, and WlanAPI channel math.
- [ ] `IPacketCapture` / `NullPacketCapture` — pending Npcap integration (SharpPcap or PacketDotNet).
- [ ] Localization currently covers the shell UI (sidebar, header, status texts). Full translation of all views requires extending resource keys and updating each page to listen for language-change events.
