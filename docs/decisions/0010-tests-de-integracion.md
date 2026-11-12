# ADR 0010 — Tests de integración contra SQL Server real con Testcontainers

**Estado:** aceptada · **Fase:** 2

## Contexto

El aislamiento entre empresas es la promesa central del proyecto y depende de cómo EF Core
traduce los filtros a SQL. Hay que probarlo de una forma que dé confianza real.

## Decisión

Tres niveles de tests:

| Proyecto | Qué prueba | Con qué |
|---|---|---|
| `Domain.Tests` | Transiciones, SLA, auditoría | Solo xUnit. Sin mocks ni base. |
| `Application.Tests` | Casos de uso | xUnit + Moq (repositorios simulados) |
| `IntegrationTests` | Aislamiento, roles, flujo completo, SignalR | API en memoria + SQL Server en Docker |

Para los de integración, `IntegrationFixture` levanta **un** contenedor de SQL Server con
Testcontainers para toda la corrida, y la API con `WebApplicationFactory<Program>`. Al arrancar,
la API aplica las migraciones y carga los datos de demo, igual que en desarrollo. Los tests hacen
requests HTTP reales con tokens obtenidos del endpoint de login.

El aislamiento se prueba en dos niveles: directamente sobre `AppDbContext` (filtros y validación
al guardar) y por HTTP (un usuario de Acme contra Ids de Globex).

## Alternativas descartadas

- **Proveedor InMemory de EF Core.** No es una base relacional: no valida claves foráneas ni
  índices únicos, y no ejecuta SQL. Un test verde ahí no dice nada sobre producción.
- **SQLite en memoria.** Es relacional y rápido, pero es otro motor: funciones como
  `DateDiffMinute` o el comportamiento de los borrados en cascada difieren.
- **Una base por test o reiniciarla entre tests.** Aislamiento perfecto, pero lento. Se prefirió
  que cada test cree los datos que modifica (un ticket nuevo, una empresa nueva) y no dependa de
  no pisarse con otros.

## Consecuencias

- Los tests corren el mismo SQL que producción, incluidas las migraciones.
- Hace falta Docker para correrlos, en la máquina local y en CI (los runners de GitHub lo traen).
- La suite de integración tarda unos 10 segundos más el arranque del contenedor.
- Los tests comparten base, así que las aserciones sobre totales usan "al menos" en vez de
  números exactos cuando otro test podría haber agregado datos.
