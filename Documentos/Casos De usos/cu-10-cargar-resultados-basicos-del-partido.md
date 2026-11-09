# Caso de Uso: Cargar resultados básicos del partido

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-10 |
| **Nombre** | Cargar resultados básicos del partido (Goles o puntos finales) |
| **Actor Principal** | Veedor / Árbitro / Organizador |
| **Alcance / Nivel** | Sistema; subfunción / meta de usuario |
| **Stakeholders e intereses** | Veedor/Organizador → finalizar formalmente el partido y asentar el marcador; Sistema de Brackets/Posiciones → actualizar estadisticas tras conocerse un resultado |
| **Disparador (Trigger)** | El usuario selecciona la opción "Cargar Resultado Final" en un partido del cronograma |
| **Prioridad / Frecuencia** | Alta; alta frecuencia (se repite por cada partido) |
| **Reglas de negocio relacionadas** | Ninguna (salvo desempate implícito en eliminatorias) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Veedor o Administrador cargar únicamente el marcador final de un partido de forma rápida para actualizar tablas o llaves.

### 2. PRECONDICIONES
- El partido debe estar configurado en el sistema y encontrarse en estado "Pendiente" o "En Juego".
- El usuario debe contar con los permisos correspondientes para reportar resultados.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PUT /api/partidos/{id}/resultado` con el JSON de `CargarResultadoDTO` (`MarcadorLocal`, `MarcadorVisitante`, y opcionalmente `PenalesLocal`, `PenalesVisitante`).
2. La **Capa de Presentación** (`PartidosController.UpdateResultado`) valida que los marcadores sean valores numéricos enteros no negativos comprendidos entre 0 y 999 (data annotations `[Range(0, 999)]`).
3. La **Capa de Negocio** (`PartidoService.LoadResultadoAsync`):
   - Verifica la existencia del partido en la base de datos (lanza `NotFoundException` 404 si no existe).
   - Verifica que el partido se encuentre en estado editable (`Pendiente` o `EnProgreso`).
   - Si el torneo es de modalidad "Eliminación Directa" y los marcadores están empatados, valida que se hayan remitido los resultados de penales/desempate y que definan un ganador unívoco (no empate en penales).
   - Asigna los marcadores definitivos y cambia el estado del partido a `Finalizado`.
4. La **Capa de Persistencia** actualiza el registro en la tabla `Partidos`.
5. **Avance de Llaves y Coronación (Eliminación Directa):**
   - Si el torneo es de `Modalidad.EliminacionDirecta`:
     - Al completarse los partidos del cruce en la ronda actual (ej. ambas semifinales), el Sistema crea y programa automáticamente el partido de la siguiente ronda (ej. la Final) emparejando a los ganadores en estado `Pendiente`.
     - Si el partido finalizado corresponde a la última ronda (la Final), el Sistema proclama al ganador como campeón (`torneo.CampeonId`), transiciona el torneo a `EstadoTorneo.Finalizado` y actualiza la entidad `Torneo`.
6. El Sistema devuelve un código **200 OK** con el payload `PartidoResultadoResponseDTO` confirmando el marcador, estado y ganador.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Marcadores inválidos o fuera de rango (HTTP 400 Bad Request):**
  1. Si en el Paso 2 los marcadores provistos son negativos, mayores a 999, o los penales poseen formato inválido o empate.
  2. La **Capa de Presentación** o la **Capa de Negocio** rechaza la petición lanzando `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el formato `{ "error": "mensaje descriptivo" }`. Fin del caso de uso.

* **3a. Partido Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del partido no existe en la base de datos.
  2. La **Capa de Negocio** lanza `NotFoundException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. Partido no apto para recibir resultados (HTTP 409 Conflict):**
  1. Si en el Paso 3 el partido ya se encuentra en estado `Finalizado` o ha sido `Cancelado`.
  2. La **Capa de Negocio** frena la operación y lanza `BusinessRuleConflictException`.
  3. El Sistema devuelve un error **409 Conflict** indicando que el partido no admite carga de resultados en su estado actual. Fin del caso de uso.

* **3c. Empate no permitido en Eliminación Directa (HTTP 409 Conflict):**
  1. Si en el Paso 3 el partido pertenece a una llave de eliminación directa y el marcador final resulta empatado sin haberse definido un ganador mediante penales/desempate.
  2. La **Capa de Negocio** detecta la violación y lanza `BusinessRuleConflictException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: `"Esta modalidad no admite empates. Debe definir el ganador (Penales/Tiempo Extra)"`. Fin del caso de uso.

### 5. SUB-VARIACIONES
- Si el partido requiere desempate, el payload enviado puede incluir opcionalmente `penalesEquipoA` y `penalesEquipoB` que serán procesados en el mismo endpoint.

### 6. POSTCONDICIONES
- El marcador del partido queda registrado y su estado cambia a "Finalizado".
- Se recalculan y actualizan automáticamente las entidades conectadas (Tablas de Posiciones, Brackets o Llaves siguientes).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Registro exitoso del resultado y actualización de estado. |
| `400` | Bad Request | Formato numérico incorrecto o marcadores faltantes/negativos. |
| `404` | Not Found | El ID del partido no existe en la base de datos. |
| `409` | Conflict | El partido ya estaba finalizado, cancelado, o un empate fue enviado en fase eliminatoria sin desempate. |

### Matriz de trazabilidad CU-10 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `LoadResultadoAsync_UpdatesScoreAndStatus`<br>`LoadResultadoAsync_WhenDrawInEliminationPhaseWithValidPenales_UpdatesScoreAndStatus` | `UpdateResultado_Returns200OK` |
| Avance de llaves (Ronda previa -> Final) | `200 OK` | `LoadResultadoAsync_EliminacionDirecta_WhenBothSemifinalsFinished_CreatesFinalMatch`<br>`LoadResultadoAsync_EliminacionDirecta_WhenOnlyOneMatchOfPairFinished_DoesNotCreateNextRound` | `Test1_Torneo4Equipos_AvanceDeSemifinalAFinal_CreaPartidoFinalAutomaticamente`<br>`Test3_Torneo8Equipos_ProgresionCompleta_Cuartos_Semifinal_Final_HastaFinalizado` |
| Partido Final y coronación | `200 OK` | `LoadResultadoAsync_EliminacionDirecta_WhenFinalMatchFinished_DeclaresChampionAndFinalizesTorneo` | `Test2_Torneo4Equipos_PartidoFinalDeclaraCampeonYFinalizaTorneo` |
| 2a. Marcadores inválidos | `400 Bad Request` | `LoadResultadoAsync_WhenNegativeScore_ThrowsValidationException` | `UpdateResultado_WithNegativeScores_Returns400BadRequest`<br>`CU10_CargarResultado_MarcadorNegativo_Retorna400BadRequest`<br>`Vector3_AtaquesNumericosEnCostosMarcadoresYMinutos_Retorna400BadRequest` |
| 3a. Partido Inexistente | `404 Not Found` | `LoadResultadoAsync_WhenPartidoNotFound_ThrowsNotFound` | `CU10_CargarResultado_PartidoInexistente_Retorna404NotFound` |
| 3b. Partido cerrado | `409 Conflict` | `LoadResultadoAsync_WhenPartidoAlreadyFinished_ThrowsConflict` | `UpdateResultado_WhenPartidoClosed_Returns409Conflict` |
| 3c. Empate en eliminatoria | `409 Conflict` | `LoadResultadoAsync_WhenDrawInEliminationPhase_ThrowsConflict` | `UpdateResultado_WhenDrawInElimination_Returns409Conflict` |
