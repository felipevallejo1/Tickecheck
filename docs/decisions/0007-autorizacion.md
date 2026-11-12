# ADR 0007 — Autorización: policies por rol y reglas de datos centralizadas

**Estado:** aceptada · **Fase:** 3–5

## Contexto

Hay cuatro roles. Además de "quién puede llamar a qué endpoint", hay reglas que dependen de los
datos: un Requester solo ve sus propios tickets y nunca ve comentarios internos.

## Decisión

Son dos preguntas distintas y se resuelven en dos lugares.

**1. ¿Este rol puede llamar a este endpoint? → Policies** (`Api/Auth/Policies.cs`)

| Policy | Roles | Ejemplos |
|---|---|---|
| `SuperAdminOnly` | SuperAdmin | Alta y listado de empresas |
| `TenantAdminOnly` | TenantAdmin | Alta de usuarios y categorías, borrar tickets |
| `AgentOrAbove` | Agent, TenantAdmin | Asignar, cambiar estado y prioridad, dashboard |
| `TenantMember` | Requester, Agent, TenantAdmin | Crear y ver tickets, comentar, reabrir |

Se declaran con `[Authorize(Policy = ...)]` sobre el controller o la acción. La clase base
`ApiController` tiene `[Authorize]`, así que un endpoint nuevo es privado salvo que diga
`[AllowAnonymous]`.

**2. ¿Qué filas ve este usuario? → `AccessRules`** (`Application/Common/AccessRules.cs`)

Una policy responde sí o no antes de ejecutar la acción; no puede filtrar un listado. Esas reglas
están en una sola clase estática con tres métodos, que los handlers llaman:

- `EnsureCanAccessTicket`: un Requester que pide un ticket ajeno recibe 404.
- `GetRequesterScope`: limita los listados de un Requester a sus tickets.
- `CanSeeInternalComments`: falso para Requester; el filtro se aplica en la consulta SQL.

**El SuperAdmin no entra a los datos de las empresas.** No está en `TenantMember`, su token no
tiene `tenant_id` y por lo tanto los filtros no le devuelven nada. Solo administra empresas.

## Alternativas descartadas

- **`if (user.Role == ...)` dentro de cada caso de uso.** Las reglas quedan repartidas y es fácil
  que dos handlers las apliquen distinto.
- **Autorización basada en recursos de ASP.NET Core** (`IAuthorizationService` con
  `AuthorizationHandler<TRequirement, Ticket>`). Es el mecanismo oficial para "¿puede este usuario
  con este ticket?", pero vive en la capa web y no resuelve el filtrado de listados, así que
  habría reglas en dos lugares igual.
- **Un endpoint único de cambio de estado con reglas por rol.** Se prefirió un endpoint aparte
  para reabrir (`POST /tickets/{id}/reopen`), abierto al Requester, y dejar el cambio general de
  estado solo para staff con una policy simple.

## Consecuencias

- Mirando un controller se ve quién puede llamar a cada acción.
- Las reglas de visibilidad se prueban sin HTTP, en los tests unitarios de los handlers.
- El frontend esconde menús y botones según el rol, pero eso es solo comodidad: la API vuelve a
  comprobar todo.
