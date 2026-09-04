# Caso de Uso: Validar y Aprobar/Rechazar la Inscripción y Documentación de los Equipos

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-06 (condiciones obligatorias para confirmación: pago validado y cupo disponible) y RN-03 (desbloqueo de la lista de buena fe ante rechazo/observación) **implementadas** en el código; cada caso borde cuenta con su test unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-06 |
| **Nombre** | Validar y aprobar/rechazar la inscripción y documentación de los equipos |
| **Actor Principal** | Organizador / Administrador del Sistema |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | **Organizador / Administrador:** certificar la aptitud de los equipos y la autenticidad de pagos/documentación antes de habilitar su participación.<br>**Capitán de Equipo:** recibir la ratificación oficial de su cupo en el torneo o la causal exacta de observación para subsanarla.<br>**Equipos Competidores:** asegurar la paridad y el fair play compitiendo con rivales debidamente auditados. |
| **Disparador (Trigger)** | El Organizador accede al listado de solicitudes de inscripción en estado "Pendiente de Validación" dentro de su panel de control. |
| **Prioridad / Frecuencia** | Alta; frecuencia regular y continua durante la ventana de inscripciones previa al sorteo de fixture. |
| **Reglas de negocio relacionadas** | RN-06 (condiciones de aprobación: pago acreditado y cupo disponible); RN-03 (liberación de edición de lista ante observación) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Organizador auditar las solicitudes de inscripción de los equipos, analizando la documentación de los deportistas cargada en la lista de buena fe y los comprobantes de pago asociados, para otorgar el alta definitiva con reserva de plaza o rechazar la postulación con observaciones fundadas.

### 2. PRECONDICIONES
- El usuario debe estar autenticado en la plataforma con un Token JWT válido con permisos de Organizador o Administrador.
- El torneo al cual pertenece la postulación debe encontrarse en estado "Abierto para Inscripción".
- El equipo a auditar debe encontrarse registrado en la Capa de Persistencia con estado "Pendiente de Validación".
- La Capa de Persistencia debe encontrarse operativa para aplicar las actualizaciones de estado.

### 3. FLUJO PRINCIPAL (Camino Feliz - Aprobación - HTTP 200 OK)
1. El Actor envía una petición al endpoint `PATCH /api/torneos/{torneoId}/equipos/{equipoId}/aprobar` con el payload de confirmación.
2. La **Capa de Presentación** (`InscripcionesController.AprobarInscripcion`) valida la estructura de la solicitud y comprueba la validez sintáctica de los identificadores de ruta.
3. La **Capa de Negocio** (`InscripcionService.AprobarInscripcionAsync`):
   - Verifica la existencia en base de datos del torneo y del equipo referenciado.
   - Comprueba que el equipo se encuentre efectivamente en estado "Pendiente de Validación".
   - Verifica que el torneo no haya alcanzado su cupo máximo de equipos confirmados, aplicando la regla de negocio **RN-06**.
   - En torneos configurados como "Pago", corrobora que conste el registro de validación del comprobante de pago, en cumplimiento estricto de la regla de negocio **RN-06**.
   - Asigna al equipo el estado definitivo "Inscrito y Confirmado".
   - Actualiza el contador del torneo, reduciendo en uno la cantidad de plazas disponibles.
4. La **Capa de Persistencia** actualiza transaccionalmente los registros en las tablas `Equipos` y `Torneos`.
5. La **Capa de Negocio** despacha una notificación al Capitán informando la ratificación formal de su cupo en el certamen.
6. El Sistema devuelve un código **200 OK** con los datos actualizados del equipo (ID, estado "Inscrito y Confirmado") y la disponibilidad remanente de cupos del torneo.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. Identificadores inválidos o cuerpo malformado (HTTP 400 Bad Request):**
  1. Si en el Paso 1 los identificadores (`torneoId` o `equipoId`) no poseen un formato válido (ej. GUID o entero mal estructurado).
  2. El Sistema (Capa de Presentación) rechaza la solicitud por error de formato de parámetros.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Torneo o equipo inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el torneo o el equipo no existen en la base de datos.
  2. El Sistema (Capa de Negocio) lanza `RecursoNotFoundException`.
  3. El Sistema devuelve un código **404 Not Found** detallando la entidad no encontrada. Fin del caso de uso.

* **3a. Estado del equipo no apto para evaluación (HTTP 409 Conflict):**
  1. Si en el Paso 3 el equipo no se encuentra en estado "Pendiente de Validación" (ej. ya se encontraba confirmado o fue dado de baja previamente).
  2. El Sistema (Capa de Negocio) frena la operación y lanza `EstadoEquipoInvalidoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"Solo pueden dictaminarse equipos en estado Pendiente de Validación"`. Fin del caso de uso.

* **3b. Cupo máximo alcanzado en el torneo (HTTP 409 Conflict):**
  1. Si en el Paso 3 el torneo ya alcanzó la cantidad máxima permitida de equipos confirmados, violando la regla **RN-06**.
  2. El Sistema (Capa de Negocio) frena la aprobación y lanza `TorneoCupoLlenoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"No se puede confirmar la inscripción: el cupo del torneo se encuentra completo (RN-06)"`. Fin del caso de uso.

* **3c. Pago no validado en torneo arancelado (HTTP 422 Unprocessable Entity):**
  1. Si en el Paso 3 el torneo es de pago pero el equipo no cuenta con un pago acreditado y validado, violando la regla **RN-06**.
  2. El Sistema (Capa de Negocio) lanza `PagoRequeridoException`.
  3. El Sistema devuelve un código **422 Unprocessable Entity** con el mensaje: `"No es posible confirmar la inscripción sin acreditar el pago correspondiente (RN-06)"`. Fin del caso de uso.

* **3d. Rechazo de la solicitud con observaciones (Camino Alternativo - HTTP 200 OK):**
  1. Si el Organizador opta por observar o rechazar la solicitud, envía una petición a `PATCH /api/torneos/{torneoId}/equipos/{equipoId}/rechazar` incluyendo obligatoriamente el atributo `motivoRechazo`.
  2. Si el `motivoRechazo` viene nulo, vacío o supera el límite de caracteres, la Capa de Presentación rechaza con código **400 Bad Request**.
  3. La Capa de Negocio cambia el estado del equipo a "Rechazado / Observado", mantiene intacto el cupo del torneo y libera la edición de la lista de buena fe para el Capitán, en cumplimiento de la regla **RN-03**.
  4. El Sistema despacha una notificación al Capitán detallando los motivos de la observación.
  5. El Sistema devuelve un código **200 OK** confirmando el estado "Rechazado / Observado" y habilitando la subsanación. Fin del caso de uso.

* **4a. Falla no controlada en la Capa de Persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 ocurre un error durante el guardado de la transacción en la base de datos.
  2. El Sistema revierte la operación y registra la incidencia técnica en el log de auditoría.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El Organizador puede dictaminar una postulación desde el panel web de auditoría individual de equipos o mediante la vista de grilla rápida con visualizador integrado de comprobantes de pago e imágenes de DNI.
2. La notificación de aprobación o rechazo puede remitirse de forma combinada por correo electrónico y mediante notificaciones push en la plataforma móvil.

### 6. POSTCONDICIONES
- Si la solicitud es aprobada: el equipo adopta el estado "Inscrito y Confirmado", queda definitivamente incorporado a la nómina de habilitados para el fixture y se descuenta una plaza del cupo disponible del torneo.
- Si la solicitud es rechazada: el cupo disponible del torneo no se altera, el equipo queda en estado "Rechazado / Observado" y se le restituyen los permisos de edición al Capitán sobre la lista de buena fe (**RN-03**) para rectificar la información objetada.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Confirmación exitosa del cambio de estado del equipo (sea Aprobación o Rechazo/Observación con motivo). |
| `400` | Bad Request | Parámetros de ruta inválidos, cuerpo de petición malformado o ausencia del motivo obligatorio en el rechazo. |
| `404` | Not Found | Inexistencia del torneo (`torneoId`) o del equipo (`equipoId`) en la Capa de Persistencia. |
| `409` | Conflict | Equipo en estado inválido para evaluar, o torneo con cupo máximo completo (RN-06). |
| `422` | Unprocessable Entity | Intento de aprobar un equipo sin comprobante de pago validado en un torneo arancelado (RN-06). |
| `500` | Internal Server Error | Falla técnica no controlada durante la actualización transaccional en base de datos. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** formato correcto de identificadores numéricos o GUIDs y obligatoriedad del texto `motivoRechazo` (no nulo, no vacío) en el endpoint de rechazo mediante Data Annotations y `ModelState.IsValid` en `InscripcionesController`.
- **Verificación (Negocio, → 404/409/422):** existencia de entidades (`RecursoNotFoundException` → 404); verificación del ciclo de vida del equipo (`EstadoEquipoInvalidoException` → 409); validación de la disponibilidad de vacantes (**RN-06** vía `TorneoCupoLlenoException` → 409); y control de acreditación de pago (**RN-06** vía `PagoRequeridoException` → 422). Al rechazar, el service aplica la regla **RN-03** desbloqueando la lista para el Capitán.

### Matriz de trazabilidad CU-06 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal (Aprobación) | `200 OK` | `AprobarInscripcionAsync_WithValidData_ReturnsAprobadoDTOAndDecrementsCupo` | `AprobarInscripcion_WithValidTeam_Returns200Ok` |
| 1a. Identificadores inválidos | `400 Bad Request` | — (validación de ruta / model binding) | `AprobarInscripcion_WithMalformedIds_Returns400BadRequest` |
| 2a. Torneo o equipo inexistente | `404 Not Found` | `AprobarInscripcionAsync_WhenEntityDoesNotExist_ThrowsRecursoNotFoundException` | `AprobarInscripcion_WhenNotFound_Returns404NotFound` |
| 3a. Estado no pendiente | `409 Conflict` | `AprobarInscripcionAsync_WhenTeamNotPending_ThrowsEstadoEquipoInvalidoException` | `AprobarInscripcion_WhenTeamAlreadyProcessed_Returns409Conflict` |
| 3b. Cupo lleno (RN-06) | `409 Conflict` | `AprobarInscripcionAsync_WhenTournamentFull_ThrowsTorneoCupoLlenoException` | `AprobarInscripcion_WhenTournamentFull_Returns409Conflict` |
| 3c. Pago no validado (RN-06) | `422 Unprocessable Entity` | `AprobarInscripcionAsync_WhenPagoNotValidated_ThrowsPagoRequeridoException` | `AprobarInscripcion_WhenUnpaidTournament_Returns422UnprocessableEntity` |
| 3d. Rechazo con motivo (RN-03) | `200 OK` | `RechazarInscripcionAsync_WithReason_UnlocksListAndSetsObservado` | `RechazarInscripcion_WithValidReason_Returns200Ok` |
| 3d. Rechazo sin motivo | `400 Bad Request` | — (detectado por `[Required]` en DTO de rechazo) | `RechazarInscripcion_WithEmptyReason_Returns400BadRequest` |

> Regla de oro: cada flujo del caso de uso cuenta con al menos un test unitario o de integración HTTP. La suite de pruebas completa se ejecuta mediante `dotnet test`.
