# Caso de Uso: Registrar y Dar de Alta Equipos y Listas de Jugadores

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-02 (un jugador no puede inscribirse en más de un equipo de forma simultánea en el mismo torneo) y RN-03 (bloqueo de edición de la lista de buena fe una vez enviada hasta eventual observación/rechazo) **implementadas** en el código; cada caso borde cuenta con su test unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-05 |
| **Nombre** | Registrar y dar de alta equipos y listas de jugadores |
| **Actor Principal** | Capitán de Equipo |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | **Capitán de Equipo:** registrar su equipo y la nómina oficial de jugadores asegurando la postulación en el certamen.<br>**Jugadores:** quedar formalmente acreditados con su identidad y documentación para poder disputar los partidos.<br>**Organizador del Torneo:** recibir nóminas completas que cumplan con la cantidad mínima de integrantes y sin solapamiento de jugadores entre equipos. |
| **Disparador (Trigger)** | El Capitán solicita la creación y registro de su equipo desde su panel de usuario. |
| **Prioridad / Frecuencia** | Alta; alta frecuencia durante la etapa de convocatoria e inscripciones. |
| **Reglas de negocio relacionadas** | RN-02 (jugador único por torneo según DNI); RN-03 (inmutabilidad de la lista de buena fe tras el envío) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un Capitán registrar su equipo en la plataforma y cargar la lista de buena fe con los datos y la documentación obligatoria de sus jugadores para postularse formalmente a un torneo con inscripciones abiertas.

### 2. PRECONDICIONES
- El Capitán debe poseer una cuenta activa y verificada en el sistema (Token JWT válido con rol de Capitán o Usuario registrado).
- El torneo al que se postula debe existir y encontrarse con estado "Abierto para Inscripción".
- La Capa de Persistencia debe estar disponible para almacenar el equipo y las entidades de jugadores asociadas.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/torneos/{torneoId}/equipos` con los datos del equipo (`nombre`, `logoUrl`) y la nómina de jugadores (`nombre`, `apellido`, `dni`, `documentacionUrl`).
2. La **Capa de Presentación** (`EquiposController.CreateEquipo`) valida que el JSON sea estructuralmente correcto, que los campos requeridos estén presentes y que la lista de jugadores no venga vacía (data annotations `[Required]` sobre `EquipoCreateDTO` y `JugadorDTO`).
3. La **Capa de Negocio** (`EquipoService.CreateEquipoAsync`):
   - Normaliza cadenas mediante `Trim()`.
   - Verifica que el torneo exista y esté en estado "Abierto para Inscripción".
   - Verifica que el nombre del equipo no se encuentre duplicado en el mismo torneo.
   - Comprueba que la cantidad de jugadores cumpla con el mínimo reglamentario para la disciplina deportiva asociada al torneo.
   - Valida que no existan números de DNI duplicados dentro de la misma nómina de entrada.
   - Verifica que ninguno de los jugadores (por DNI) se encuentre ya inscripto en otro equipo del mismo torneo, aplicando la regla de negocio **RN-02**.
   - Asigna al equipo el estado "Pendiente de Validación" y bloquea la posibilidad de edición directa por parte del Capitán, aplicando la regla de negocio **RN-03**.
4. La **Capa de Persistencia** genera el ID único para el equipo y persiste de forma transaccional el registro en la tabla `Equipos` y cada uno de los integrantes en la tabla `Jugadores`.
5. El Sistema devuelve un código **201 Created** con la cabecera `Location` y la información del equipo registrado (ID, nombre, estado "Pendiente de Validación" y cantidad de jugadores).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. JSON inválido o malformado (HTTP 400 Bad Request):**
  1. Si en el Paso 1 el cuerpo de la petición no es un JSON válido o contiene tipos de datos incongruentes.
  2. El Sistema (Capa de Presentación / model binding) rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Datos obligatorios faltantes en equipo o jugadores (HTTP 400 Bad Request):**
  1. Si en el Paso 2 falta el nombre del equipo o datos identificatorios obligatorios de algún jugador (`nombre`, `apellido`, `dni`).
  2. El Sistema (Capa de Presentación) rechaza la petición por validación de modelo (`ModelState.IsValid == false`).
  3. El Sistema devuelve un código **400 Bad Request** con el detalle del campo faltante. Fin del caso de uso.

* **3a. Torneo inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el `{torneoId}` no corresponde a un torneo registrado en la base de datos.
  2. El Sistema (Capa de Negocio) lanza `TorneoNotFoundException`.
  3. El Sistema devuelve un código **404 Not Found** con el mensaje: `"El torneo especificado no existe"`. Fin del caso de uso.

* **3b. Torneo no disponible para inscripción (HTTP 409 Conflict):**
  1. Si en el Paso 3 el torneo no se encuentra en estado "Abierto para Inscripción" (ej. "Inscripciones Cerradas" o "En Desarrollo").
  2. El Sistema (Capa de Negocio) lanza `TorneoInscripcionCerradaException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"El torneo no se encuentra disponible para nuevas inscripciones"`. Fin del caso de uso.

* **3c. Lista por debajo del mínimo reglamentario de jugadores (HTTP 422 Unprocessable Entity):**
  1. Si en el Paso 3 la cantidad de jugadores cargados es menor al mínimo exigido por la disciplina del torneo.
  2. El Sistema (Capa de Negocio) lanza `MinimoJugadoresInsuficienteException`.
  3. El Sistema devuelve un código **422 Unprocessable Entity** con el mensaje: `"La lista debe contener al menos el número mínimo de jugadores requeridos para la disciplina"`. Fin del caso de uso.

* **3d. DNI duplicado dentro de la misma nómina (HTTP 400 Bad Request):**
  1. Si en el Paso 3 dos o más jugadores ingresados en el payload poseen el mismo número de DNI.
  2. El Sistema (Capa de Negocio) lanza `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: `"Se encontraron DNIs duplicados en la lista de jugadores enviada"`. Fin del caso de uso.

* **3e. Jugador ya inscripto en otro equipo del mismo torneo (HTTP 409 Conflict):**
  1. Si en el Paso 3 se detecta que uno o más jugadores (según su DNI) ya figuran inscriptos en otro equipo dentro del mismo certamen, violando la regla **RN-02**.
  2. El Sistema (Capa de Negocio) frena la operación y lanza `JugadorYaInscriptoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"El jugador con DNI {dni} ya se encuentra inscripto en otro equipo de este torneo (RN-02)"`. Fin del caso de uso.

* **3f. Nombre de equipo duplicado en el torneo (HTTP 409 Conflict):**
  1. Si en el Paso 3 ya existe otro equipo con el mismo nombre registrado para dicho torneo.
  2. El Sistema (Capa de Negocio) lanza `EquipoNombreDuplicadoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"Ya existe un equipo con ese nombre en este torneo"`. Fin del caso de uso.

* **4a. Error de persistencia transaccional (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 la Capa de Persistencia falla al almacenar el equipo o la lista de jugadores.
  2. El Sistema realiza rollback de la transacción y registra el fallo técnico.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. La carga de jugadores puede realizarse de forma interactiva a través del formulario web de la plataforma o mediante importación masiva en archivo estructurado (CSV/Excel).
2. Los comprobantes o deslindes pueden ser subidos en formato binario (`multipart/form-data`) o referenciados mediante URLs seguras de almacenamiento en la nube.

### 6. POSTCONDICIONES
- El sistema almacena el registro del equipo ligado al perfil del Capitán y al torneo con estado "Pendiente de Validación".
- La lista de buena fe queda bloqueada contra modificaciones por parte del Capitán (**RN-03**) a la espera de la auditoría y validación del Organizador.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Registro exitoso del nuevo equipo y su lista de buena fe en estado Pendiente. |
| `400` | Bad Request | Formato JSON roto, datos obligatorios faltantes o DNIs repetidos dentro de la misma lista. |
| `404` | Not Found | Inexistencia del torneo referenciado (`torneoId`) en la base de datos. |
| `409` | Conflict | Torneo con inscripciones no disponibles, nombre de equipo duplicado o jugador ya inscripto en otro equipo (RN-02). |
| `422` | Unprocessable Entity | Nómina de jugadores por debajo del mínimo reglamentario para la disciplina. |
| `500` | Internal Server Error | Falla técnica no controlada en la persistencia transaccional. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** campos no nulos, formato de DNI numérico, longitud de caracteres en nombres y presencia de elementos en la lista mediante `[Required]` y `ModelState.IsValid` en `EquiposController`.
- **Verificación (Negocio, → 404/409/422):** existencia y vigencia de inscripciones del torneo (`TorneoNotFoundException` → 404; `TorneoInscripcionCerradaException` → 409); mínimo de jugadores reglamentario (`MinimoJugadoresInsuficienteException` → 422); control de duplicidad interna de DNIs (`ValidationException` → 400); y control inter-equipos de jugadores por DNI (**RN-02** vía `JugadorYaInscriptoException` → 409).

### Matriz de trazabilidad CU-05 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateEquipoAsync_WithValidData_ReturnsCreatedEquipoDTO` | `CreateEquipo_WithValidPayload_Returns201Created` |
| 1a. JSON inválido | `400 Bad Request` | — (model binding de ASP.NET Core) | `CreateEquipo_WithMalformedJson_Returns400BadRequest` |
| 2a. Datos obligatorios faltantes | `400 Bad Request` | — (detectado por `[Required]` en Presentación) | `CreateEquipo_WithMissingRequiredFields_Returns400BadRequest` |
| 3a. Torneo inexistente | `404 Not Found` | `CreateEquipoAsync_WhenTorneoDoesNotExist_ThrowsTorneoNotFoundException` | `CreateEquipo_WhenTorneoNotFound_Returns404NotFound` |
| 3b. Torneo cerrado a inscripciones | `409 Conflict` | `CreateEquipoAsync_WhenTorneoNotOpen_ThrowsTorneoInscripcionCerradaException` | `CreateEquipo_WhenTorneoClosed_Returns409Conflict` |
| 3c. Menos jugadores del mínimo | `422 Unprocessable Entity` | `CreateEquipoAsync_WhenPlayersBelowMinimum_ThrowsMinimoJugadoresInsuficienteException` | `CreateEquipo_WhenPlayersBelowMinimum_Returns422UnprocessableEntity` |
| 3d. DNI duplicado en misma lista | `400 Bad Request` | `CreateEquipoAsync_WhenDuplicateDniInList_ThrowsValidationException` | `CreateEquipo_WhenDuplicateDniInList_Returns400BadRequest` |
| 3e. Jugador ya inscripto en torneo (RN-02) | `409 Conflict` | `CreateEquipoAsync_WhenPlayerAlreadyInOtherTeam_ThrowsJugadorYaInscriptoException` | `CreateEquipo_WhenPlayerAlreadyRegisteredInTournament_Returns409Conflict` |
| 3f. Nombre de equipo duplicado | `409 Conflict` | `CreateEquipoAsync_WhenTeamNameExistsInTournament_ThrowsEquipoNombreDuplicadoException` | `CreateEquipo_WhenDuplicateTeamName_Returns409Conflict` |

> Regla de oro: cada flujo del caso de uso cuenta con al menos un test unitario o de integración HTTP. La suite de pruebas completa se ejecuta mediante `dotnet test`.
