# ADR 0009 — Frontend Blazor WebAssembly servido por la API

**Estado:** aceptada · **Fase:** 7

## Contexto

El frontend es Blazor WebAssembly con MudBlazor. Hay que decidir dónde se aloja, cómo comparte
tipos con la API y dónde guarda los tokens.

## Decisión

**1. La API sirve el frontend.** El proyecto Api referencia a Web y devuelve sus archivos
estáticos (`UseBlazorFrameworkFiles`, `MapFallbackToFile("index.html")`). Es un solo sitio y un
solo origen: no hace falta CORS, SignalR conecta sin configuración extra y se despliega un único
App Service.

**2. El frontend tiene sus propias copias de los DTOs** (`Web/Models/ApiModels.cs`). No
referencia a Application ni a Domain: es un cliente que solo conoce el contrato JSON, y nada del
código del servidor se descarga al navegador.

**3. Los tokens se guardan en `localStorage`.** `ApiClient` agrega el access token a cada
request, lo renueva antes de que venza y reintenta una vez si recibe 401.

**4. El frontend no repite reglas de negocio.** Ofrece todos los estados y muestra el mensaje de
la API cuando una transición no es válida. Los menús se esconden según el rol, pero la seguridad
está en la API.

## Alternativas descartadas

- **Azure Static Web Apps para el frontend.** Gratis y con CDN, pero son dos despliegues y hay
  que configurar CORS y la URL de la API por ambiente.
- **Un proyecto compartido de contratos** entre Api y Web. Evita duplicar los DTOs y el
  compilador avisa si el contrato cambia. Se descartó para no sumar un quinto proyecto; es la
  primera mejora que haría si el frontend creciera.
- **Refresh token en cookie HttpOnly.** Es más seguro: JavaScript no puede leerlo, así que un XSS
  no se lo lleva. Requiere protección CSRF y manejo de `SameSite`. Se dejó `localStorage` por
  simplicidad y queda anotado como limitación.
- **Blazor Server.** No necesita API, pero mantiene una conexión por usuario y el estado en el
  servidor; el proyecto buscaba mostrar una API consumida por un cliente separado.

## Consecuencias

- La primera carga descarga el runtime de .NET en WebAssembly (varios MB).
- Si cambia un DTO en la API hay que actualizar la copia del frontend a mano; el compilador no lo
  detecta.
- El frontend no tiene tests automáticos. Se verificó manualmente y con un script de navegador
  durante el desarrollo.
