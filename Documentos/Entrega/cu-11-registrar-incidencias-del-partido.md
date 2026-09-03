# Caso de Uso: Registrar Incidencias del Partido

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Regla de negocio RN-07 (expulsión automática por segunda tarjeta amarilla o tarjeta roja directa y bloqueo de futuras incidencias para el jugador) **implementada** en el código; cada caso borde cuenta con su test unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-11 |
| **Nombre** | Registrar incidencias del partido |
| **Actor Principal** | Veedor / Árbitro |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | **Veedor / Árbitro:** asentar en vivo o al cierre del cotejo los eventos relevantes (goles, faltas, tarjetas, cambios) con rapidez y precisión.<br>**Organización del Torneo:** contar con actas oficiales automatizadas para alimentar la tabla de posiciones, goleadores y sanciones disciplinarias.<br>**Equipos y Jugadores:** disponer de un registro transparente, cronológico y fehaciente de las acciones del partido. |
| **Disparador (Trigger)** | El Veedor selecciona la opción "Registrar Incidencia" desde la planilla digital del encuentro en su panel móvil. |
| **Prioridad / Frecuencia** | Alta; muy alta frecuencia durante la disputa de las fechas competitivas. |
| **Reglas de negocio relacionadas** | RN-07 (expulsión por doble amarilla o roja directa y bloqueo de nuevas incidencias para el jugador) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Veedor o Árbitro registrar en tiempo real o diferido los eventos críticos del encuentro deportivo (goles/puntos, tarjetas amarillas y rojas, faltas o sustituciones) asociados a los deportistas habilitados en las listas oficiales de ambos planteles.

### 2. PRECONDICIONES
- El Veedor debe estar autenticado en la plataforma con Token JWT válido y poseer permisos de gestión asignados sobre el partido en disputa.
- El partido debe encontrarse en estado "En Progreso" o "Pendiente de Carga".
- Las listas de buena fe de ambos equipos participantes deben encontrarse previamente aprobadas y cerradas en el sistema.
- La Capa de Persistencia debe encontrarse accesible para almacenar los eventos y reflejar las variaciones de marcador.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/partidos/{partidoId}/incidencias` con un JSON que contiene los datos del evento (`tipoIncidencia`, `minuto`, `jugadorId`, `equipoId`, `observaciones`).
2. La **Capa de Presentación** (`IncidenciasController.RegistrarIncidencia`) valida que el JSON sea estructuralmente correcto, que los campos requeridos no sean nulos, que el minuto sea válido (`[Range(0, 150)]`) y que el tipo de incidencia corresponda a los valores permitidos (data annotations sobre `IncidenciaCreateDTO`).
3. La **Capa de Negocio** (`IncidenciaService.RegistrarIncidenciaAsync`):
   - Verifica la existencia del partido y que su estado actual admita la carga de eventos ("En Progreso" o "Pendiente de Carga").
   - Verifica que el jugador pertenezca a la nómina oficial de alguno de los dos equipos intervinientes en el encuentro.
   - Comprueba que el jugador no registre sanciones disciplinarias previas inhabilitantes.
   - Verifica que el jugador no se encuentre en estado "Expulsado" en dicho encuentro.
   - Si la incidencia es una "Tarjeta Roja Directa", o si es una "Tarjeta Amarilla" y el jugador ya registraba una amonestación previa en el mismo partido: aplica la regla de negocio **RN-07**, cambia el estado del jugador a "Expulsado" e impide la carga de subsiguientes incidencias de juego para él.
   - Si la incidencia corresponde a "Gol" o "Punto", actualiza el marcador parcial acumulado para el equipo correspondiente.
4. La **Capa de Persistencia** genera el ID único de la incidencia y guarda el registro en la tabla `Incidencias`, actualizando de forma atómica el marcador y el estado de los jugadores en las tablas `Partidos` y `PlanillaPartidos`.
5. El Sistema devuelve un código **201 Created** con la cabecera `Location` y el payload resultante (ID de la incidencia, minuto, jugador, estado disciplinario actualizado y marcador parcial).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. JSON inválido o con formato erróneo (HTTP 400 Bad Request):**
  1. Si en el Paso 1 el payload no es un JSON válido o posee tipos de datos incompatibles.
  2. El Sistema (Capa de Presentación / model binding) rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Datos requeridos faltantes o minuto fuera de rango (HTTP 400 Bad Request):**
  1. Si en el Paso 2 falta el `tipoIncidencia`, `jugadorId`, o si el `minuto` es negativo o excede el límite estipulado.
  2. El Sistema (Capa de Presentación) rechaza la petición por validación de modelo (`ModelState.IsValid == false`).
  3. El Sistema devuelve un código **400 Bad Request** detallando el error de validación. Fin del caso de uso.

* **3a. Partido inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el `{partidoId}` no existe en la base de datos.
  2. El Sistema (Capa de Negocio) lanza `PartidoNotFoundException`.
  3. El Sistema devuelve un código **404 Not Found** con el mensaje: `"El partido especificado no existe"`. Fin del caso de uso.

* **3b. Partido no disponible para carga de incidencias (HTTP 409 Conflict):**
  1. Si en el Paso 3 el partido se encuentra en estado "Finalizado", "Cancelado" o aún "Pendiente de Inicio".
  2. El Sistema (Capa de Negocio) lanza `PartidoEstadoInvalidoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"El partido no se encuentra habilitado para registrar incidencias en su estado actual"`. Fin del caso de uso.

* **3c. Jugador no pertenece a los equipos participantes (HTTP 422 Unprocessable Entity):**
  1. Si en el Paso 3 el `jugadorId` no pertenece a la lista de buena fe de ninguno de los planteles en disputa.
  2. El Sistema (Capa de Negocio) lanza `JugadorNoAsignadoAPartidoException`.
  3. El Sistema devuelve un código **422 Unprocessable Entity** con el mensaje: `"El jugador no pertenece a los planteles de este partido"`. Fin del caso de uso.

* **3d. Jugador con inhabilitación disciplinaria previa (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que el jugador seleccionado arrastra una suspensión activa de fechas anteriores dictada por el tribunal de disciplina.
  2. El Sistema (Capa de Negocio) frena el registro y lanza `JugadorInhabilitadoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"El jugador seleccionado se encuentra inhabilitado para este partido por sanción previa"`. Fin del caso de uso.

* **3e. Intento de registro sobre jugador expulsado (HTTP 409 Conflict):**
  1. Si en el Paso 3 se intenta asociar una acción de juego a un deportista que ya fue expulsado durante el mismo cotejo, violando la regla **RN-07**.
  2. El Sistema (Capa de Negocio) bloquea la acción y lanza `JugadorExpulsadoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"No es posible registrar incidencias para un jugador expulsado (RN-07)"`. Fin del caso de uso.

* **3f. Pérdida transitoria de conectividad / Modo Offline (Sincronización diferida):**
  1. Si el dispositivo del Veedor pierde cobertura de red en el predio deportivo durante la carga.
  2. La aplicación cliente almacena localmente el paquete de la incidencia en memoria protegida (IndexedDB/PWA).
  3. Al reestablecerse la señal, el cliente envía las incidencias encoladas al endpoint de sincronización (`POST /api/partidos/{partidoId}/incidencias/sincronizar`).
  4. El Sistema valida y persiste los eventos respetando la marca temporal original. Fin del caso de uso.

* **4a. Falla de persistencia en la base de datos (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 ocurre un error no controlado al persistir la incidencia o actualizar el marcador.
  2. El Sistema revierte la transacción y genera el registro en el log de errores.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El Veedor puede registrar los eventos minuto a minuto desde su dispositivo móvil durante el transcurso del encuentro o cargar la planilla completa de incidencias al finalizar el cotejo.
2. La segunda tarjeta amarilla puede registrarse indicando explícitamente "Segunda Amarilla" o ser inferida de forma automática por el sistema al seleccionar "Tarjeta Amarilla" sobre un jugador previamente apercibido (**RN-07**).

### 6. POSTCONDICIONES
- La incidencia se almacena con un identificador único en la tabla `Incidencias` vinculada al partido, al equipo y al deportista.
- El marcador general del partido y la cronología pública de eventos se actualizan de forma instantánea.
- Si la incidencia implicó expulsión (**RN-07**), el deportista queda inhabilitado para el resto del partido y se genera el registro preventivo para el tribunal de disciplina.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Registro exitoso de la nueva incidencia y actualización del tanteador del partido. |
| `400` | Bad Request | Formato JSON defectuoso, datos obligatorios ausentes o minuto fuera de rango permitido. |
| `404` | Not Found | Inexistencia del partido referenciado (`partidoId`) en la Capa de Persistencia. |
| `409` | Conflict | Partido en estado no apto para carga, jugador con sanción disciplinaria previa o jugador ya expulsado (RN-07). |
| `422` | Unprocessable Entity | El jugador seleccionado no pertenece a la lista de buena fe de los equipos del cotejo. |
| `500` | Internal Server Error | Falla no controlada en la persistencia transaccional del evento y del tanteador. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** sintaxis JSON, verificación de rangos (`[Range(0, 150)]` para el minuto), obligatoriedad de campos y consistencia de tipos enumerados (`[EnumDataType]`) en `IncidenciaCreateDTO` a través de `ModelState.IsValid` en `IncidenciasController`.
- **Verificación (Negocio, → 404/409/422):** existencia y vigencia de estado del partido (`PartidoNotFoundException` → 404; `PartidoEstadoInvalidoException` → 409); pertenencia del jugador a los planteles en juego (`JugadorNoAsignadoAPartidoException` → 422); control de inhabilitaciones históricas (`JugadorInhabilitadoException` → 409); y ejecución de la regla **RN-07** para control de expulsiones inmediatas y bloqueo de eventos posteriores (`JugadorExpulsadoException` → 409).

### Matriz de trazabilidad CU-11 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `RegistrarIncidenciaAsync_WithValidData_ReturnsCreatedIncidenciaAndUpdatesScore` | `RegistrarIncidencia_WithValidData_Returns201Created` |
| 1a. JSON inválido | `400 Bad Request` | — (model binding de ASP.NET Core) | `RegistrarIncidencia_WithMalformedJson_Returns400BadRequest` |
| 2a. Minuto fuera de rango | `400 Bad Request` | — (detectado por `[Range]` en Presentación) | `RegistrarIncidencia_WithNegativeMinute_Returns400BadRequest` |
| 3a. Partido inexistente | `404 Not Found` | `RegistrarIncidenciaAsync_WhenPartidoDoesNotExist_ThrowsPartidoNotFoundException` | `RegistrarIncidencia_WhenPartidoNotFound_Returns404NotFound` |
| 3b. Partido no habilitado | `409 Conflict` | `RegistrarIncidenciaAsync_WhenPartidoNotEditable_ThrowsPartidoEstadoInvalidoException` | `RegistrarIncidencia_WhenPartidoFinished_Returns409Conflict` |
| 3c. Jugador no pertenece | `422 Unprocessable Entity` | `RegistrarIncidenciaAsync_WhenPlayerNotInTeams_ThrowsJugadorNoAsignadoException` | `RegistrarIncidencia_WhenPlayerNotInTeams_Returns422UnprocessableEntity` |
| 3d. Jugador inhabilitado | `409 Conflict` | `RegistrarIncidenciaAsync_WhenPlayerSuspended_ThrowsJugadorInhabilitadoException` | `RegistrarIncidencia_WhenPlayerSuspended_Returns409Conflict` |
| 3e. Expulsión / 2da amarilla (RN-07) | `201 Created` / `409 Conflict` | `RegistrarIncidenciaAsync_WhenSecondYellow_SetsPlayerExpulsado`<br>`RegistrarIncidenciaAsync_WhenPlayerAlreadyExpulsado_ThrowsJugadorExpulsadoException` | `RegistrarIncidencia_WhenSecondYellow_SetsExpulsado`<br>`RegistrarIncidencia_WhenPlayerAlreadyExpulsado_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso cuenta con al menos un test unitario o de integración HTTP. La suite de pruebas completa se ejecuta mediante `dotnet test`.
