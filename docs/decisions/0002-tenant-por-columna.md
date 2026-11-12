# ADR 0002 — Multi-tenancy con base compartida y columna TenantId

**Estado:** aceptada · **Fase:** 2

## Contexto

Varias empresas usan la misma instalación y ninguna puede ver datos de otra. Hay tres modelos
clásicos: una base por empresa, un esquema por empresa, o tablas compartidas con una columna que
indica a qué empresa pertenece cada fila.

## Decisión

Base y esquema compartidos. Cada tabla con datos de una empresa tiene una columna `TenantId`.
El aislamiento se aplica en un solo lugar, `AppDbContext`, con dos mecanismos:

1. **Lectura — Global Query Filters.** A toda entidad que implementa `ITenantEntity` se le agrega
   automáticamente `WHERE TenantId = @tenantActual`. El tenant sale del claim `tenant_id` del JWT
   a través de `ITenantProvider`. Los repositorios no escriben ese filtro nunca.
2. **Escritura — validación en `SaveChanges`.** Antes de guardar, el contexto asigna el `TenantId`
   a las filas nuevas y lanza `TenantViolationException` si algo pertenece a otro tenant.

Tres reglas completan el diseño:

- **Falla cerrado:** sin tenant en el contexto, las consultas no devuelven nada y los guardados
  de datos de tenant fallan. Un bug produce una pantalla vacía, no una fuga de datos.
- **404 y no 403** al pedir un Id de otra empresa: no se confirma que el Id existe.
- **`IgnoreQueryFilters` está en un solo archivo**, `UserRepository`, en tres métodos con el
  sufijo `IgnoringTenant`: buscar usuario por email (login), buscar usuario por Id (refresh) y
  verificar si un email ya existe (los emails son únicos en toda la plataforma). Login y refresh
  son anónimos: todavía no hay JWT del cual sacar el tenant.

## Alternativas descartadas

- **Una base por empresa.** Es el aislamiento más fuerte y permite backup por cliente, pero cada
  empresa nueva implica crear una base, y las migraciones hay que correrlas N veces. En las capas
  gratuitas de Azure directamente no entra.
- **Un esquema por empresa.** Punto intermedio con el mismo costo operativo de migraciones y
  soporte limitado en EF Core (el esquema es parte del modelo compilado).
- **Filtrar a mano en cada consulta.** Funciona hasta el día que alguien se olvida un `Where`.
- **Row-Level Security de SQL Server.** Protege incluso de SQL escrito a mano. Se descartó por
  simplicidad, pero es la mejora natural si el sistema creciera (defensa en profundidad).

## Consecuencias

- Agregar una entidad nueva con `ITenantEntity` la protege sin tocar nada más.
- Todo el aislamiento depende de código de aplicación: una consulta SQL cruda (`FromSqlRaw`) o un
  `IgnoreQueryFilters` mal puesto lo saltean. Por eso hay tests de integración que lo verifican
  contra SQL Server real, a nivel `DbContext` y a nivel HTTP.
- Un vecino ruidoso afecta a todos: las empresas comparten la misma base y sus recursos.
- `User` es un caso especial: su `TenantId` es nullable (el SuperAdmin no tiene empresa), así que
  no implementa `ITenantEntity` y recibe el mismo filtro de forma explícita.
