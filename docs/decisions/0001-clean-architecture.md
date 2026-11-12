# ADR 0001 — Clean Architecture en cuatro proyectos

**Estado:** aceptada · **Fase:** 1

## Contexto

El sistema tiene reglas de negocio propias (transiciones de estado, SLA, auditoría) y un requisito
crítico de aislamiento entre empresas. Hace falta poder probar esas reglas sin base de datos ni
servidor web, y poder explicar dónde vive cada cosa.

## Decisión

La solución se divide en cuatro proyectos y las referencias apuntan solo hacia adentro:

```
Api ──► Application ──► Domain
 │           ▲
 └──► Infrastructure
```

| Proyecto | Qué contiene | Qué puede referenciar |
|---|---|---|
| Domain | Entidades, enums, reglas de negocio, excepciones de dominio | Nada (ni paquetes NuGet) |
| Application | Casos de uso (handlers) e interfaces de lo que necesitan | Domain |
| Infrastructure | EF Core, repositorios, generación de JWT | Application |
| Api | Controllers, autenticación, DI, manejo de errores | Application e Infrastructure |

Api referencia Infrastructure únicamente para registrar las implementaciones en el contenedor de
dependencias. Los controllers solo usan tipos de Application.

El compilador hace cumplir la regla: como Domain no referencia a nadie, es imposible usar
`DbContext` o `HttpContext` dentro de una entidad, aunque alguien lo intente.

## Alternativas descartadas

- **Un solo proyecto con carpetas.** Es lo más simple, pero las reglas de dependencia quedan en
  manos de la disciplina del equipo: nada impide que un controller use el `DbContext` directo.
- **Tres capas clásicas (UI → Negocio → Datos).** La capa de negocio depende de la de datos, así
  que las reglas no se pueden probar sin base de datos y el modelo termina moldeado por las tablas.
- **Vertical Slice Architecture.** Agrupa por funcionalidad en vez de por capa. Es una opción
  válida, pero el aislamiento multi-tenant es transversal a todas las funcionalidades y se explica
  mejor como una pieza de infraestructura única.

## Consecuencias

- Los tests del dominio corren en milisegundos y no necesitan mocks.
- Hay más proyectos y más archivos que en una solución de una sola capa. Agregar un caso de uso
  toca varios proyectos.
- Algunas concesiones a EF Core llegan igual al dominio: constructores privados sin parámetros y
  setters privados. Se aceptan porque no agregan ninguna dependencia de compilación.
