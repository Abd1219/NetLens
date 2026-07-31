# NetLens — Arquitectura del Sistema

> Este documento describe la arquitectura técnica de NetLens para facilitar el contexto a IAs, colaboradores y revisores de código.

---

## Principios de diseño

NetLens sigue **Clean Architecture** con las siguientes capas (de interior a exterior):

1. **Domain** — Núcleo puro: entidades, value objects, reglas de negocio
2. **Application** — Contratos (interfaces) y orquestación ligera
3. **Infrastructure / Database** — Implementaciones de persistencia
4. **Network** — Adaptadores de hardware y red (Win32 APIs)
5. **Services** — Servicios de fondo (background services)
6. **Reporting** — Generación de reportes
7. **UI** — Presentación WinUI 3 (MVVM puro)

La **Dependency Rule** es estricta: las capas internas nunca dependen de las externas. La UI y los servicios dependen de abstracciones (interfaces) definidas en Application.

---

## Diagrama de capas

```
┌─────────────────────────────────────────────────────────────┐
│                      NetLens.UI (WinUI 3)                   │
│  Views / ViewModels / Converters / Styles                   │
└──────────────────────────┬──────────────────────────────────┘
                           │ depende de
┌──────────────────────────▼──────────────────────────────────┐
│              NetLens.Application (Contratos)                │
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
       └──── todos dependen de ────────────────────────────────┐
                                                               │
┌──────────────────────────────────────────────────────────────▼──┐
│                   NetLens.Domain                                 │
│  Entities: WirelessSnapshot, DiagnosticSession, TimelineEvent   │
│  Value Objects: RSSI, PhyRate, Latency, Jitter, PacketLossRate  │
│  Rules: IDiagnosticRule + 5 implementaciones                    │
│  Events: TelemetryCollectedEvent, CorrelationAlertEvent         │
└─────────────────────────────────────────────────────────────────┘
```

---

## Módulo por módulo

### NetLens.Domain

El corazón del sistema. **Sin dependencias externas.**

#### Entidades (Aggregate Roots / Entities)

| Clase | Tipo | Descripción |
|---|---|---|
| `DiagnosticSession` | Aggregate Root | Ciclo de vida de una sesión de diagnóstico. Coordina snapshots y timeline. Estado: Initializing → Monitoring → Ended |
| `WirelessSnapshot` | Entity (sealed record) | Frame inmutable de métricas WiFi capturadas en un instante. Es el "ledger" de la sesión |
| `TimelineEvent` | Entity | Evento con timestamp, severidad y evidencia. Se añade al timeline de la sesión |
| `DiscoveredDevice` | Entity | Dispositivo descubierto en la subred (ARP scan) |
| `CapturedPacket` | Entity | Paquete de red capturado (actualmente stub) |
| `TracerouteHop` | Entity | Salto individual de un traceroute |

#### Value Objects (Model/)

Todos son **inmutables** con validación en constructor:

| Value Object | Rango / Regla |
|---|---|
| `RSSI` | -100 a 0 dBm |
| `PhyRate` | ≥ 0 Mbps |
| `Channel` | 1-196 |
| `Frequency` | MHz (2400-6000) |
| `SignalQuality` | 0-100% calculado desde RSSI |
| `Latency` | 0-∞ ms, con soporte de Timeout |
| `Jitter` | ≥ 0 ms |
| `PacketLossRate` | 0-100% |
| `Bandwidth` | ≥ 0 Mbps |
| `HealthScoreValue` | 0-100 |
| `MacAddress` | Formato `XX:XX:XX:XX:XX:XX` |
| `IPAddressValue` | IPv4 string |

#### Reglas de diagnóstico (Rules/)

Interfaz: `IDiagnosticRule.Evaluate(WirelessSnapshot) → DiagnosticResult?`

| Regla | Código | Umbral | Severidad |
|---|---|---|---|
| `LowRSSIRule` | `LOW_RSSI` | RSSI < -75 dBm | Warning / Critical (<-85) |
| `HighPacketLossRule` | `HIGH_PACKET_LOSS` | > 5% | Warning / Critical (>15%) |
| `GatewayLatencyRule` | `HIGH_GATEWAY_LATENCY` | > 100 ms | Warning / Critical (>300ms) |
| `DnsLatencyRule` | `HIGH_DNS_LATENCY` | > 150 ms | Warning / Critical (>400ms) |
| `HighJitterRule` | `HIGH_JITTER` | > 30 ms | Warning / Critical (>80ms) |

---

### NetLens.Application

Únicamente interfaces (contratos) y servicios de aplicación simples.

| Interface | Responsabilidad |
|---|---|
| `IEventBus` | Pub/Sub desacoplado. `PublishAsync<T>()` / `Subscribe<T>()` / `Unsubscribe<T>()` |
| `ITelemetryCollector` | `CaptureSnapshotAsync()` → `WirelessSnapshot?` |
| `ISessionRepository` | `SaveSessionAsync()`, `GetRecentSessionsAsync()`, `GetSessionByIdAsync()` |
| `IReportGenerator` | `GeneratePdfReport(DiagnosticSession)` → `byte[]` |
| `IRuleEngine` | `Evaluate(WirelessSnapshot)` → `IReadOnlyList<DiagnosticResult>` |
| `IPacketCapture` | `StartCapture()` / `StopCapture()` — actualmente NullObject |

**Servicios en Application:**
- `EventBus`: Implementación Singleton con diccionario de handlers por tipo de evento. Thread-safe.
- `RuleEngine`: Ejecuta todas las `IDiagnosticRule` registradas contra un snapshot.
- `CorrelationEngine` (stub en Application, implementación en Services).

---

### NetLens.Network

Implementaciones de red que dependen de APIs del sistema operativo Windows.

#### WiFi (`Wifi/`)
- **`WlanApi.cs`**: P/Invoke directo contra `wlanapi.dll`. Expone `WlanOpenHandle`, `WlanEnumInterfaces`, `WlanQueryInterface`, `WlanFreeMemory`. Mapea structs nativos (`WLAN_CONNECTION_ATTRIBUTES`, `WLAN_INTERFACE_INFO_LIST`, etc.)
- **`WifiTelemetryCollector.cs`**: Implementa `ITelemetryCollector`. Combina WlanAPI + IP Helper API + PingService para construir un `WirelessSnapshot` completo. Ejecuta 3 pings en paralelo (gateway, DNS, internet).

#### Adapters (`Adapters/`)
- **`SystemMetricsCollector.cs`**: Lectura de CPU (PerformanceCounter) y RAM (GlobalMemoryStatusEx).

#### Discovery (`Discovery/`)
- **`SubnetScanner.cs`**: Escaneo de subred por rango CIDR, ping a cada host.
- **`ArpResolver.cs`**: Resolución MAC vía tabla ARP del sistema (SendARP P/Invoke).
- **`HostnameResolver.cs`**: Resolución DNS inversa de IPs descubiertas.

#### Diagnostics (`Diagnostics/`)
- **`PingService.cs`**: Realiza N pings y devuelve `PingResult` con latencia promedio, jitter y packet loss.
- **`TracerouteService.cs`**: Implementa traceroute manual con TTL incremental.

#### PacketCapture (`PacketCapture/`)
- **`NullPacketCapture.cs`**: Implementación vacía (NullObject pattern). Placeholder hasta integrar Npcap.

---

### NetLens.Database

Persistencia con **Entity Framework Core 9 + SQLite**.

- **`NetLensDbContext`**: DbSets para `DiagnosticSessionRecord`, `WirelessSnapshotRecord`, `TimelineEventRecord`.
- **Entidades de BD**: Separadas de las entidades de dominio. Usan `DateTime` (UTC) en lugar de `DateTimeOffset` por limitación de SQLite con ORDER BY.

> ⚠️ Decisión técnica: El dominio usa `DateTimeOffset` internamente. El repositorio convierte a/desde `DateTime UTC` en la capa de persistencia. Ver `REPORTS/DateTimeOffset_to_DateTime_Migration_Report.md`.

---

### NetLens.Infrastructure

- **`SessionRepository.cs`**: Implementa `ISessionRepository`. Guarda y recupera `DiagnosticSession` desde SQLite, mapeando entre entidades de dominio y BD. Convierte `DateTimeOffset` ↔ `DateTime UTC` en lectura/escritura.

---

### NetLens.Services

Servicios de fondo que se ejecutan durante toda la vida de la aplicación.

- **`TelemetryBackgroundService`** (`BackgroundService`): Loop que captura un `WirelessSnapshot` cada **3 segundos**, lo registra en la `DiagnosticSession` activa y publica `TelemetryCollectedEvent` en el `IEventBus`.
- **`CorrelationEngine`** (`BackgroundService + IEventHandler<TelemetryCollectedEvent>`): Mantiene ventana deslizante de 5 minutos de snapshots. Detecta:
  - **Roaming Flap**: > 3 cambios de BSSID en 60 segundos
  - **Gateway Failover**: Cambio de IP del gateway

---

### NetLens.Reporting

- **`DiagnosticReportGenerator`**: Implementa `IReportGenerator`. Genera PDF con **QuestPDF** (Community License). El reporte incluye: metadata de sesión, estado de red más reciente, tabla de eventos de timeline con evidencia.

---

### NetLens.UI

Capa de presentación **WinUI 3** con patrón **MVVM puro** usando CommunityToolkit.Mvvm.

#### Vistas (Views/)
| Vista | ViewModel | Descripción |
|---|---|---|
| `DashboardPage` | `DashboardViewModel` | Métricas en tiempo real + 3 gráficas LiveCharts2 (RSSI, Latencia, Packet Loss) |
| `WifiExplorerPage` | `WifiExplorerViewModel` | Info del AP conectado + tabla de redes vecinas |
| `DiagnosticsPage` | `DiagnosticsViewModel` | Escaneo manual, Health Score, lista de alertas |
| `DiscoveryPage` | `DiscoveryViewModel` | Escaneo de subred, tabla de dispositivos |
| `HistoryPage` | `HistoryViewModel` | Listado de sesiones pasadas + exportación PDF |

#### ViewModels
- Todos heredan `ObservableObject` (CommunityToolkit.Mvvm)
- Los que reciben telemetría implementan `IEventHandler<TelemetryCollectedEvent>` y se suscriben al `IEventBus`
- Actualizaciones de UI siempre se despachan al hilo principal via `DispatcherQueue.TryEnqueue()`

#### Composición DI (`App.xaml.cs`)
`App.xaml.cs` actúa como **Composition Root** usando `Microsoft.Extensions.Hosting`. Registra todos los servicios, inicia el Host y crea la ventana principal. La base de datos SQLite se inicializa via `EnsureDatabaseSchemaAsync()`: comprueba `PRAGMA user_version` y recrea el esquema automáticamente si la versión cambió (actualmente v2).

---

## Flujo de eventos

```
TelemetryBackgroundService
    │
    ├─ CaptureSnapshotAsync()  →  WifiTelemetryCollector
    │                               ├─ WlanAPI (RSSI, PHY Rate, SSID, BSSID)
    │                               ├─ IP Helper (Gateway, DNS, Local IP, MAC)
    │                               ├─ PingService x3 (latencia, jitter, packet loss)
    │                               └─ SystemMetrics (CPU, RAM)
    │
    └─ PublishAsync(TelemetryCollectedEvent)
            │
            ├─► DashboardViewModel.HandleAsync()
            │       └─ RuleEngine.Evaluate() → alertas
            │       └─ DispatcherQueue → UpdateFromSnapshot() + gráficas
            │
            ├─► WifiExplorerViewModel.HandleAsync()
            │       └─ Actualiza SSID/BSSID/RSSI/Channel
            │
            └─► CorrelationEngine.HandleAsync()
                    ├─ Roaming Flap → PublishAsync(CorrelationAlertEvent)
                    └─ Gateway Failover → PublishAsync(CorrelationAlertEvent)
```

---

## Patrones y decisiones de diseño notables

| Patrón | Dónde | Razón |
|---|---|---|
| **NullObject** | `NullPacketCapture` | Permite compilar/ejecutar sin Npcap; fácil swap futuro |
| **Aggregate Root** | `DiagnosticSession` | Controla el ciclo de vida del ledger de snapshots |
| **Value Objects inmutables** | Todo `Domain/Model/` | Seguridad de tipos, no primitivas expuestas |
| **Event Bus Pub/Sub** | `EventBus` | Desacopla telemetría de UI; múltiples suscriptores sin dependencias directas |
| **Composition Root** | `App.xaml.cs` | Un único lugar donde se alambra todo el DI container |
| **MVVM con Source Generators** | ViewModels | `[ObservableProperty]` elimina boilerplate de `INotifyPropertyChanged` |
| **Background Service loop** | `TelemetryBackgroundService` | Integración nativa con `IHostedService` y `CancellationToken` de shutdown |

---

## Pendientes arquitecturales conocidos

- [ ] `CorrelationEngine` en `NetLens.Application/Services/CorrelationEngine.cs` es un stub vacío; la implementación real vive en `NetLens.Services/`. Requiere limpieza/unificación.
- [ ] `IPacketCapture` / `NullPacketCapture` — pendiente integrar Npcap (SharpPcap o PacketDotNet).
- [ ] Las redes vecinas en `WifiExplorerViewModel` son datos simulados (`PopulateSurroundingNetworks()`); pendiente integrar scan real de BSS via WlanAPI.
- [ ] El canal/frecuencia se derive de forma heurística basada en `dot11PhyType`; pendiente usar `wlan_intf_opcode_channel_number` para lectura exacta.
- [ ] `tests/NetLens.Tests/` está vacío — pendiente pruebas unitarias.
