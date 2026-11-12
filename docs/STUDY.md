# Guía de estudio

Esta guía recorre el proyecto **de la base a la superficie**: primero las reglas de negocio, que
no dependen de nada, y al final el navegador y el deploy. Cada etapa indica qué archivos leer y
en qué orden, qué conceptos hay que entender y tres preguntas de entrevista.

Antes de empezar conviene tener la app corriendo (ver el [README](../README.md)) y leer la
sección 2 de [ARCHITECTURE.md](ARCHITECTURE.md), que sigue una request completa.

**Cómo estudiar cada archivo:** leé primero los comentarios (explican el porqué), después el
código, y por último el test que lo ejercita. Si podés explicar en voz alta por qué cada línea
está ahí, pasá al siguiente.

| Etapa | Tema | Proyecto principal |
|---|---|---|
| 1 | Reglas de negocio | Domain |
| 2 | Base de datos y multi-tenancy | Infrastructure |
| 3 | Autenticación y roles | Application, Infrastructure, Api |
| 4 | Casos de uso de tickets | Application |
| 5 | Comentarios, auditoría y SLA | Application |
| 6 | Tiempo real y dashboard | Api |
| 7 | Frontend | Web |
| 8 | Docker, CI/CD y deploy | raíz del repo |

---

## Etapa 1 — Dominio

### Orden de lectura

1. `Tickecheck.sln` y los `.csproj` de `src/`: solo los `ProjectReference`. Después
   [ADR 0001](decisions/0001-clean-architecture.md).
2. `src/Tickecheck.Domain/Common/ITenantEntity.cs` y `DomainException.cs`.
3. `Domain/Tickets/TicketEnums.cs` y `SlaPolicy.cs`.
4. `Domain/Tickets/Ticket.cs`. Es el archivo más importante de la etapa: empezá por el
   diccionario `AllowedTransitions` y seguí con `ChangeStatus`.
5. `Domain/Tickets/TicketHistory.cs` y `Comment.cs`.
6. `Domain/Users/User.cs`, `RefreshToken.cs`, `Tenants/Tenant.cs`, `Categories/Category.cs`.
7. `tests/Tickecheck.Domain.Tests/`: `TicketFactory.cs`, después `TicketStatusTests.cs`,
   `TicketChangesTests.cs` y `SlaTests.cs`. Los nombres de los tests son la especificación.

### Reglas implementadas

| Desde | Hacia |
|---|---|
| Open | InProgress, Closed |
| InProgress | WaitingOnCustomer, Resolved |
| WaitingOnCustomer | InProgress, Resolved |
| Resolved | Closed, Reopened |
| Reopened | InProgress, Closed |
| Closed | ninguno (terminal) |

- No se puede pasar a InProgress sin agente asignado.
- Un ticket cerrado no admite cambios.
- Cada cambio de estado, agente o prioridad agrega un `TicketHistory`.
- SLA en horas corridas desde la creación: Critical 4, High 8, Medium 24, Low 72.
- Al cambiar la prioridad, el vencimiento se recalcula desde la creación.

### Conceptos

- Regla de dependencia de Clean Architecture.
- Modelo de dominio rico vs. anémico.
- Encapsulamiento: setters privados, constructor que valida, `IReadOnlyCollection`.
- Máquina de estados representada como datos (diccionario).
- Excepciones de dominio vs. errores técnicos.
- El tiempo como parámetro (`utcNow`) y los tests deterministas.
- xUnit: `[Fact]`, `[Theory]`, `[InlineData]`, patrón Arrange-Act-Assert.
- Modificador `internal`; `record` vs. `class`; tipos nullable (`Guid?`).

### Preguntas de entrevista

1. ¿Por qué las transiciones de estado están en la entidad `Ticket` y no en el controller o en un
   servicio? ¿Qué problema concreto aparecería si estuvieran en el controller?
2. `Ticket.ChangeStatus` recibe `utcNow` como parámetro. ¿Qué alternativas hay para manejar la
   fecha actual y por qué se eligió esta para el dominio?
3. Domain no referencia ningún paquete, pero las entidades tienen un constructor privado vacío
   "para EF Core". ¿Eso rompe Clean Architecture?

---

## Etapa 2 — Persistencia y multi-tenancy

### Orden de lectura

1. [ADR 0002](decisions/0002-tenant-por-columna.md) y la sección 3 de ARCHITECTURE.md.
2. `Application/Abstractions/ITenantProvider.cs`.
3. `Infrastructure/Persistence/AppDbContext.cs`: primero `OnModelCreating` (filtros), después
   `EnforceTenantOnSave`. **Es el archivo central del proyecto.**
4. `Infrastructure/Persistence/Configurations/`: empezá por `TicketConfiguration.cs`.
5. `Infrastructure/Persistence/Migrations/`: abrí `..._InitialCreate.cs` y reconocé las tablas,
   claves foráneas e índices que salieron de las configuraciones.
6. `Api/Auth/HttpTenantProvider.cs` y `Infrastructure/Persistence/FixedTenantProvider.cs`.
7. `Infrastructure/DependencyInjection.cs`.
8. `tests/Tickecheck.IntegrationTests/IntegrationFixture.cs` y
   `Persistence/TenantIsolationTests.cs`. [ADR 0010](decisions/0010-tests-de-integracion.md).

### Conceptos

- ORM: qué es EF Core, `DbContext`, `DbSet`, *change tracker* y estados de una entidad
  (Added, Modified, Deleted, Unchanged).
- Fluent API (`IEntityTypeConfiguration`) vs. atributos.
- Migraciones: `dotnet ef migrations add`, `database update`, el *snapshot* del modelo.
- Global Query Filters e `IgnoreQueryFilters`.
- `SaveChanges` como punto único de control; interceptores como alternativa.
- Claves `Guid` generadas en el cliente y `ValueGeneratedNever`.
- `DeleteBehavior.Restrict` vs. `Cascade`.
- Inyección de dependencias: tiempos de vida *scoped*, *singleton*, *transient*.
- Reflexión (`MakeGenericMethod`) y por qué se usa una sola vez acá.
- Testcontainers y por qué no el proveedor InMemory.
- *Fail closed* vs. *fail open*.

### Preguntas de entrevista

1. Un desarrollador nuevo agrega la entidad `Attachment` y un endpoint para listarla. ¿Qué tiene
   que hacer para que quede aislada por empresa? ¿Qué pasa si se olvida?
2. Los Global Query Filters protegen las lecturas. ¿Por qué no alcanzan y qué ataque o bug
   concreto frena la validación en `SaveChanges`?
3. ¿Qué diferencias hay entre base por tenant, esquema por tenant y columna `TenantId`? ¿Cuándo
   elegirías cada una?

---

## Etapa 3 — Autenticación y roles

### Orden de lectura

1. [ADR 0003](decisions/0003-jwt-y-refresh-tokens.md).
2. `Application/Abstractions/`: `ITokenService.cs`, `IPasswordHasher.cs`, `IUserRepository.cs`,
   `IRefreshTokenRepository.cs`, `IUnitOfWork.cs`, `ICurrentUser.cs`.
3. `Application/Auth/`: `AppClaims.cs`, `Login.cs`, `AuthTokenIssuer.cs`, `RefreshSession.cs`,
   `Logout.cs`.
4. `Infrastructure/Auth/`: `JwtOptions.cs`, `JwtTokenService.cs`, `PasswordHasherAdapter.cs`.
5. `Infrastructure/Persistence/Repositories/UserRepository.cs` (los tres `IgnoringTenant`).
6. `Api/Program.cs`, de arriba hacia abajo. Es donde se arma todo.
7. `Api/Auth/Policies.cs`, `HttpCurrentUser.cs`, `Api/Controllers/ApiController.cs` y
   `AuthController.cs`. [ADR 0007](decisions/0007-autorizacion.md).
8. `Api/ErrorHandling/GlobalExceptionHandler.cs`, `Application/Common/Exceptions.cs` y
   [ADR 0006](decisions/0006-validacion-y-errores.md).
9. `Application/Tenants/CreateTenant.cs` y `Application/Users/CreateUser.cs`.
10. Tests: `Application.Tests/Auth/` y `IntegrationTests/Api/AuthTests.cs`.

### Conceptos

- Autenticación (quién sos) vs. autorización (qué podés hacer).
- JWT: header, payload, firma; claims; por qué no se cifra; HMAC-SHA256.
- Access token vs. refresh token; rotación; detección de reuso.
- Hash de passwords: salt, PBKDF2, por qué no SHA-256 a secas.
- Pipeline de middleware de ASP.NET Core y por qué importa el orden
  (`UseAuthentication` antes de `UseAuthorization`).
- Policies y `[Authorize]`; `[AllowAnonymous]`.
- Patrón Options (`IOptions<JwtOptions>`) y configuración por variables de entorno.
- ProblemDetails (RFC 9457) e `IExceptionHandler`.
- FluentValidation.
- Constructores primarios de C# 12.
- Logging estructurado con Serilog.
- Códigos HTTP: 400, 401, 403, 404, 409.

### Preguntas de entrevista

1. Si te roban el access token, ¿qué puede hacer el atacante y por cuánto tiempo? ¿Y si roban el
   refresh token? ¿Cómo lo detecta el sistema?
2. ¿Por qué el password se guarda con PBKDF2 y salt, pero el refresh token con SHA-256 sin salt?
3. El login busca al usuario con `IgnoreQueryFilters`. ¿Por qué es necesario y por qué no es un
   agujero en el aislamiento?

---

## Etapa 4 — Casos de uso de tickets

### Orden de lectura

1. [ADR 0004](decisions/0004-sin-mediatr.md) y [ADR 0005](decisions/0005-repositorios-y-unit-of-work.md).
2. `Application/Abstractions/ITicketRepository.cs`.
3. `Application/Tickets/TicketDto.cs` y `Application/Common/PagedResult.cs`.
4. `Application/Tickets/CreateTicket.cs`: comando, validador y handler. Todos los demás siguen
   esta forma.
5. `Application/Common/AccessRules.cs`.
6. `GetTicketById.cs`, `SearchTickets.cs`, `UpdateTicket.cs`, `DeleteTicket.cs`.
7. `AssignTicket.cs`, `ChangeTicketStatus.cs`, `ReopenTicket.cs`, `ChangeTicketPriority.cs`.
8. `Infrastructure/Persistence/Repositories/TicketRepository.cs`: `SearchAsync`, `ProjectToDto`
   y `ApplySort`.
9. `Api/Controllers/TicketsController.cs`.
10. `Application/DependencyInjection.cs`.
11. Tests: `Application.Tests/Tickets/TicketHandlerTests.cs` y
    `IntegrationTests/Api/TicketFlowTests.cs`, `TenantIsolationApiTests.cs`,
    `RoleAuthorizationTests.cs`.

### Conceptos

- Caso de uso / *application service*; comando vs. consulta.
- DTO vs. entidad: por qué no se devuelve la entidad.
- Repositorio y Unit of Work.
- `IQueryable` y ejecución diferida; LINQ que se traduce a SQL.
- Proyección (`Select`), `AsNoTracking`, *joins* en sintaxis de consulta, *left join*.
- Paginación con `Skip`/`Take` y por qué hace falta un orden estable.
- Lista blanca de campos para ordenar.
- `async`/`await` y `CancellationToken`.
- Moq: `Setup`, `ReturnsAsync`, `Verify`, `It.Is`.
- Controllers finos; `[FromServices]`, `[FromQuery]`; `CreatedAtAction`.
- Diseño REST: recursos, verbos y sub-recursos (`/tickets/{id}/assignee`).

### Preguntas de entrevista

1. Seguí en el código qué pasa cuando un agente pide `GET /api/tickets?status=Open&page=2`.
   ¿Cuántas consultas llegan a la base y qué `WHERE` tiene cada una?
2. ¿Por qué los handlers reciben `ITicketRepository` y no `AppDbContext`? Da un argumento a favor
   de cada opción.
3. Un Requester pide por Id un ticket de un compañero de su misma empresa. ¿Qué código lo frena,
   qué status devuelve y por qué ese y no 403?

---

## Etapa 5 — Comentarios, auditoría y SLA

### Orden de lectura

1. `Application/Comments/AddComment.cs` y `GetComments.cs`.
2. `Infrastructure/Persistence/Repositories/CommentRepository.cs`.
3. `Application/Tickets/GetTicketHistory.cs` y `TicketRepository.GetHistoryAsync`.
4. De vuelta en `Domain/Tickets/Ticket.cs`: `AddHistory`, `IsOverdue`, `ChangePriority`.
5. La regla de vencido repetida en SQL: `TicketRepository.ProjectToDto` y `GetStatsAsync`.
6. Acciones de comentarios e historial en `TicketsController.cs`.
7. Tests: `Application.Tests/Comments/CommentHandlerTests.cs` y, en
   `RoleAuthorizationTests.cs`, `Requester_never_sees_internal_comments_and_cannot_write_them`.

### Conceptos

- Filtrar datos en el servidor vs. esconderlos en la interfaz.
- Auditoría: registros inmutables, quién, qué y cuándo.
- Agregado y raíz de agregado (`Ticket` es dueño de su `TicketHistory`).
- Valor calculado vs. valor guardado (`IsOverdue`).
- Una misma regla en C# y en SQL: costo y cómo se controla.
- Campo de respaldo (*backing field*) y cómo EF Core llena una colección privada.

### Preguntas de entrevista

1. ¿Cómo garantiza el sistema que un Requester nunca recibe un comentario interno? Nombrá cada
   lugar donde se aplica la regla.
2. "Vencido" no se guarda en la base: se calcula. ¿Qué ventajas y desventajas tiene frente a un
   job que marque los tickets vencidos cada minuto?
3. ¿Qué impide que alguien cambie el estado de un ticket sin dejar registro en el historial?

---

## Etapa 6 — SignalR y dashboard

### Orden de lectura

1. [ADR 0008](decisions/0008-signalr-grupos.md).
2. `Application/Abstractions/ITicketNotifier.cs`.
3. `Api/Hubs/NotificationsHub.cs` y `SignalRTicketNotifier.cs`.
4. En `Api/Program.cs`: `AddSignalR`, `MapHub` y el evento `OnMessageReceived` del JWT.
5. Dónde se dispara: `AssignTicket.cs` y `AddComment.cs`.
6. `Application/Dashboard/GetDashboardStats.cs` y `TicketRepository.GetStatsAsync`.
7. Tests: `IntegrationTests/Api/NotificationTests.cs`.

### Conceptos

- HTTP pedido-respuesta vs. conexión persistente; WebSockets y *long polling*.
- Hub, conexión, grupos; `IHubContext` para enviar desde fuera del hub.
- Por qué el servidor asigna los grupos y no el cliente.
- Token en la query string para WebSockets.
- Agregaciones en SQL: `GROUP BY`, `COUNT`, `AVG`; `EF.Functions`.
- Inversión de dependencias aplicada a las notificaciones.
- *Backplane* para escalar SignalR a varias instancias.

### Preguntas de entrevista

1. ¿Por qué no alcanza con un grupo por empresa? ¿Qué dato se filtraría?
2. Un cliente malicioso modifica el JavaScript para unirse al grupo de otra empresa. ¿Por qué no
   puede?
3. Si mañana la API corre en tres instancias detrás de un balanceador, ¿qué deja de funcionar en
   las notificaciones y cómo se arregla?

---

## Etapa 7 — Frontend Blazor

### Orden de lectura

1. [ADR 0009](decisions/0009-frontend-blazor.md).
2. `Web/wwwroot/index.html` y `Web/Program.cs`.
3. `Web/App.razor`, `Layout/MainLayout.razor` y `Layout/RedirectToLogin.razor`.
4. `Web/Models/ApiModels.cs`.
5. `Web/Services/`: `TokenStore.cs`, `JwtAuthStateProvider.cs`, `AuthService.cs`,
   `ApiClient.cs`, `NotificationService.cs`, `Display.cs`.
6. `Web/Pages/Login.razor` (la más simple).
7. `Pages/Tickets.razor` (tabla con datos del servidor).
8. `Pages/NewTicket.razor` y `Pages/TicketDetail.razor`.
9. `Pages/Dashboard.razor`, `Users.razor`, `Categories.razor`, `Tenants.razor`.
10. En `Api/Program.cs`: `UseBlazorFrameworkFiles` y `MapFallbackToFile`.

### Conceptos

- SPA; WebAssembly; diferencias entre Blazor WebAssembly y Blazor Server.
- Componentes Razor: `@page`, `@code`, parámetros, `@bind-Value`, `EventCallback`.
- Ciclo de vida: `OnInitializedAsync`, `StateHasChanged`, `InvokeAsync`, `IDisposable`.
- Inyección de dependencias en componentes (`@inject`).
- `AuthenticationStateProvider`, `AuthorizeView`, `[Authorize]` en páginas y por qué eso no es
  seguridad.
- Interoperabilidad con JavaScript (`IJSRuntime`) y `localStorage`.
- Leer un JWT en el cliente sin verificarlo.
- `SemaphoreSlim` para serializar el refresh.
- MudBlazor: `MudTable` con `ServerData`, `MudSelect`, `ISnackbar`.
- XSS y por qué importa dónde se guardan los tokens.
- Enrutado en el cliente y el *fallback* a `index.html`.

### Preguntas de entrevista

1. El menú esconde "Users" a los agentes. ¿Qué pasa si un agente escribe `/users` en la barra de
   direcciones, y qué pasa si llama a `POST /api/users` con su token?
2. Una página dispara tres requests a la vez y el access token acaba de vencer. ¿Qué problema
   aparece con la rotación de refresh tokens y cómo lo evita `AuthService`?
3. ¿Qué riesgo tiene guardar los tokens en `localStorage` y qué alternativa hay?

---

## Etapa 8 — Docker, CI/CD y deploy

### Orden de lectura

1. `global.json` y `.config/dotnet-tools.json`.
2. `docker-compose.yml`, `.env.example` y `Dockerfile`.
3. `Infrastructure/Persistence/Seed/DbSeeder.cs` y `PrepareDatabaseAsync` en `Api/Program.cs`.
4. `Api/appsettings.json` y `appsettings.Development.json`.
5. `coverlet.runsettings`.
6. `.github/workflows/ci.yml` y `deploy.yml`.
7. [DEPLOY.md](DEPLOY.md).

### Conceptos

- Imagen vs. contenedor; Dockerfile multi-etapa; volúmenes; redes de Compose; healthcheck.
- Jerarquía de configuración de ASP.NET Core: `appsettings.json` → por ambiente → variables de
  entorno. Convención `Seccion__Clave`.
- Secretos fuera del repositorio.
- Integración continua: build y tests en cada push; cobertura y umbral mínimo.
- Cobertura de líneas vs. de ramas, y qué **no** dice un porcentaje alto.
- Despliegue continuo; perfil de publicación; variables y secretos de GitHub Actions.
- Migrar al arrancar vs. como paso del deploy.
- PaaS: Azure App Service, Azure SQL serverless, arranque en frío.

### Preguntas de entrevista

1. ¿Dónde vive la clave de firma de los JWT en desarrollo, en Docker, en CI y en Azure? ¿Qué pasa
   si la API arranca sin ella?
2. La API aplica las migraciones al arrancar. ¿Qué problema trae eso con varias instancias o con
   una migración que tarda diez minutos?
3. La cobertura es del 97 %. ¿Eso significa que no hay bugs? Nombrá algo importante del proyecto
   que ese número no mide.

---

## Glosario

| Término | Significado en este proyecto |
|---|---|
| Tenant | Una empresa cliente. Sus datos están aislados de los de las demás. |
| Multi-tenant | Una sola instalación atiende a varias empresas. |
| Invariante | Condición que una entidad garantiza siempre (un SuperAdmin no tiene tenant). |
| Entidad | Objeto con identidad propia (Id) y reglas (`Ticket`). |
| DTO | Objeto plano para transportar datos hacia afuera (`TicketDto`). |
| Caso de uso / handler | Una operación completa del sistema (`AssignTicketHandler`). |
| Repositorio | Abstracción para leer y guardar un tipo de entidad. |
| Unit of Work | Confirma en una transacción todos los cambios de una operación. |
| ORM | Librería que traduce objetos a tablas y LINQ a SQL (EF Core). |
| Migración | Archivo versionado con un cambio de esquema de la base. |
| Global Query Filter | Condición que EF Core agrega a todas las consultas de una entidad. |
| Claim | Dato sobre el usuario dentro del token (`role`, `tenant_id`). |
| JWT | Token firmado que contiene claims y se verifica sin consultar la base. |
| Refresh token | Token de vida larga para pedir un access token nuevo. |
| Rotación | Cada refresh token se usa una vez y se reemplaza por otro. |
| Policy | Regla de autorización con nombre (`AgentOrAbove`). |
| Middleware | Pieza del pipeline por donde pasa cada request. |
| ProblemDetails | Formato JSON estándar para errores HTTP. |
| Hub | Punto de conexión de SignalR. |
| SLA | Plazo máximo para resolver un ticket según su prioridad. |
| Auditoría | Registro inmutable de quién cambió qué y cuándo (`TicketHistory`). |
| Seed | Datos iniciales de demostración. |
| Mock | Objeto simulado que reemplaza una dependencia en un test. |
| Fixture | Recurso compartido entre tests (el contenedor de SQL Server). |
| ADR | Registro corto de una decisión de arquitectura y sus alternativas. |
