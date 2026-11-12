# Deploy en Azure

> **Estado:** los workflows están escritos, pero el deploy todavía no se ejecutó nunca.
> Los comandos de esta guía no fueron probados contra una suscripción real: revisalos con
> `az <comando> --help` antes de correrlos, porque los nombres de planes y opciones cambian.

## Qué se despliega

Un solo App Service que sirve la API y el frontend Blazor (son el mismo sitio), más una base
Azure SQL.

| Recurso | Plan sugerido | Costo |
|---|---|---|
| App Service (Linux, .NET 9) | F1 | Gratis. Límite de 60 minutos de CPU por día y se duerme sin tráfico. |
| Azure SQL Database | Oferta gratuita serverless | Gratis hasta el límite mensual; después se pausa. |

## 1. Crear los recursos

Requiere [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) y `az login`.

```bash
RG=tickecheck-rg
LOCATION=eastus2
APP=tickecheck-<tu-sufijo>        # tiene que ser único en todo Azure
SQL_SERVER=tickecheck-sql-<tu-sufijo>
SQL_ADMIN=tickecheckadmin
SQL_PASSWORD='<una-password-fuerte>'

az group create --name $RG --location $LOCATION

# Base de datos
az sql server create --name $SQL_SERVER --resource-group $RG --location $LOCATION \
  --admin-user $SQL_ADMIN --admin-password "$SQL_PASSWORD"

az sql db create --name Tickecheck --server $SQL_SERVER --resource-group $RG \
  --edition GeneralPurpose --family Gen5 --capacity 2 --compute-model Serverless \
  --use-free-limit --free-limit-exhaustion-behavior AutoPause

# Permite que los servicios de Azure (el App Service) lleguen a la base.
az sql server firewall-rule create --server $SQL_SERVER --resource-group $RG \
  --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

# Aplicación
az appservice plan create --name tickecheck-plan --resource-group $RG --sku F1 --is-linux
az webapp create --name $APP --resource-group $RG --plan tickecheck-plan --runtime "DOTNETCORE:9.0"

# SignalR usa WebSockets; sin esto cae a long polling.
az webapp config set --name $APP --resource-group $RG --web-sockets-enabled true
az webapp update --name $APP --resource-group $RG --https-only true
```

## 2. Configurar la aplicación

Los secretos van como configuración del App Service, nunca en el repositorio. ASP.NET Core lee
las variables con `__` como separador de secciones (`Jwt__SigningKey` es `Jwt:SigningKey`).

```bash
az webapp config appsettings set --name $APP --resource-group $RG --settings \
  Jwt__SigningKey="$(openssl rand -base64 48)" \
  Database__MigrateOnStartup=true \
  Database__SeedDemoData=true

az webapp config connection-string set --name $APP --resource-group $RG \
  --connection-string-type SQLAzure --settings \
  Default="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=Tickecheck;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;"
```

`Database__SeedDemoData=true` crea las cuentas de demo con la contraseña pública `Demo123!`.
Sirve para una demo de portfolio; no lo actives en un sistema con datos reales.

## 3. Conectar GitHub Actions

1. Descargá el perfil de publicación:
   `az webapp deployment list-publishing-profiles --name $APP --resource-group $RG --xml`
2. En GitHub, en *Settings → Secrets and variables → Actions*:
   - Secret `AZURE_WEBAPP_PUBLISH_PROFILE`: el XML completo del paso anterior.
   - Variable `AZURE_WEBAPP_NAME`: el valor de `$APP`.
3. Hacé push a `main`. Cuando el workflow **CI** termina bien, corre **Deploy**.

Mientras la variable `AZURE_WEBAPP_NAME` no exista, el workflow de deploy se saltea solo.

## 4. Verificar

Abrí `https://$APP.azurewebsites.net/login` e ingresá con una cuenta de demo. La primera carga
tarda: el plan F1 arranca en frío y la base serverless puede estar pausada.

## Borrar todo

```bash
az group delete --name $RG --yes
```
