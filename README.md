# Plataforma de Torneos Deportivos - API RESTful (.NET 10)

Sistema de gestión para Torneos Deportivos y E-Sports implementado con arquitectura en N-Capas (N-Tier) usando **.NET 10** y **Entity Framework Core (SQLite)**.

## Arquitectura (N-Tier)

```mermaid
graph TD
    API[API (Controllers)] --> BL[BusinessLogic (Services)]
    BL --> DA[DataAccess (Repositories + EF Core)]
    API --> SH[Shared (DTOs & Exceptions)]
    BL --> SH
    DA --> SH
```

## Ejecución del Proyecto

1. **Configurar la base de datos (Migraciones):**
   ```bash
   dotnet ef migrations add InitialCreate --project DataAccess --startup-project API
   dotnet ef database update --project DataAccess --startup-project API
   ```
2. **Ejecutar la API:**
   ```bash
   dotnet run --project API/API.csproj
   ```

*Nota: Al arrancar, un `DbInitializer` cargará datos semilla automáticamente si la base de datos está vacía.*

## Catálogo de Endpoints Principales

### Torneos
- `GET /api/torneos` - Lista todos los torneos
- `GET /api/torneos/{id}` - Obtiene un torneo
- `POST /api/torneos` - Crea un nuevo torneo
- `PUT /api/torneos/{id}` - Actualiza un torneo

**Ejemplo Request (POST /api/torneos):**
```json
{
  "nombre": "Copa Master",
  "disciplina": 1, 
  "modalidad": 1,
  "cupoMaximo": 8,
  "costoInscripcion": 5000.00
}
```

### Equipos
- `GET /api/torneos/{torneoId}/equipos` - Lista equipos de un torneo
- `POST /api/torneos/{torneoId}/equipos` - Inscribe un equipo
- `PUT /api/equipos/{id}/estado` - Cambia el estado de inscripción (Pendiente, Confirmado, Rechazado)

### Jugadores
- `GET /api/equipos/{equipoId}/jugadores` - Lista jugadores de un equipo
- `POST /api/equipos/{equipoId}/jugadores` - Añade jugador al equipo (Valida DNI único por torneo)
- `DELETE /api/jugadores/{id}` - Elimina un jugador
