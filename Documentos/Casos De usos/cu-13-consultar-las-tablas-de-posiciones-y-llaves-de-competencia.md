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

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `GET /api/torneos/{id}/posiciones` (para torneos de Liga) o `GET /api/torneos/{id}/llaves` (para torneos de Eliminación Directa).
2. La **Capa de Presentación** (`TorneosController.GetPosiciones` / `TorneosController.GetLlaves`) procesa el identificador del torneo.
3. La **Capa de Negocio** (`TorneoService.GetPosicionesAsync` / `TorneoService.GetLlavesAsync`):
   - Verifica la existencia del torneo (lanza `NotFoundException` 404 si no existe).
   - Verifica la compatibilidad de modalidad (Liga para posiciones, Eliminación Directa para llaves; si no coincide, lanza `ValidationException` 400).
4. La **Capa de Negocio** recupera los datos optimizados:
   - Para Liga: Consulta la tabla persistida y precalculada `TablaPosicion` (mediante `ITablaPosicionRepository` y `AsNoTracking()`), ordenada por `Posicion`. En caso de que no existan registros previos o en entornos de prueba, calcula dinámicamente las posiciones aplicando criterios de desempate (Puntos descendente, Diferencia de Gol descendente, Goles a Favor descendente y Nombre alfabético) y persiste el resultado en la entidad `TablaPosicion` para optimizar futuras lecturas. Al registrarse resultados en `PartidoService`, las posiciones se recalculan y actualizan automáticamente.
   - Para Eliminación Directa: Estructura del bracket por rondas (Octavos, Cuartos, Semifinal, Final) con ganadores avanzando.
5. El Sistema devuelve un código **200 OK** con la colección de `PosicionTablaDTO` o el árbol de `LlaveTorneoDTO`.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Modalidad incompatible consultada (HTTP 400 Bad Request):**
  1. Si en el Paso 3 se solicita la "Tabla de Posiciones" de un torneo de Eliminación Directa, o las "Llaves" de un torneo de Liga.
  2. La **Capa de Negocio** detecta que la consulta no aplica para la modalidad del torneo y lanza `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: `"El torneo no es de modalidad Liga. Para eliminación directa consulte /llaves"` o viceversa. Fin del caso de uso.

* **3b. Torneo Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del torneo no existe en la base de datos.
  2. La **Capa de Negocio** no encuentra la entidad y lanza `NotFoundException`.
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
| Flujo principal (Posiciones Liga) | `200 OK` | `GetPosicionesAsync_CalculatesAndReturnsStandings`<br>`GetPosicionesAsync_WithTieBreakers_SortsByDiferenciaGolesThenGolesAFavorThenNombre`<br>`GetPosicionesAsync_WhenPrecalculatedStandingsExist_ReturnsDirectlyFromRepository`<br>`GetPosicionesAsync_WhenStandingsNotPrecalculated_ComputesAndPersists` | `GetPosiciones_Returns200OK`<br>`GetPosiciones_WhenMatchesFinished_ReturnsOptimizedStandingsAndPersistsTablaPosiciones` |
| Flujo principal (Llaves Brackets) | `200 OK` | `GetLlavesAsync_CalculatesAndReturnsBrackets` | `GetLlaves_Returns200OK` |
| 3a. Posiciones en Eliminación Directa | `400 Bad Request` | `GetPosicionesAsync_WhenTorneoIsBrackets_ThrowsValidationException` | `GetPosiciones_WhenTorneoIsElimination_Returns400BadRequest`<br>`CU13_GetPosiciones_TorneoEliminacionDirecta_Retorna400BadRequest` |
| 3a. Llaves en Liga | `400 Bad Request` | `GetLlavesAsync_WhenTorneoIsLiga_ThrowsValidationException` | `GetLlaves_WhenTorneoIsLiga_Returns400BadRequest` |
| 3b. Torneo inexistente | `404 Not Found` | `GetPosicionesAsync_WhenTorneoNotFound_ThrowsNotFoundException`<br>`GetLlavesAsync_WhenTorneoNotFound_ThrowsNotFoundException` | `GetPosiciones_WhenTorneoDoesNotExist_Returns404NotFound`<br>`GetLlaves_WhenTorneoDoesNotExist_Returns404NotFound`<br>`CU13_GetPosiciones_TorneoInexistente_Retorna404NotFound` |
