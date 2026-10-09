# Caso de Uso: Consultar las tablas de posiciones y llaves de competencia en tiempo real

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-13 |
| **Nombre** | Consultar las tablas de posiciones y llaves de competencia (Brackets) en tiempo real |
| **Actor Principal** | Capitán / Jugador / Espectador |
| **Alcance / Nivel** | Sistema; subfunción / meta de usuario |
| **Stakeholders e intereses** | Usuarios Públicos → conocer la posición actual de sus equipos y posibles cruces; Sistema → proveer datos estadísticos calculados dinámicamente |
| **Disparador (Trigger)** | El usuario selecciona la opción "Tabla de Posiciones" o "Llaves / Brackets" dentro de un torneo |
| **Prioridad / Frecuencia** | Alta; muy alta frecuencia |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Permite a los usuarios consultar las tablas (puntos, PG, PE, PP, GF, GC, Dif) o el diagrama de llaves (eliminatoria) actualizadas automáticamente tras cada resultado.

### 2. PRECONDICIONES
- El torneo consultado debe existir en la plataforma.
- El torneo debe encontrarse en estado "En Desarrollo" o "Finalizado".

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor envía una petición al endpoint `GET /api/torneos/{id}/posiciones` o `GET /api/torneos/{id}/llaves`.
2. La **Capa de Presentación** procesa el identificador del torneo.
3. La **Capa de Negocio** verifica la existencia del torneo y su modalidad (Liga o Eliminación Directa).
4. La **Capa de Negocio** recupera los partidos finalizados y calcula dinámicamente las estadísticas de los equipos (puntos, diferencia de goles) o la estructura del bracket.
5. El Sistema devuelve un código **200 OK** con la colección de posiciones debidamente ordenada por los criterios de desempate computados, o el diagrama de árbol (llaves).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Modalidad incorrecta consultada (HTTP 400 Bad Request):**
  1. Si en el Paso 3 se intenta consultar la "Tabla de Posiciones" de un torneo que es estrictamente de "Eliminación Directa" (o viceversa).
  2. La **Capa de Negocio** detecta que la consulta no aplica para la configuración del torneo.
  3. El Sistema devuelve un código **400 Bad Request** notificando que la vista solicitada no es compatible con el formato del torneo. Fin del caso de uso.

* **3b. Torneo Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del torneo enviado no existe.
  2. La **Capa de Negocio** no encuentra la entidad en la Base de Datos.
  3. El Sistema devuelve un error **404 Not Found**. Fin del caso de uso.

### 5. SUB-VARIACIONES
- Si el formato del torneo es **Liga**, se mostrará una grilla con los puntos de cada equipo.
- Si el formato es **Brackets (Eliminación Directa)**, se mostrará el diagrama dinámico con los equipos avanzando ronda tras ronda.

### 6. POSTCONDICIONES
- Muestra visual precisa e inmediata del estado de la competencia (solo lectura).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Retorno exitoso de la tabla de posiciones o llaves de eliminación. |
| `400` | Bad Request | Consulta de un formato que no coincide con la modalidad del torneo. |
| `404` | Not Found | Inexistencia del torneo consultado. |

### Matriz de trazabilidad CU-13 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetPosicionesAsync_CalculatesAndReturnsStandings` | `GetPosiciones_Returns200OK` |
| 3a. Modalidad incompatible | `400 Bad Request` | `GetPosicionesAsync_WhenTorneoIsBrackets_ThrowsValidationException` | `GetPosiciones_WhenTorneoIsElimination_Returns400BadRequest` |
| 3b. Torneo inexistente | `404 Not Found` | `GetPosicionesAsync_WhenTorneoNotFound_ThrowsNotFoundException` | `GetPosiciones_WhenTorneoDoesNotExist_Returns404NotFound` |
