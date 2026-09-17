# Caso de Uso: Consultar perfiles estadísticos e historial

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-14 |
| **Nombre** | Consultar perfiles estadísticos e historial |
| **Actor Principal** | Capitán / Jugador / Espectador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Espectador/Jugador → ver el rendimiento histórico y actual de un equipo/jugador |
| **Disparador (Trigger)** | El usuario hace clic sobre el nombre de un jugador o equipo en la tabla, plantilla o buscador. |
| **Prioridad / Frecuencia** | Media |
| **Reglas de negocio relacionadas** | Ninguna (consulta de datos) |

---

### 1. BREVE DESCRIPCIÓN
Permite a cualquier usuario consultar la ficha pública de un jugador o equipo, acumulando su historial de rendimiento, tarjetas, goles/puntos acumulados.

### 2. PRECONDICIONES
- Existencia del jugador/equipo en la base de datos centralizada.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El usuario selecciona el perfil del jugador/equipo.
2. El Sistema recupera los datos acumulados a lo largo de las competencias.
3. El Sistema muestra:
   - Perfil de Jugador: Goles/Puntos convertidos, tarjetas amarillas/rojas, faltas, torneos disputados y equipo actual.
   - Perfil de Equipo: Victorias, derrotas, efectividad, historial de torneos y lista de integrantes.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. Jugador o Equipo Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 1 el perfil consultado ha sido eliminado o no existe en la base de datos.
  2. El Sistema devuelve un error 404 y muestra un mensaje: "Perfil no encontrado". Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- La consulta puede originarse desde distintas pantallas: buscador general, ficha de un partido o tabla de goleadores.

### 6. POSTCONDICIONES
- Despliegue de trazabilidad histórica (CV Deportivo). No se modifican los datos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Perfil estadístico recuperado correctamente. |
| `404` | Not Found | Perfil no hallado. |

### Matriz de trazabilidad CU-14 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetProfileStatsAsync_ReturnsAccumulatedData` | `GetProfileStats_Returns200OK` |
| 1a. Perfil inexistente | `404 Not Found` | `GetProfileStatsAsync_WhenNotFound_ThrowsEntityNotFoundException` | `GetProfileStats_WhenNotFound_Returns404NotFound` |
