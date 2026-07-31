# Informe: Migración DateTimeOffset -> DateTime (UTC)

## Resumen
Se detectó una excepción System.NotSupportedException provocada por el uso de DateTimeOffset en una cláusula ORDER BY con SQLite. EF Core no puede traducir ORDER BY sobre DateTimeOffset al dialecto SQLite.

## Hallazgo principal
- Excepción: SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses.
- Ubicación: SessionRepository.GetRecentSessionsAsync() — .OrderByDescending(s => s.StartedAt)

## Archivos afectados
- src/NetLens.Database/Entities/DatabaseEntities.cs
  - DiagnosticSessionRecord: StartedAt, EndedAt (DateTimeOffset -> DateTime)
  - TimelineEventRecord: OccurredAt (DateTimeOffset -> DateTime)
  - WirelessSnapshotRecord: CapturedAt (DateTimeOffset -> DateTime)
- src/NetLens.Infrastructure/Repositories/SessionRepository.cs
  - SaveSessionAsync: conversión DateTimeOffset -> DateTime (UtcDateTime)
  - GetRecentSessionsAsync: ya funcionará tras el cambio en las entidades
  - GetSessionByIdAsync: conversión DateTime -> DateTimeOffset al reconstruir dominio
- src/NetLens.Domain/Entities/DiagnosticSession.cs (dominio mantiene DateTimeOffset)

## Cambios propuestos (ejemplos de código)
1) Entidad: DatabaseEntities.cs
```csharp
// Antes
public DateTimeOffset StartedAt { get; set; }
public DateTimeOffset? EndedAt { get; set; }

// Después
public DateTime StartedAt { get; set; }
public DateTime? EndedAt { get; set; }
```
Aplicar mismo cambio para OccurredAt y CapturedAt.

2) Guardado (SessionRepository.cs)
```csharp
record.StartedAt = session.StartedAt.UtcDateTime;
record.EndedAt = session.EndedAt?.UtcDateTime;
```

3) Lectura/Reconstrucción del dominio (SessionRepository.cs)
```csharp
var startedDto = new DateTimeOffset(DateTime.SpecifyKind(record.StartedAt, DateTimeKind.Utc));
var endedDto = record.EndedAt.HasValue
	? new DateTimeOffset(DateTime.SpecifyKind(record.EndedAt.Value, DateTimeKind.Utc))
	: (DateTimeOffset?)null;
var session = new DiagnosticSession(record.SessionId, startedDto, endedDto, state);
```

## Pasos de migración sugeridos
1. Hacer backup de la base de datos SQLite.
2. Modificar las clases de entidad y el código de conversión.
3. Generar migración EF Core:
```bash
dotnet ef migrations add ConvertDateOffsetToUtcDateTime
```
4. Revisar la migración generada (SQLite suele recrear tablas para cambios de tipo).
5. Aplicar la migración en entorno de pruebas:
```bash
dotnet ef database update
```
6. Validar datos y pruebas de integración (ordenación, listados, lectura/escritura).

## Complicaciones y riesgos
- SQLite puede recrear tablas al cambiar tipos; esto puede implicar pérdida si no se maneja export/import.
- Es necesario comprobar que todos los valores persistidos se interpretan como UTC. Conviene revisar datos históricos.
- Alternativa temporal: ordenar en cliente (materializar y ordenar en memoria) — impacta rendimiento y escalabilidad.
- Otra alternativa: usar ValueConverter para persistir DateTimeOffset como ISO8601 string o ticks. Requiere menos cambios al modelo pero cambia almacenamiento.

## Pruebas recomendadas
- Tests unitarios para SaveSessionAsync/GetSessionByIdAsync.
- Pruebas de integración contra copia de la base SQLite real.
- Validar ordenación en GetRecentSessionsAsync después de migración.

---
Generado por: GitHub Copilot
Fecha: REEMPLAZAR_POR_FECHA
