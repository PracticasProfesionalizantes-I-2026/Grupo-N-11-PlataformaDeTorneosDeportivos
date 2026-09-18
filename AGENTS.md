# Contexto Operativo - Plataforma de Torneos Deportivos

Este proyecto es una API RESTful desarrollada en .NET 10 siguiendo una arquitectura estricta de N-Capas (N-Tier).

## Propósito
El sistema gestiona Torneos, Equipos y Jugadores, aplicando reglas de negocio complejas como control de cupos de torneos y restricciones de duplicidad de jugadores (mediante DNI) en equipos del mismo torneo.

## Comandos CLI (Agent Toolkit)
- **Build**: `dotnet build`
- **Tests**: `dotnet test`
- **Run**: `dotnet run --project API/API.csproj`
- **Migrations**: `dotnet ef migrations add <Name> --project DataAccess --startup-project API`

## Convenciones de Arquitectura
1. **API**: Contiene los Controllers. Depende de `BusinessLogic` y `Shared`. Maneja excepciones globalmente mediante `ExceptionHandlingMiddleware`.
2. **BusinessLogic**: Contiene las interfaces e implementaciones de los servicios. Contiene **toda la lógica de negocio y validación**. No hace consultas directas a EF, usa `DataAccess` (Repositories). Mapea manualmente las entidades a DTOs.
3. **DataAccess**: DbContext (`AppDbContext` SQLite), entidades (`Torneo`, `Equipo`, `Jugador`) y Repositorios (`IGenericRepository` y derivados). Las relaciones configuradas con `DeleteBehavior.Restrict`.
4. **Shared**: Enumeraciones, DTOs y Excepciones tipadas (`NotFoundException`, `ValidationException`, `BusinessRuleConflictException`).

## Reglas de Negocio Implementadas
- Torneos de Eliminación Directa tienen `CupoMaximo >= 4`.
- Costo de inscripción siempre `> 0`.
- Torneos tienen cupo limitado. Un equipo no puede pasar a `InscritoYConfirmado` si el torneo se llenó.
- Un equipo requiere al menos un jugador para ser confirmado.
- Un jugador no puede estar en dos equipos distintos dentro de un mismo torneo de manera simultánea.
