# ADR 0006 — FluentValidation y errores centralizados con ProblemDetails

**Estado:** aceptada · **Fase:** 3

## Contexto

Hay que validar la entrada de cada caso de uso y devolver los errores con un formato único.

## Decisión

**Validación de entrada con FluentValidation.** Cada comando tiene su validador en el mismo
archivo, y el handler lo ejecuta en su primera línea.

**Tres niveles de reglas, cada uno en su lugar:**

| Nivel | Ejemplo | Dónde vive | Resultado |
|---|---|---|---|
| Forma del dato | "el título es obligatorio y tiene hasta 200 caracteres" | Validador | 400 |
| Depende de datos | "la categoría existe", "el email no está en uso" | Handler | 400 / 409 |
| Regla de negocio | "no se pasa de Open a Resolved" | Entidad de dominio | 409 |

**Manejo de errores centralizado.** Los casos de uso lanzan excepciones y un único
`GlobalExceptionHandler` las convierte en respuestas ProblemDetails (RFC 9457):

| Excepción | HTTP |
|---|---|
| `ValidationException` | 400, con los errores por campo |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException`, `DomainException` | 409 |
| Cualquier otra | 500, se registra completa en el log y al cliente va un mensaje genérico |

## Alternativas descartadas

- **Validación manual con `if`.** Sin dependencias, pero mezcla validación y lógica, y el formato
  de los errores hay que armarlo a mano.
- **Data Annotations (`[Required]`).** Corren en el model binding, antes del caso de uso: la
  validación quedaría en la capa Api y un handler llamado desde otro lado no validaría nada.
- **Validación automática de FluentValidation en el pipeline de ASP.NET.** Está deprecada.
- **Result pattern** (devolver `Result<T>` en vez de lanzar). Evita usar excepciones para flujos
  esperados, pero cada handler y cada controller tendrían que inspeccionar y traducir resultados.

## Consecuencias

- Ningún controller tiene `try/catch` ni arma respuestas de error.
- Las excepciones para errores esperados tienen un costo de rendimiento que acá es irrelevante.
- Cada handler tiene que acordarse de llamar a su validador; no hay nada que lo obligue.
