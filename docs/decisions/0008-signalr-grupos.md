# ADR 0008 — Notificaciones con SignalR y grupos decididos por el servidor

**Estado:** aceptada · **Fase:** 6

## Contexto

Hay que avisar en tiempo real cuando se asigna un ticket o se agrega un comentario, sin que un
evento llegue a otra empresa ni un comentario interno llegue al solicitante.

## Decisión

Un hub (`NotificationsHub`) que solo emite: los clientes no pueden invocar métodos ni elegir
grupos. Al conectarse, el servidor lee los claims del JWT y agrega la conexión a:

| Grupo | Quién entra | Qué recibe |
|---|---|---|
| `user:{userId}` | Todo usuario conectado | Ticket asignado a él; comentario público en su ticket |
| `tenant:{tenantId}:staff` | Agent y TenantAdmin de esa empresa | Todo comentario de la empresa, incluidos los internos |

Destino de cada evento:

- **Ticket asignado** → `user:{agente}`.
- **Comentario público** → `tenant:{id}:staff` y `user:{solicitante}`.
- **Comentario interno** → solo `tenant:{id}:staff`.

El mensaje lleva lo mínimo (Id y título del ticket). Para ver el contenido, el cliente llama a la
API, que vuelve a aplicar todas las reglas de acceso.

Los casos de uso dependen de la interfaz `ITicketNotifier` (Application); la implementación con
SignalR vive en Api. La notificación se envía después de `SaveChanges`.

## Alternativas descartadas

- **Un solo grupo por empresa (`tenant:{id}`).** Era la idea inicial. Se descartó porque el
  solicitante está en ese grupo y recibiría el aviso de un comentario interno.
- **Que el cliente se suscriba con un método `JoinTenant(tenantId)`.** Bastaría con mandar el Id
  de otra empresa para escuchar sus eventos.
- **Polling desde el frontend.** Más simple, pero con demora y carga constante en la API.
- **Azure SignalR Service.** Necesario con varias instancias de la API; no con una sola.

## Consecuencias

- El aislamiento en tiempo real depende de los mismos claims firmados que el resto de la API, y
  hay tests de integración con conexiones reales que lo verifican.
- Los navegadores no pueden mandar el header `Authorization` al abrir un WebSocket, así que el
  token viaja en la query string. La API lo acepta de ahí únicamente para la ruta del hub.
- Con más de una instancia de la API haría falta un *backplane* (Redis o Azure SignalR): los
  grupos viven en la memoria de cada proceso.
- Si la notificación falla después de guardar, el cambio queda hecho y el aviso se pierde.
