# Caso de Uso: Consultar perfiles estadísticos e historial

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-14 |
| **Nombre** | Consultar perfiles estadísticos e historial |
| **Actor Principal** | Capitán / Jugador / Espectador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuarios Públicos → conocer estadísticas, trayectoria y rendimiento de equipos y jugadores; Sistema → proveer visibilidad histórica del dominio |
| **Disparador (Trigger)** | El usuario hace clic sobre el nombre de un jugador o equipo en la tabla, plantilla o buscador |
| **Prioridad / Frecuencia** | Media; alta frecuencia |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Permite a cualquier usuario consultar la ficha pública de un jugador o equipo, acumulando su historial de rendimiento, tarjetas, goles/puntos acumulados y trayectoria en la plataforma.

### 2. PRECONDICIONES
- Existencia del jugador o del equipo en la base de datos centralizada.
- El sistema de consultas y agregación de métricas debe estar operativo.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/jugadores/{id}/estadisticas` o `GET /api/equipos/{id}/estadisticas`.
2. La **Capa de Presentación** (`JugadoresController.GetEstadisticas` / `EquiposController.GetEstadisticas`) recibe la solicitud y transfiere el ID del recurso consultado.
3. La **Capa de Negocio** (`JugadorService.GetEstadisticasJugadorAsync` / `EquipoService.GetEstadisticasEquipoAsync`) verifica la existencia del jugador o equipo en la Base de Datos (lanza `NotFoundException` 404 si no existe).
4. La **Capa de Persistencia** recupera los datos acumulados a lo largo de todas las competencias históricas y torneos actuales asociados a esa entidad.
5. El Sistema devuelve un código **200 OK** con las métricas consolidadas en `EstadisticasJugadorDTO` o `EstadisticasEquipoDTO` (goles, tarjetas, partidos, efectividad, torneos disputados).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Entidad Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del jugador o equipo no corresponde a ningún registro en la plataforma.
  2. La **Capa de Negocio** lanza `NotFoundException`.
  3. El Sistema devuelve un error **404 Not Found**. Fin del caso de uso.

### 5. SUB-VARIACIONES
- **Perfil de Jugador:** Muestra métricas individuales (Goles/Puntos convertidos, tarjetas amarillas/rojas, faltas, torneos disputados y equipo actual).
- **Perfil de Equipo:** Muestra métricas colectivas (Victorias, derrotas, efectividad, historial de torneos y lista de integrantes).

### 6. POSTCONDICIONES
- Despliegue de la trazabilidad histórica en formato solo lectura (CV Deportivo del ente consultado).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Retorno exitoso del compendio estadístico e historial. |
| `404` | Not Found | El jugador o el equipo consultado no existe. |

### Matriz de trazabilidad CU-14 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal (Jugador) | `200 OK` | `GetEstadisticasJugadorAsync_ReturnsConsolidatedData`<br>`GetEstadisticasJugadorAsync_WithHistoryAcrossMultipleTournaments_ReturnsAllTorneos` | `GetEstadisticas_Returns200OK` |
| Flujo principal (Equipo) | `200 OK` | `GetEstadisticasEquipoAsync_ReturnsConsolidatedData` | `GetEstadisticasEquipo_Returns200OK` |
| 3a. Jugador inexistente | `404 Not Found` | `GetEstadisticasJugadorAsync_WhenNotFound_ThrowsNotFoundException` | `GetEstadisticas_WhenEntityDoesNotExist_Returns404NotFound`<br>`CU14_GetEstadisticasJugador_Inexistente_Retorna404NotFound` |
| 3a. Equipo inexistente | `404 Not Found` | `GetEstadisticasEquipoAsync_WhenNotFound_ThrowsNotFoundException` | `GetEstadisticasEquipo_WhenDoesNotExist_Returns404NotFound`<br>`CU14_GetEstadisticasEquipo_Inexistente_Retorna404NotFound` |
