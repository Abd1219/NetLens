# NetLens — Development Progress Log

> This file is the **official progress log** for the NetLens project.
> Each entry documents what was implemented, decisions made, and the resulting state.
> Update this file at the end of each development session or significant milestone.
>
> **For AI assistants**: Read this file to understand what features exist, what bugs have been fixed, and what is still pending. Entries are in reverse-chronological order within sections.

---

## Current Project State

**Version**: v0.6 (Active prototype)
**Phase**: Active development
**Last updated**: 2026-08-04 (documentation + localization commit)

### Component Summary

| Component | Status | Notes |
|---|---|---|
| Domain (Entities + Value Objects) | ✅ Complete | Immutable, constructor validation |
| Rule Engine (5 rules) | ✅ Complete | LowRSSI, HighPacketLoss, GatewayLatency, DnsLatency, HighJitter |
| Correlation Engine | ✅ Complete | Roaming Flap + Gateway Failover |
| WlanAPI (P/Invoke) | ✅ Complete | RSSI, PHY Rate, SSID, BSSID, PHY Type |
| Combined telemetry | ✅ Complete | WiFi + IP Helper + PingService + SystemMetrics |
| Background Services | ✅ Complete | TelemetryBackgroundService + CorrelationEngine |
| Event Bus | ✅ Complete | Decoupled Pub/Sub, thread-safe |
| Dashboard UI | ✅ Complete | LiveCharts2 graphs, real-time metrics |
| WiFi Explorer UI | ✅ Complete | Simulated neighboring networks |
| Manual Diagnostics | ✅ Complete | Health Score calculated |
| Network Discovery | ✅ Complete | ARP + reverse DNS |
| Session History | ✅ Complete | SQLite, last 50 sessions; DateTimeOffset ORDER BY fix applied |
| PDF Export | ✅ Complete | QuestPDF, Community License |
| Localization (en/es) | ✅ Complete | Shell UI localized; full view translation pending |
| Settings page | ✅ Complete | Language selector, persisted to `netlens.settings.json` |
| Packet capture | ❌ Pending | NullPacketCapture stub |
| Real neighboring networks | ❌ Pending | Currently simulated data |
| Unit tests | ❌ Pending | `tests/` directory empty |
| Exact channel/frequency | ⚠️ Partial | Heuristic from PHY type, not exact WlanAPI opcode |

---

## Change History

### [2026-08-04] — Documentation refresh + GitHub push

**Type**: Documentation
**Who**: Antigravity AI Assistant

**What was done:**
- Rewrote `README.md` in English with AI-friendly orientation section, updated feature table (added localization/settings), corrected repository structure to include `Resources/` and `Services/` dirs
- Rewrote `ARCHITECTURE.md` in English with complete module reference, added `SettingsPage`, `SettingsService`, `LocalizationService`; updated known pending items
- Updated `PROGRESS.md` (this file) with v0.6 state and new entry
- Committed and pushed all changes (localization feature + documentation) to `origin/main`

**Files modified:**
- `README.md`
- `ARCHITECTURE.md`
- `PROGRESS.md`

---

### [2026-08-03] — Feature: Localization / language selector (Spanish)

**Type**: Feature
**Who**: GitHub Copilot (automated)

**What was done:**
- Added Settings page with language selector (English / Spanish)
- Implemented `SettingsService` to persist language choice to `netlens.settings.json`
- Implemented `LocalizationService` using `.resx` resources for English/Spanish
- Integrated basic localization in the sidebar and application status
- Modified `MainWindow.xaml` and `MainWindow.xaml.cs` to support navigation to SettingsPage
- Registered new services in `App.xaml.cs` DI composition root

**Files added:**
- `src/NetLens.UI/Resources/Resources.resx` — English resource strings
- `src/NetLens.UI/Resources/Resources.es.resx` — Spanish resource strings
- `src/NetLens.UI/Services/SettingsService.cs` — reads/writes `netlens.settings.json`
- `src/NetLens.UI/Services/LocalizationService.cs` — runtime resource loading
- `src/NetLens.UI/Views/SettingsPage.xaml` — Settings UI page
- `src/NetLens.UI/Views/SettingsPage.xaml.cs` — code-behind

**Files modified:**
- `src/NetLens.UI/MainWindow.xaml` — added Settings nav item
- `src/NetLens.UI/MainWindow.xaml.cs` — nav logic for SettingsPage
- `src/NetLens.UI/App.xaml.cs` — registered SettingsService, LocalizationService
- `src/NetLens.UI/NetLens.UI.csproj` — added Resources as EmbeddedResource

**Decisions made:**
- Language change takes effect immediately (no restart required) by re-loading resource keys
- Settings file format is JSON for human readability
- Localization currently covers shell UI only; view-level strings require extending resource keys

**State after this change:**
- App shows a Settings page accessible from the sidebar
- Language toggle between English and Spanish works for shell UI elements
- Full page-level translation is a known pending item

---

### [2026-07-30] — Fix: NotSupportedException in Refresh History (SQLite DateTimeOffset)

**Type**: Bug Fix
**Who**: Cursor AI Assistant

**What was done:**
- Migrated persistence entities from `DateTimeOffset` to `DateTime` (UTC) in `DatabaseEntities.cs`
- Added `ToUtcDateTime` / `ToDateTimeOffset` conversion helpers in `SessionRepository.cs`
- Implemented SQLite schema versioning (`PRAGMA user_version = 2`) in `App.xaml.cs` to auto-recreate the DB on schema changes
- Added `SQLitePCLRaw.lib.e_sqlite3` dependency to `NetLens.Database.csproj`

**Files modified:**
- `src/NetLens.Database/Entities/DatabaseEntities.cs` — date fields as `DateTime`
- `src/NetLens.Infrastructure/Repositories/SessionRepository.cs` — domain ↔ DB mapping
- `src/NetLens.UI/App.xaml.cs` — `EnsureDatabaseSchemaAsync()` with versioning
- `src/NetLens.Database/NetLens.Database.csproj` — native SQLite package

**Decisions made:**
- Domain keeps `DateTimeOffset` internally; only the persistence layer uses `DateTime UTC`
- Auto-recreate DB when schema version changes (v2), instead of EF Core migrations
- Sessions in old DB are lost on schema upgrade (acceptable for prototype)

**State after this change:**
- Refresh History no longer throws `System.NotSupportedException`
- Old DB sessions are cleared on upgrade (expected behavior)

---

### [2026-07-30] — GitHub upload + initial documentation

**Type**: Documentation
**Who**: Antigravity AI Assistant

**What was done:**
- Created `README.md` with project overview, tech stack, features, and usage guide
- Created `ARCHITECTURE.md` with layer-by-layer architecture description, patterns, and data flow
- Created `PROGRESS.md` (this file) as the living development log
- Committed and pushed all changes to GitHub (`origin/main`)

**Motivation:**
The project lacked context documentation. The `.md` files allow AIs and contributors to understand the architecture and state without reading the entire source code.

---

### [Before 2026-07-30] — v0.5 Build

> *Note: Earlier entries are reconstructed from code analysis.*

**Type**: Implementation

#### Domain and base architecture
- Defined all Value Objects in `NetLens.Domain.Model` (RSSI, PhyRate, Latency, Jitter, PacketLossRate, SignalQuality, Channel, Frequency, MacAddress, IPAddressValue, Bandwidth, HealthScoreValue)
- Implemented `DiagnosticSession` as Aggregate Root with lifecycle: `Initializing → Monitoring → Ended`
- Implemented `WirelessSnapshot` as immutable record with all telemetry fields

#### Rule engine
- Defined `IDiagnosticRule` interface with optional Result pattern
- Implemented 5 rules: `LowRSSIRule`, `HighPacketLossRule`, `GatewayLatencyRule`, `DnsLatencyRule`, `HighJitterRule`
- Implemented `RuleEngine` that runs all DI-registered rules

#### Network and telemetry
- Implemented full P/Invoke against `wlanapi.dll` in `WlanApi.cs`
- Implemented `WifiTelemetryCollector` combining WlanAPI + IP Helper + PingService + SystemMetrics
- Implemented `PingService` (N pings, average latency, jitter, packet loss) and `TracerouteService`
- Implemented `SubnetScanner`, `ArpResolver`, `HostnameResolver` for network discovery

#### Event Bus and background services
- Implemented `EventBus` as thread-safe Singleton with `ConcurrentDictionary`
- Implemented `TelemetryBackgroundService` with 3-second loop
- Implemented `CorrelationEngine` with 5-minute window and Roaming Flap / Gateway Failover detection

#### Database
- Implemented `NetLensDbContext` with EF Core 9 + SQLite
- Created `SessionRepository` with Save/GetRecent/GetById
- Documented `DateTimeOffset` + SQLite ORDER BY bug in `REPORTS/DateTimeOffset_to_DateTime_Migration_Report.md`

#### WinUI 3 UI
- Implemented 5 views: Dashboard, WiFi Explorer, Diagnostics, Discovery, History
- Implemented ViewModels with MVVM + CommunityToolkit.Mvvm Source Generators
- Integrated LiveCharts2 graphs with rolling 60-point window (3 minutes)
- Implemented `App.xaml.cs` as Composition Root with Microsoft.Extensions.Hosting

#### Reporting
- Implemented `DiagnosticReportGenerator` with QuestPDF (Community License)
- PDF includes: session metadata, latest network status table, timeline event table with evidence and severity colors

---

## Pending / Next Steps

### High priority
- [ ] **Real packet capture**: Integrate SharpPcap / PacketDotNet + Npcap driver
- [ ] **Unit tests**: Populate `tests/NetLens.Tests/` with tests for Value Objects, Rules, and Repository
- [ ] **Exact channel and frequency**: Use `wlan_intf_opcode_channel_number` and `wlan_intf_opcode_current_operation_mode` in WlanAPI

### Medium priority
- [ ] **Real neighboring networks**: Use `WlanGetNetworkBssList` for real BSS scan
- [ ] **CorrelationEngine stub cleanup**: Remove or unify the empty file in `NetLens.Application/Services/CorrelationEngine.cs`
- [ ] **Configurable options**: Expose `CollectionInterval` (currently 3s hardcoded) and rule thresholds via settings UI
- [ ] **System notifications**: Windows Toast notifications when a critical event is detected
- [ ] **Full localization**: Extend resource keys and update each view/page to use `LocalizationService`

### Low priority
- [ ] **Light/dark theme**: Currently only implicit dark mode
- [ ] **CSV/Excel export**: In addition to PDF
- [ ] **System Tray icon**: Background monitoring without a visible window

---

## Entry Convention

Each new entry in this file must follow this format:

```markdown
### [YYYY-MM-DD] — Short change title

**Type**: Implementation | Bug Fix | Refactor | Documentation | Technical Decision
**Who**: (name or "AI Assistant name")

**What was done:**
- Bullet point for each significant change

**Files modified:**
- `path/to/file.cs` — description of change

**Decisions made:**
- Option chosen vs alternatives considered

**State after this change:**
- What works / what is still pending
```

---

*Maintained as a living document. Update at the close of each development session.*
