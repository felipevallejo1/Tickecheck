# ADR 0004 — Casos de uso como handlers simples, sin MediatR

**Estado:** aceptada · **Fase:** 4

## Contexto

Cada operación del sistema (crear ticket, asignar, comentar) es un caso de uso. Hay que decidir
cómo los llama el controller.

## Decisión

Cada caso de uso es una clase con un método `HandleAsync`, registrada en el contenedor de
dependencias e inyectada directamente en el controller:

```csharp
public async Task<ActionResult<TicketDto>> Create(
    CreateTicketCommand command,
    [FromServices] CreateTicketHandler handler,
    CancellationToken cancellationToken)
{
    var ticket = await handler.HandleAsync(command, cancellationToken);
    return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
}
```

El comando, su validador y su handler viven en el mismo archivo (`Tickets/CreateTicket.cs`), y
todos los handlers se registran uno por uno en `Application/DependencyInjection.cs`.

## Alternativas descartadas

- **MediatR.** El controller llamaría `mediator.Send(command)` y la librería buscaría el handler.
  Aporta *pipeline behaviors* (validación, logging o transacciones aplicados a todos los casos de
  uso). Se descartó porque agrega una indirección: con "ir a la definición" ya no se llega del
  controller al código que se ejecuta. Además su licencia pasó a ser comercial.
- **Un servicio grande por entidad** (`TicketService` con veinte métodos). Termina con un
  constructor de diez dependencias, de las cuales cada método usa dos.

## Consecuencias

- El flujo se sigue con F12: controller → handler → repositorio.
- Cada handler declara exactamente las dependencias que usa, y sus tests solo simulan esas.
- Sin pipeline, lo transversal se repite: cada handler llama a su validador
  (`new XValidator().ValidateAndThrow(command)`) y a `SaveChangesAsync`. Con veintitantos casos
  de uso es aceptable; con cientos convendría reconsiderarlo.
