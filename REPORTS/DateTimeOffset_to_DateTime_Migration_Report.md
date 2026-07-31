# Informe: Migración DateTimeOffset -> DateTime (UTC)

## Estado: ✅ Aplicado (2026-07-30)

## Resumen
Se detectó una excepción `System.NotSupportedException` provocada por el uso de `DateTimeOffset` en una cláusula `ORDER BY` con SQLite. EF Core no puede traducir `ORDER BY` sobre `DateTimeOffset` al dialecto SQLite.

## Hallazgo principal
- Excepción: `SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses.`
- Ubicación: `SessionRepository.GetRecentSessionsAsync()` — `.OrderByDescending(s => s.StartedAt)`
- Síntoma en UI: crash al pulsar **Refresh History** en `HistoryPage`

## Solución implementada

### 1. Entidades de persistencia (`DatabaseEntities.cs`)
Campos de fecha migrados de `DateTimeOffset` a `DateTime` (UTC):
- `DiagnosticSessionRecord`: `StartedAt`, `EndedAt`
- `TimelineEventRecord`: `OccurredAt`
- `WirelessSnapshotRecord`: `CapturedAt`

### 2. Repositorio (`SessionRepository.cs`)
Helpers de conversión añadidos:
- `ToUtcDateTime(DateTimeOffset)` — al guardar
- `ToDateTimeOffset(DateTime)` — al leer (con `DateTimeKind.Utc`)

El dominio (`DiagnosticSession`, `TimelineEvent`, `WirelessSnapshot`) **mantiene `DateTimeOffset`**.

### 3. Versionado de esquema (`App.xaml.cs`)
En lugar de migraciones EF Core, se usa `PRAGMA user_version`:
- Versión actual del esquema: **2**
- Si la versión almacenada difiere, la BD se elimina y recrea automáticamente al arrancar
- Esto garantiza compatibilidad sin intervención manual del usuario

## Archivos modificados
- `src/NetLens.Database/Entities/DatabaseEntities.cs`
- `src/NetLens.Infrastructure/Repositories/SessionRepository.cs`
- `src/NetLens.UI/App.xaml.cs`
- `src/NetLens.Database/NetLens.Database.csproj` (añadido `SQLitePCLRaw.lib.e_sqlite3`)

## Impacto en datos existentes
- Bases de datos creadas antes de este fix (sin `user_version = 2`) se recrean al primer arranque
- **Las sesiones históricas previas se pierden** al actualizar; comportamiento aceptable para prototipo

## Alternativas descartadas
- **Ordenar en cliente**: funciona pero trae todas las filas a memoria; no escala
- **ValueConverter a string/ticks**: más cambios en almacenamiento, menos legible en SQLite
- **Migraciones EF Core**: overhead innecesario para prototipo con `EnsureCreated`

---
Generado por: GitHub Copilot
Aplicado por: Cursor AI Assistant
Fecha detección: 2026-07-30
Fecha aplicación: 2026-07-30
