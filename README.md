# NetLens — Network Diagnostic Tool for Windows

> **Visibilidad total de tu red WiFi, en tiempo real.**
> Aplicación de escritorio Windows (WinUI 3 / .NET 10) para monitoreo continuo, diagnóstico y reporte de la calidad de la red inalámbrica.

---

## ¿Qué es NetLens?

NetLens es un **visor WiFi profesional para PC** diseñado para técnicos de redes e IT. Captura métricas de la conexión inalámbrica cada **3 segundos** mediante la Windows WLAN API, las analiza en tiempo real con un motor de reglas, y emite alertas y reportes PDF exportables.

El proyecto es un **prototipo funcional** en desarrollo activo, construido con arquitectura limpia (Clean Architecture) y DDD (Domain-Driven Design).

---

## Características principales

| Módulo | Estado | Descripción |
|---|---|---|
| **Dashboard en tiempo real** | ✅ Funcional | RSSI, PHY Rate, Latencia, Jitter, Packet Loss, CPU/RAM |
| **Motor de reglas diagnósticas** | ✅ Funcional | 5 reglas: LowRSSI, HighPacketLoss, GatewayLatency, DnsLatency, HighJitter |
| **Motor de correlación** | ✅ Funcional | Detección de Roaming Flap y Gateway Failover |
| **Explorador WiFi** | ✅ Funcional | Vista del AP conectado + redes vecinas simuladas |
| **Diagnóstico manual** | ✅ Funcional | Escaneo bajo demanda con Health Score |
| **Descubrimiento de red** | ✅ Funcional | Escaneo de subred via ARP + resolución DNS |
| **Historial de sesiones** | ✅ Funcional | SQLite / EF Core — últimas 50 sesiones |
| **Exportación PDF** | ✅ Funcional | Reportes con QuestPDF (Community License) |
| **Captura de paquetes** | 🚧 Pendiente | `NullPacketCapture` como stub; Npcap no integrado |

---

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| **UI / Framework** | WinUI 3 (Windows App SDK 1.6) |
| **Lenguaje** | C# 13 / .NET 10 |
| **MVVM** | CommunityToolkit.Mvvm 8.3.2 |
| **Gráficas** | LiveChartsCore.SkiaSharpView.WinUI 2.0.0-rc3 |
| **DI / Hosting** | Microsoft.Extensions.Hosting 9.0 |
| **Base de datos** | SQLite + Entity Framework Core 9 |
| **PDF** | QuestPDF (Community License) |
| **APIs de sistema** | WlanAPI (P/Invoke), IP Helper API, PerformanceCounter |

---

## Requisitos del sistema

- **OS**: Windows 10 1903 (build 19041) o superior
- **Runtime**: Windows App SDK 1.6 (self-contained)
- **Arquitecturas**: x86, x64, ARM64
- **Permisos**: Sin elevación de privilegios requerida para métricas WiFi; ARP scan puede requerir permisos de red

---

## Estructura del repositorio

```
VisorWifiForPc/
├── NetLens.sln                     # Solución principal
├── README.md                       # Este archivo
├── ARCHITECTURE.md                 # Arquitectura detallada
├── PROGRESS.md                     # Registro de avances
├── REPORTS/                        # Informes técnicos de decisiones
│   └── DateTimeOffset_to_DateTime_Migration_Report.md
├── src/
│   ├── NetLens.Domain/             # Núcleo del dominio (Entities, Value Objects, Rules)
│   ├── NetLens.Application/        # Contratos de aplicación (Abstractions, Services)
│   ├── NetLens.Network/            # Implementación de red (WiFi, Discovery, Diagnostics)
│   ├── NetLens.Infrastructure/     # Repositorios (EF Core / SQLite)
│   ├── NetLens.Database/           # DbContext y entidades de BD
│   ├── NetLens.Services/           # Background Services (Telemetry, Correlation)
│   ├── NetLens.Reporting/          # Generación de reportes PDF (QuestPDF)
│   └── NetLens.UI/                 # Capa de presentación WinUI 3 (MVVM)
└── tests/
    └── NetLens.Tests/              # Pruebas unitarias (en desarrollo)
```

---

## Cómo compilar y ejecutar

```powershell
# Restaurar dependencias
dotnet restore NetLens.sln

# Compilar en modo debug
dotnet build NetLens.sln -c Debug

# Ejecutar la aplicación UI
dotnet run --project src/NetLens.UI/NetLens.UI.csproj
```

> **Nota**: La primera ejecución crea automáticamente la base de datos SQLite `netlens.db` en el directorio de ejecución.

---

## Flujo de datos resumido

```
[WlanAPI / IP Helper / PingService]
          ↓  cada 3 segundos
[WifiTelemetryCollector] → WirelessSnapshot
          ↓
[TelemetryBackgroundService] → publica TelemetryCollectedEvent
          ↓                           ↓
[CorrelationEngine]          [DashboardViewModel]
 (Roaming Flap,               (UI en tiempo real,
  Gateway Failover)            gráficas LiveCharts2)
          ↓
   [IEventBus] → otros suscriptores
```

---

## Licencias de terceros

| Librería | Licencia |
|---|---|
| CommunityToolkit.Mvvm | MIT |
| LiveChartsCore | MIT |
| QuestPDF | Community (gratis para individuos/PYMES) |
| Microsoft.WindowsAppSDK | MIT |
| Entity Framework Core | Apache 2.0 |

---

*Proyecto en estado de prototipo activo. Ver [PROGRESS.md](./PROGRESS.md) para el registro de avances y [ARCHITECTURE.md](./ARCHITECTURE.md) para los detalles de arquitectura.*
