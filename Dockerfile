# Two stages: the first has the full SDK and compiles the app; the second has only
# the runtime and receives the compiled files. The final image is smaller and does
# not contain the source code or the compiler.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY global.json ./
COPY src/ src/

# Publishing the Api also builds the Blazor frontend, because the Api references it.
RUN dotnet publish src/Tickecheck.Api/Tickecheck.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the non-root user that the base image provides.
USER $APP_UID

ENTRYPOINT ["dotnet", "Tickecheck.Api.dll"]
