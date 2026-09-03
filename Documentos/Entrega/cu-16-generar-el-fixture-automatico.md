# Caso de Uso: Generar el Fixture Automático

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-08 (bloqueo permanente de inscripciones tras generación de fixture) y RN-09 (prohibición de disputar más de un partido por día calendario por equipo) **implementadas** en el código; cada caso borde cuenta con su test unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-16 |
| **Nombre** | Generar el fixture automático |
| **Actor Principal** | Organizador / Administrador del Sistema |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | **Organizador / Administrador:** automatizar la diagramación y cálculo de llaves y jornadas sin errores de solapamiento ni esfuerzo manual.<br>**Capitanes y Jugadores:** contar con un cronograma equitativo, transparente y con anticipación suficiente, respetando los tiempos de descanso.<br>**Árbitros y Complejos Deportivos:** coordinar la utilización de canchas y turnos de arbitraje sin colisiones de horarios. |
| **Disparador (Trigger)** | El Organizador presiona el botón "Generar Fixture Automático" desde el panel de programación del torneo. |
| **Prioridad / Frecuencia** | Alta; baja frecuencia (se ejecuta una única vez por certamen tras el cierre definitivo de inscripciones). |
| **Reglas de negocio relacionadas** | RN-08 (cierre y bloqueo de inscripciones por fixture); RN-09 (límite de un partido por equipo por día calendario) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Organizador generar de forma automatizada el calendario completo de partidos y los emparejamientos del certamen (fechas, cruces de zonas o llaves de eliminación directa) utilizando los equipos formalmente confirmados, garantizando la equidad horaria y la no superposición de encuentros.

### 2. PRECONDICIONES
- El usuario debe estar autenticado en la plataforma con Token JWT válido con rol de Organizador o Administrador.
- El torneo debe encontrarse en estado "Inscripciones Cerradas".
- Debe existir la cantidad mínima reglamentaria de equipos en estado "Inscrito y Confirmado" (mínimo 4 equipos).
- La Capa de Persistencia debe encontrarse disponible para almacenar la programación íntegra de partidos de manera transaccional.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/torneos/{torneoId}/fixture` con un JSON que contiene los parámetros de programación (`fechaInicio`, `diasJuego`, `franjasHorarias`, `canchasDisponibles`, `intervaloMinutos`).
2. La **Capa de Presentación** (`FixtureController.GenerarFixture`) valida que el JSON sea sintácticamente correcto, que la `fechaInicio` sea válida y que se definan al menos una cancha y franja horaria (data annotations sobre `FixtureParametrosDTO`).
3. La **Capa de Negocio** (`FixtureService.GenerarFixtureAsync`):
   - Verifica la existencia del torneo y comprueba que su estado sea estrictamente "Inscripciones Cerradas".
   - Verifica que el certamen cuente con al menos 4 equipos confirmados.
   - Ejecuta el algoritmo de emparejamiento según la modalidad configurada:
     - En modalidad "Liga": ejecuta el algoritmo de Round Robin (todos contra todos).
     - En modalidad "Eliminación Directa": genera la estructura de llaves (Brackets) de primera ronda y ramas subsiguientes.
   - Aplica el algoritmo de asignación horaria verificando de forma estricta que ningún equipo dispute más de un partido en el mismo día calendario, en estricto cumplimiento de la regla **RN-09**.
   - Cambia el estado del torneo a "En Desarrollo" y bloquea irrevocablemente el ingreso o postulación de nuevos equipos, en cumplimiento de la regla **RN-08**.
4. La **Capa de Persistencia** almacena en bloque (batch) todos los registros generados en la tabla `Partidos` con estado "Pendiente" y actualiza la entidad `Torneo` de forma transaccional.
5. El Sistema devuelve un código **201 Created** con la cabecera `Location` y el payload de resumen del fixture (ID del fixture, total de jornadas, cantidad de partidos programados y fecha estimada de finalización).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. JSON inválido o parámetros de calendario faltantes (HTTP 400 Bad Request):**
  1. Si en el Paso 1 el payload no es un JSON válido o faltan listas de canchas o franjas horarias.
  2. El Sistema (Capa de Presentación / model binding) rechaza la solicitud.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Fecha de inicio inválida o en el pasado (HTTP 400 Bad Request):**
  1. Si en el Paso 2 la `fechaInicio` es anterior a la fecha actual del sistema.
  2. El Sistema (Capa de Presentación) rechaza la petición por validación de modelo (`ModelState.IsValid == false`).
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: `"La fecha de inicio debe ser posterior o igual a la fecha actual"`. Fin del caso de uso.

* **3a. Torneo inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el `{torneoId}` no corresponde a un torneo registrado.
  2. El Sistema (Capa de Negocio) lanza `TorneoNotFoundException`.
  3. El Sistema devuelve un código **404 Not Found** con el mensaje: `"El torneo especificado no existe"`. Fin del caso de uso.

* **3b. Torneo en estado inválido (HTTP 409 Conflict):**
  1. Si en el Paso 3 el torneo no se encuentra en estado "Inscripciones Cerradas" (ej. aún continúa en "Abierto para Inscripción" o ya se encuentra "En Desarrollo").
  2. El Sistema (Capa de Negocio) lanza `TorneoEstadoInvalidoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"El fixture solo puede generarse cuando el torneo se encuentra en estado Inscripciones Cerradas"`. Fin del caso de uso.

* **3c. Cantidad insuficiente de equipos confirmados (HTTP 422 Unprocessable Entity):**
  1. Si en el Paso 3 el certamen cuenta con menos de 4 equipos confirmados.
  2. El Sistema (Capa de Negocio) lanza `EquiposInsuficientesException`.
  3. El Sistema devuelve un código **422 Unprocessable Entity** con el mensaje: `"Se requieren al menos 4 equipos confirmados para generar el fixture"`. Fin del caso de uso.

* **3d. Cantidad impar de equipos en Todos Contra Todos / Liga (Ajuste Algorítmico - HTTP 201 Created):**
  1. Si en el Paso 3 el número de equipos confirmados para una Liga es impar.
  2. El algoritmo de Round Robin incorpora un equipo ficticio ("Bye") y asigna automáticamente una fecha libre o de descanso a un equipo diferente en cada jornada del fixture.
  3. El Sistema genera el fixture completo informando los descansos en el payload.

* **3e. Cantidad de equipos no potencia de 2 en Eliminación Directa (Ajuste Algorítmico - HTTP 201 Created):**
  1. Si en el Paso 3 la cantidad de equipos para Eliminación Directa no es potencia de 2 (ej. 13 equipos en lugar de 16).
  2. El algoritmo calcula los pases directos ("byes") necesarios para equilibrar la llave a potencia de 2 en la siguiente ronda.
  3. El Sistema genera el fixture señalizando gráficamente los cruces con pase directo.

* **3f. Disponibilidad horaria insuficiente para respetar descansos (HTTP 422 Unprocessable Entity):**
  1. Si en el Paso 3 el conjunto de canchas y franjas horarias configuradas resulta insuficiente para ubicar todos los cruces sin violar la **RN-09** (más de un partido diario por equipo).
  2. El Sistema (Capa de Negocio) frena la operación y lanza `DisponibilidadHorariaInsuficienteException`.
  3. El Sistema devuelve un código **422 Unprocessable Entity** con el mensaje: `"Disponibilidad horaria insuficiente para programar los partidos respetando la RN-09. Amplíe días u horarios"`. Fin del caso de uso.

* **4a. Falla de persistencia en bloque (HTTP 500 Internal Server Error):**
  1. Si en el Paso 4 se produce una falla técnica durante el guardado masivo de los partidos o la actualización del torneo.
  2. El Sistema revierte la transacción y genera el log de error técnico correspondiente.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El Organizador puede ejecutar una previsualización de prueba (`POST /api/torneos/{torneoId}/fixture/simular` con respuesta `200 OK`) para evaluar la propuesta de calendario antes de persistirla definitivamente.
2. La determinación de emparejamientos puede realizarse mediante sorteo ciego aleatorio o asignando cabezas de serie según el historial de rendimiento de los equipos.

### 6. POSTCONDICIONES
- El torneo cambia su estado a "En Desarrollo".
- Se genera el conjunto total de partidos en la tabla `Partidos` con estado "Pendiente", vinculados a fecha, hora, cancha y equipos.
- Queda bloqueada definitivamente la posibilidad de agregar o modificar equipos inscritos en el certamen, en cumplimiento de la regla **RN-08**.
- El fixture completo pasa a estar disponible públicamente en la plataforma para consulta de todos los interesados.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Generación exitosa de la totalidad de partidos del fixture y pase del torneo a estado "En Desarrollo". |
| `400` | Bad Request | Formato JSON roto, parámetros requeridos ausentes o fecha de inicio en el pasado. |
| `404` | Not Found | Inexistencia del torneo referenciado (`torneoId`) en la Capa de Persistencia. |
| `409` | Conflict | Torneo en estado inválido (aún en "Abierto" o ya "En Desarrollo" con fixture previo). |
| `422` | Unprocessable Entity | Menos de 4 equipos confirmados, o disponibilidad horaria insuficiente para cumplir la RN-09. |
| `500` | Internal Server Error | Falla técnica transaccional durante la persistencia en bloque de los partidos. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** formato de parámetros, validación de fechas (`fechaInicio >= DateTime.Today`) y presencia de listas no vacías de canchas y franjas horarias en `FixtureParametrosDTO` mediante `ModelState.IsValid` en `FixtureController`.
- **Verificación (Negocio, → 404/409/422):** existencia y estado del torneo (`TorneoNotFoundException` → 404; `TorneoEstadoInvalidoException` → 409); validación de mínimo de equipos (`EquiposInsuficientesException` → 422); aplicación algorítmica de la regla **RN-09** con control de superposición diaria (`DisponibilidadHorariaInsuficienteException` → 422); y cambio irreversible a "En Desarrollo" bloqueando nuevas inscripciones (**RN-08**).

### Matriz de trazabilidad CU-16 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `GenerarFixtureAsync_WithValidData_ReturnsCreatedFixtureAndLocksTournament` | `GenerarFixture_WithValidData_Returns201Created` |
| 1a. JSON inválido | `400 Bad Request` | — (model binding de ASP.NET Core) | `GenerarFixture_WithMalformedJson_Returns400BadRequest` |
| 2a. Fecha de inicio pasada | `400 Bad Request` | — (detectado por validación de DTO en Presentación) | `GenerarFixture_WithPastStartDate_Returns400BadRequest` |
| 3a. Torneo inexistente | `404 Not Found` | `GenerarFixtureAsync_WhenTorneoDoesNotExist_ThrowsTorneoNotFoundException` | `GenerarFixture_WhenTorneoNotFound_Returns404NotFound` |
| 3b. Torneo en estado no cerrado | `409 Conflict` | `GenerarFixtureAsync_WhenTorneoNotClosed_ThrowsTorneoEstadoInvalidoException` | `GenerarFixture_WhenTorneoNotClosed_Returns409Conflict` |
| 3c. Menos de 4 equipos confirmados | `422 Unprocessable Entity` | `GenerarFixtureAsync_WhenTeamsBelowFour_ThrowsEquiposInsuficientesException` | `GenerarFixture_WhenTeamsBelowFour_Returns422UnprocessableEntity` |
| 3d. Equipos impares (Liga / Byes) | `201 Created` | `GenerarFixtureAsync_WhenOddNumberOfTeams_AssignsRotativeByesCorrectly` | `GenerarFixture_WhenOddTeams_Returns201WithByes` |
| 3e. Equipos no potencia de 2 (Brackets) | `201 Created` | `GenerarFixtureAsync_WhenNonPowerOfTwoBrackets_CalculatesByesCorrectly` | `GenerarFixture_WhenNonPowerOfTwo_Returns201WithBrackets` |
| 3f. Conflicto horario (RN-09) | `422 Unprocessable Entity` | `GenerarFixtureAsync_WhenInsufficientTimeSlots_ThrowsDisponibilidadHorariaInsuficienteException` | `GenerarFixture_WhenInsufficientSlots_Returns422UnprocessableEntity` |

> Regla de oro: cada flujo del caso de uso cuenta con al menos un test unitario o de integración HTTP. La suite de pruebas completa se ejecuta mediante `dotnet test`.
