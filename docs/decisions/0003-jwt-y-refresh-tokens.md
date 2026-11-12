# ADR 0003 — JWT de vida corta y refresh tokens con rotación

**Estado:** aceptada · **Fase:** 3

## Contexto

El frontend es una SPA (Blazor WebAssembly) que consume una API. Hace falta identificar al
usuario, su rol y su empresa en cada request.

## Decisión

- **Access token:** JWT firmado con HMAC-SHA256, válido 15 minutos. Lleva los claims `sub`,
  `email`, `name`, `role` y `tenant_id`. La API no lo guarda: verifica la firma y confía en su
  contenido.
- **Refresh token:** 64 bytes aleatorios, válido 7 días. En la base solo se guarda su hash.
- **Rotación:** cada uso del refresh token lo revoca y entrega uno nuevo.
- **Detección de reuso:** si llega un refresh token ya revocado, se revocan todas las sesiones
  activas de ese usuario. El cliente legítimo siempre usa el último token, así que un token viejo
  que reaparece indica robo.
- **Logout:** revoca el refresh token. El access token sigue siendo válido hasta que expira.
- **Passwords:** `PasswordHasher<T>` de ASP.NET Core Identity (PBKDF2 con salt aleatorio), sin
  el resto de Identity.

### Por qué el refresh token se hashea con SHA-256 y no con el hasher de passwords

Un password es corto y predecible: necesita salt y un hash lento. Un refresh token ya es largo y
aleatorio, así que no se puede adivinar ni atacar con diccionario. Además hay que **buscarlo** en
la base: con salt, el mismo token daría un hash distinto cada vez y no se podría encontrar la fila.

## Alternativas descartadas

- **Solo un JWT de vida larga.** Simple, pero un token robado sirve durante días y no hay forma
  de cerrar la sesión.
- **Sesiones en el servidor con cookie.** Permite revocar al instante, pero obliga a consultar un
  almacén de sesiones en cada request y complica SignalR y clientes que no sean navegador.
- **ASP.NET Core Identity completo.** Trae tablas, managers y flujos (confirmación de email,
  2FA, lockout) que este proyecto no usa. Entender qué hace cada pieza valía más que tenerlas.
- **Un proveedor externo (Entra ID, Auth0).** Es lo que usaría en producción, pero esconde
  justamente lo que este proyecto quiere mostrar.

## Consecuencias

- Verificar un request no toca la base de datos.
- Un access token robado sirve como máximo 15 minutos. Tampoco refleja cambios de rol hasta que
  se renueva.
- Dos refresh simultáneos con el mismo token disparan la detección de reuso. El frontend lo evita
  serializando los refresh con un semáforo (`AuthService`).
- El login no tiene límite de intentos ni bloqueo de cuenta (ver "Limitaciones" en el README).
