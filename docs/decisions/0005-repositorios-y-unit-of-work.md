# ADR 0005 — Repositorios por agregado y Unit of Work

**Estado:** aceptada · **Fase:** 4

## Contexto

Application no puede referenciar Infrastructure, así que los casos de uso necesitan una
abstracción para leer y guardar datos. Además tienen que poder probarse con Moq.

## Decisión

- Una interfaz de repositorio por agregado en `Application/Abstractions`
  (`ITicketRepository`, `IUserRepository`, ...), con métodos que nombran lo que el negocio
  necesita (`SearchAsync`, `GetStatsAsync`, `FindByEmailIgnoringTenantAsync`).
- Los repositorios solo registran cambios. El handler decide cuándo confirmar con
  `IUnitOfWork.SaveChangesAsync()`, y todo lo que cambió en el request se guarda en una
  transacción.
- `AppDbContext` implementa `IUnitOfWork` directamente: ya es una unidad de trabajo.
- **Lecturas con proyección:** las consultas de listado devuelven DTOs armados en SQL
  (`Select`), no entidades. Las escrituras cargan la entidad y usan sus métodos de dominio.

## Alternativas descartadas

- **Usar `DbContext` en los handlers** (a través de una interfaz `IAppDbContext` con `DbSet`).
  Es menos código y es una postura válida: EF Core ya es un repositorio. Se descartó porque
  simular un `DbSet` con Moq es engorroso (hay que simular `IQueryable` asíncrono) y los tests de
  los handlers eran un requisito.
- **Repositorio genérico `IRepository<T>`.** Promete reutilización, pero termina exponiendo
  `IQueryable` (y la capa deja de aislar nada) o siendo demasiado pobre para consultas reales.

## Consecuencias

- Los tests de un handler simulan dos o tres métodos con nombre claro.
- Hay más archivos: interfaz + implementación por agregado.
- Las reglas "ticket vencido" quedaron escritas dos veces: en `Ticket.IsOverdue` (C#) y en
  `TicketRepository` (para que corra como SQL). Están comentadas en ambos lados; un test de
  integración verifica que coinciden con los datos de demo.
