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

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor envía una petición al endpoint `PUT /api/partidos/{id}/resultado` con el JSON de los marcadores finales (Ej. `marcadorA` y `marcadorB`).
2. La **Capa de Presentación** valida que los marcadores sean valores numéricos no negativos y no falten datos.
3. La **Capa de Negocio** verifica el estado actual del partido y aplica la lógica de desempate en caso de corresponder.
4. La **Capa de Persistencia** actualiza el partido y cambia su estado a "Finalizado".
5. El Sistema desencadena internamente la actualización de las tablas de posiciones o los cruces de eliminatoria en la base de datos y la cartelera pública.
6. El Sistema devuelve un código **200 OK** confirmando que el marcador y el estado se han guardado exitosamente.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Marcadores inválidos (HTTP 400 Bad Request):**
  1. Si en el Paso 2 los marcadores provistos son negativos, nulos o con formato no numérico.
  2. La **Capa de Presentación** rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **3a. Partido no apto para recibir resultados (HTTP 409 Conflict):**
  1. Si en el Paso 3 el partido ya se encuentra en estado "Finalizado" o ha sido "Cancelado".
  2. La **Capa de Negocio** frena la operación.
  3. El Sistema devuelve un error **409 Conflict** indicando que el partido no admite carga de resultados en su estado actual. Fin del caso de uso.

* **3b. Empate no permitido en Eliminación Directa (HTTP 409 Conflict):**
  1. Si en el Paso 3 el partido pertenece a una llave de eliminación directa (brackets) y se envía un marcador igualado sin datos de desempate (penales/tiempo extra).
  2. La **Capa de Negocio** detecta la violación de modalidad.
  3. El Sistema devuelve un código **409 Conflict** indicando: "Esta modalidad no admite empates. Debe definir el ganador (Penales/Tiempo Extra)". Fin del caso de uso.

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
| Flujo principal | `200 OK` | `LoadResultadoAsync_UpdatesScoreAndStatus` | `UpdateResultado_Returns200OK` |
| 2a. Marcadores inválidos | `400 Bad Request` | — (validación DTO) | `UpdateResultado_WithNegativeScores_Returns400BadRequest` |
| 3a. Partido cerrado | `409 Conflict` | `LoadResultadoAsync_WhenPartidoAlreadyFinished_ThrowsConflict` | `UpdateResultado_WhenPartidoClosed_Returns409Conflict` |
| 3b. Empate en eliminatoria| `409 Conflict` | `LoadResultadoAsync_WhenDrawInEliminationPhase_ThrowsConflict` | `UpdateResultado_WhenDrawInElimination_Returns409Conflict` |
