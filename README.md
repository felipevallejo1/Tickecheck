# Tickecheck

Sistema de tickets (helpdesk) multi-tenant para varias empresas, hecho en .NET 9 con Clean
Architecture. Proyecto de portfolio en construcción: este README se completa en la última fase.

## Estado

Fase 1 de 8: esqueleto de la solución y capa de dominio.

## Requisitos

- .NET SDK 9
- Docker Desktop

## Cómo correrlo

```bash
# Tests
dotnet test

# SQL Server local (todavía no lo usa ningún proyecto)
cp .env.example .env
docker compose up -d
```

## Documentación

- [Guía de estudio](docs/STUDY.md)
- [Decisiones de arquitectura](docs/decisions/)
