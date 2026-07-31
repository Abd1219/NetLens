# NetLens — Registro de Avances del Proyecto

> Este archivo es el **log oficial de progreso** del proyecto NetLens.
> Cada entrada documenta qué se implementó, qué decisiones se tomaron y cuál es el estado actual.
> Actualizar este archivo al finalizar cada sesión de desarrollo o milestone significativo.

---

## Estado actual del proyecto

**Versión**: v0.5 (Prototipo funcional)
**Fase**: Desarrollo activo
**Última actualización**: 2026-07-30

### Resumen de componentes

| Componente | Estado | Notas |
|---|---|---|
| Dominio (Entities + Value Objects) | ✅ Completo | Inmutables, validación en constructor |
| Motor de reglas (5 reglas) | ✅ Completo | LowRSSI, HighPacketLoss, GatewayLatency, DnsLatency, HighJitter |
| Motor de correlación | ✅ Completo | Roaming Flap + Gateway Failover |
| WlanAPI (P/Invoke) | ✅ Completo | RSSI, PHY Rate, SSID, BSSID, PHY Type |
| Telemetría combinada | ✅ Completo | WiFi + IP Helper + PingService + SystemMetrics |
| Background Services | ✅ Completo | TelemetryBackgroundService + CorrelationEngine |
| Event Bus | ✅ Completo | Pub/Sub desacoplado, thread-safe |
| Dashboard UI | ✅ Completo | Gráficas LiveCharts2, métricas en tiempo real |
| WiFi Explorer UI | ✅ Completo | Redes vecinas simuladas |
| Diagnóstico manual | ✅ Completo | Health Score calculado |
| Descubrimiento de red | ✅ Completo | ARP + DNS reverso |
| Historial de sesiones | ✅ Completo | SQLite, últimas 50 sesiones |
| Exportación PDF | ✅ Completo | QuestPDF, Community License |
| Captura de paquetes | ❌ Pendiente | NullPacketCapture stub |
| Redes vecinas reales | ❌ Pendiente | Actualmente datos simulados |
| Pruebas unitarias | ❌ Pendiente | Directorio tests/ vacío |
| Canal/Frecuencia exactos | ⚠️ Parcial | Heurística por PHY type, no WlanAPI exacto |

---

## Historial de avances

### [2026-07-30] — Subida a GitHub + documentación del proyecto

**Tipo**: Documentación
**Quién**: Antigravity AI Assistant

**Qué se hizo:**
- Creado `README.md` con descripción general, stack tecnológico, características y guía de uso
- Creado `ARCHITECTURE.md` con descripción detallada de capas, módulos, patrones y flujo de datos
- Creado `PROGRESS.md` (este archivo) como registro vivo de avances
- Commit y push de todos los cambios al repositorio GitHub (`origin/main`)

**Motivación:**
El proyecto carecía de documentación de contexto. Los archivos `.md` permiten que IAs y colaboradores entiendan la arquitectura y el estado sin necesidad de leer todo el código fuente.

---

### [Antes de 2026-07-30] — Construcción de la v0.5

> *Nota: Las entradas anteriores se reconstruyen desde el análisis del código existente.*

**Tipo**: Implementación

#### Dominio y arquitectura base
- Definidos todos los Value Objects de `NetLens.Domain.Model` (RSSI, PhyRate, Latency, Jitter, PacketLossRate, SignalQuality, Channel, Frequency, MacAddress, IPAddressValue, Bandwidth, HealthScoreValue)
- Implementado `DiagnosticSession` como Aggregate Root con lifecycle: `Initializing → Monitoring → Ended`
- Implementado `WirelessSnapshot` como record inmutable con todos los campos de telemetría

#### Motor de reglas
- Definida interfaz `IDiagnosticRule` con patrón de Result opcional
- Implementadas 5 reglas: `LowRSSIRule`, `HighPacketLossRule`, `GatewayLatencyRule`, `DnsLatencyRule`, `HighJitterRule`
- Implementado `RuleEngine` que ejecuta todas las reglas registradas vía DI

#### Red y telemetría
- Implementado P/Invoke completo contra `wlanapi.dll` en `WlanApi.cs`
- Implementado `WifiTelemetryCollector` combinando WlanAPI + IP Helper + PingService + SystemMetrics
- Implementados `PingService` (N pings, latencia promedio, jitter, packet loss) y `TracerouteService`
- Implementados `SubnetScanner`, `ArpResolver`, `HostnameResolver` para descubrimiento de red

#### Event Bus y servicios de fondo
- Implementado `EventBus` como Singleton thread-safe con `ConcurrentDictionary`
- Implementado `TelemetryBackgroundService` con loop de 3 segundos
- Implementado `CorrelationEngine` con ventana de 5 minutos y detección de Roaming Flap / Gateway Failover

#### Base de datos
- Implementado `NetLensDbContext` con EF Core 9 + SQLite
- Creado `SessionRepository` con Save/GetRecent/GetById
- **Bug resuelto**: `DateTimeOffset` no soportado por SQLite en ORDER BY → migrado a `DateTime UTC` en entidades de BD; dominio mantiene `DateTimeOffset`. Documentado en `REPORTS/DateTimeOffset_to_DateTime_Migration_Report.md`

#### UI WinUI 3
- Implementadas 5 vistas: Dashboard, WiFi Explorer, Diagnostics, Discovery, History
- Implementados ViewModels con MVVM + CommunityToolkit.Mvvm Source Generators
- Integradas gráficas LiveCharts2 con ventana rolling de 60 puntos (3 minutos)
- Implementado `App.xaml.cs` como Composition Root con Microsoft.Extensions.Hosting

#### Reporting
- Implementado `DiagnosticReportGenerator` con QuestPDF (Community License)
- El PDF incluye: metadata de sesión, tabla de estado de red más reciente, timeline de eventos con evidencia y colores por severidad

---

## Pendientes y próximos pasos

### Alta prioridad
- [ ] **Captura de paquetes real**: Integrar SharpPcap / PacketDotNet + Npcap como driver
- [ ] **Pruebas unitarias**: Poblar `tests/NetLens.Tests/` con pruebas para Value Objects, Rules y Repository
- [ ] **Canal y frecuencia exactos**: Usar `wlan_intf_opcode_channel_number` y `wlan_intf_opcode_current_operation_mode` en WlanAPI

### Media prioridad
- [ ] **Redes vecinas reales**: Usar `WlanGetNetworkBssList` para scan real de BSS disponibles
- [ ] **Limpieza de CorrelationEngine stub**: Eliminar o unificar el archivo vacío en `NetLens.Application/Services/CorrelationEngine.cs`
- [ ] **Opciones configurables**: Exponer `CollectionInterval` (actualmente 3s hardcoded) y umbrales de reglas vía settings
- [ ] **Notificaciones del sistema**: Toast notifications de Windows cuando se detecte un evento crítico

### Baja prioridad
- [ ] **Localización**: Soporte multi-idioma (actualmente todo en inglés en código, español en UI parcialmente)
- [ ] **Tema claro/oscuro**: Actualmente solo tema oscuro implícito
- [ ] **Exportación CSV/Excel**: Además de PDF
- [ ] **Icono de bandeja del sistema (System Tray)**: Monitoreo en background sin ventana visible

---

## Convención para futuras entradas

Cada nueva entrada en este archivo debe seguir este formato:

```markdown
### [YYYY-MM-DD] — Título corto del cambio

**Tipo**: Implementación | Bug Fix | Refactor | Documentación | Decisión técnica
**Quién**: (nombre o "AI Assistant")

**Qué se hizo:**
- Bullet point de cada cambio significativo

**Archivos modificados:**
- `ruta/al/archivo.cs` — descripción del cambio

**Decisiones tomadas:**
- Opción elegida vs alternativas consideradas

**Estado tras este avance:**
- Qué funciona / qué queda pendiente
```

---

*Mantenido como documento vivo. Actualizar al cierre de cada sesión de desarrollo.*
