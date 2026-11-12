# Guía de estudio

Este documento crece con cada fase. Para cada una indica en qué orden leer el código, qué
conceptos hay que entender y qué preguntas de entrevista conviene poder responder.

## Fase 1 — Esqueleto y dominio

### Orden de lectura

1. `Tickecheck.sln` y los cuatro `.csproj` de `src/`: mirá solo los `ProjectReference` para ver
   hacia dónde apuntan las dependencias. Después leé [ADR 0001](decisions/0001-clean-architecture.md).
2. `src/Tickecheck.Domain/Common/`: `ITenantEntity` y `DomainException`.
3. `src/Tickecheck.Domain/Tickets/TicketEnums.cs` y `SlaPolicy.cs`.
4. `src/Tickecheck.Domain/Tickets/Ticket.cs`: es el archivo más importante de la fase. Empezá por
   el diccionario `AllowedTransitions` y seguí con `ChangeStatus`.
5. `TicketHistory.cs` y `Comment.cs`.
6. `Users/User.cs`, `Tenants/Tenant.cs`, `Categories/Category.cs`.
7. `tests/Tickecheck.Domain.Tests/`: empezá por `TicketFactory.cs` y después `TicketStatusTests.cs`.
   Los nombres de los tests funcionan como especificación de las reglas.
8. `docker-compose.yml` y `.env.example`.

### Reglas de negocio implementadas

| Desde | Hacia |
|---|---|
| Open | InProgress, Closed |
| InProgress | WaitingOnCustomer, Resolved |
| WaitingOnCustomer | InProgress, Resolved |
| Resolved | Closed, Reopened |
| Reopened | InProgress, Closed |
| Closed | ninguno (terminal) |

- No se puede pasar a InProgress sin agente asignado.
- Un ticket cerrado no admite cambios de datos, asignación ni prioridad.
- Cada cambio de estado, agente o prioridad agrega un `TicketHistory`. Si el valor no cambia, no
  se registra nada.
- SLA en horas corridas desde la creación: Critical 4, High 8, Medium 24, Low 72.
- Al cambiar la prioridad, el vencimiento se recalcula desde la fecha de creación.
- Un ticket está vencido si pasó su `DueAt` y no está Resolved ni Closed.

### Conceptos para estudiar

- **Regla de dependencia de Clean Architecture:** por qué las referencias apuntan hacia el dominio.
- **Modelo de dominio rico vs. anémico:** una entidad con comportamiento (`ticket.ChangeStatus(...)`)
  frente a una con solo propiedades públicas y la lógica en servicios.
- **Encapsulamiento e invariantes:** setters privados, constructor que valida, colección expuesta
  como `IReadOnlyCollection`.
- **Máquina de estados:** representarla como tabla de datos (diccionario) en vez de `if` anidados.
- **Excepciones de dominio:** diferencia entre un error de regla de negocio y un error técnico.
- **Tiempo como parámetro:** por qué los métodos reciben `utcNow` en lugar de llamar a
  `DateTime.UtcNow`, y qué tiene que ver con los tests deterministas.
- **xUnit:** `[Fact]` vs. `[Theory]` con `[InlineData]`, patrón Arrange-Act-Assert.
- **Modificador `internal`:** por qué el constructor de `TicketHistory` no es público.
- **Docker Compose:** servicios, volúmenes con nombre, healthcheck, variables desde `.env`.
- **`global.json`:** cómo se fija la versión del SDK cuando hay varias instaladas.

### Preguntas de entrevista

1. ¿Por qué las transiciones de estado están en la entidad `Ticket` y no en el controller o en un
   servicio? ¿Qué problema concreto aparecería si estuvieran en el controller?
2. `Ticket.ChangeStatus` recibe `utcNow` como parámetro. ¿Qué alternativas hay para manejar la
   fecha actual y por qué se eligió esta para el dominio?
3. El proyecto Domain no referencia ningún paquete, pero las entidades tienen un constructor
   privado vacío "para EF Core". ¿Eso rompe Clean Architecture? Justificá la respuesta.

### Glosario

| Término | Significado en este proyecto |
|---|---|
| Tenant | Una empresa cliente de la plataforma. Sus datos están aislados de los de las demás. |
| Invariante | Condición que una entidad garantiza siempre (ej.: un SuperAdmin no tiene tenant). |
| SLA | Plazo máximo para resolver un ticket según su prioridad. |
| Auditoría | Registro inmutable de quién cambió qué y cuándo (`TicketHistory`). |
| Estado terminal | Estado del que no se puede salir (`Closed`). |
