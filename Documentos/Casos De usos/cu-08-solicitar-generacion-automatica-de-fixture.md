# Caso de Uso: Solicitar generación automática de fixture

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-08 |
| **Nombre** | Solicitar generación automática de fixture |
| **Actor Principal** | Organizador / Administrador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Organizador → generar automáticamente el cronograma y cruces evitando errores manuales; Sistema → invocar correctamente el algoritmo del fixture (CU-16) |
| **Disparador (Trigger)** | El Organizador hace clic en "Cerrar Inscripciones y Generar Fixture" |
| **Prioridad / Frecuencia** | Alta; baja frecuencia (una vez por torneo) |
| **Reglas de negocio relacionadas** | RN-08 (Cierre de inscripciones tras generar fixture) |

---

### 1. BREVE DESCRIPCIÓN
Interfaz e instrucción donde el Organizador valida el cierre de la fase de inscripción e inicia la orden para que el sistema procese el calendario de partidos según los cupos.

### 2. PRECONDICIONES
- El Organizador debe estar autenticado.
- El torneo debe tener la fase de inscripciones abierta.
- La cantidad de equipos inscritos debe alcanzar el mínimo permitido para la disciplina y modalidad.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor envía una petición al endpoint `POST /api/torneos/{id}/generar-fixture` con los parámetros de configuración en `GenerarFixtureRequestDTO` (`fechaInicio`, `diasJuego`, `horaInicio`, `horaFin`, `duracionPartidoMinutos`, `canchasDisponibles`, `intervaloMinutos`).
2. La **Capa de Presentación** (`TorneosController.GenerarFixture`) valida que el formato de los datos sea correcto:
   - `fechaInicio`: válida (`>= DateTime.Today`), año `<= 2100`.
   - `horaInicio` y `horaFin`: formato `HH:mm` con `horaFin > horaInicio`.
   - `duracionPartidoMinutos` e `intervaloMinutos`: valores entre 10 y 240 minutos.
   - `canchasDisponibles`: lista no vacía, sin sedes duplicadas ni con nombres mayores a 100 caracteres.
3. La **Capa de Negocio** (`FixtureService.GenerarFixtureAsync`):
   - Verifica la existencia del torneo en la base de datos (lanza `NotFoundException` 404 si no existe).
   - Verifica que el torneo se encuentre en un estado apto (`Abierto` o `InscripcionesCerradas`).
   - Comprueba que la cantidad de equipos confirmados cumpla el mínimo reglamentario (mínimo 2 para Liga, 4 para Eliminación Directa).
4. El Sistema invoca al motor de generación de fixture (Caso de Uso **CU-16**), garantizando el cumplimiento de las reglas **RN-08** y **RN-09**.
5. La **Capa de Persistencia** cierra la inscripción del torneo (cambia estado a `EnDesarrollo`), bloquea futuras inscripciones y persiste los partidos generados con estado `Pendiente`.
6. El Sistema devuelve un código **200 OK** con el payload `GenerarFixtureResponseDTO` confirmando el total de partidos, fechas y jornadas programadas.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Parámetros de generación inválidos o fechas absurdas (HTTP 400 Bad Request):**
  1. Si en el Paso 2 no se envían canchas, los nombres de cancha están vacíos/repetidos, o la fecha de inicio es anterior a hoy, o las horas son inconsistentes (`horaFin <= horaInicio`).
  2. La **Capa de Presentación** rechaza la petición o la **Capa de Negocio** lanza `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el formato unificado `{ "error": "mensaje descriptivo" }`. Fin del caso de uso.

* **3a. Torneo Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del torneo no existe en la base de datos.
  2. La **Capa de Negocio** lanza `NotFoundException`.
  3. El Sistema devuelve un código **404 Not Found**. Fin del caso de uso.

* **3b. No se alcanzó el cupo mínimo (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que la cantidad de equipos confirmados es menor al mínimo reglamentario para crear el fixture.
  2. La **Capa de Negocio** frena la ejecución y lanza `TorneoCupoMinimoNoAlcanzadoException` / `BusinessRuleConflictException`.
  3. El Sistema devuelve un código **409 Conflict** con el error: `"No se puede generar el fixture. No se alcanza el mínimo de equipos requeridos"`. Fin del caso de uso.

* **3c. Torneo no apto para generar fixture (HTTP 409 Conflict):**
  1. Si en el Paso 3 el torneo ya tiene un fixture generado o se encuentra en un estado incompatible (ej. ya `EnDesarrollo` o `Finalizado`).
  2. La **Capa de Negocio** detecta la violación de estado y lanza `TorneoEstadoInvalidoException` / `BusinessRuleConflictException`.
  3. El Sistema devuelve un error **409 Conflict**. Fin del caso de uso.

### 5. SUB-VARIACIONES
- No presenta variaciones significativas.

### 6. POSTCONDICIONES
- Se cierra definitivamente la inscripción del torneo.
- Se invoca y completa el proceso de cálculo de partidos (creación de la tabla `Partidos` en la base de datos).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Generación del fixture y cierre de inscripciones exitoso. |
| `400` | Bad Request | Falta de parámetros obligatorios de fechas u horarios, o franjas horarias inválidas. |
| `404` | Not Found | Torneo inexistente. |
| `409` | Conflict | Cupo mínimo no alcanzado, o torneo en estado inválido (ej. ya en desarrollo). |

### Matriz de trazabilidad CU-08 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GenerarFixtureAsync_WithValidData_ReturnsSuccess`<br>`GenerarFixtureAsync_EliminacionDirecta_ReturnsSuccess` | `GenerarFixture_Returns200OK` |
| 2a. Parámetros inválidos | `400 Bad Request` | — (validación DTO DataAnnotations) | `GenerarFixture_WithMissingParams_Returns400BadRequest`<br>`Vector5_FechasYHorariosAbsurdosEnFixture_Retorna400BadRequest` |
| 3a. Torneo inexistente | `404 Not Found` | `GenerarFixtureAsync_WhenTorneoNotFound_ThrowsNotFoundException` | `GenerarFixture_WhenTorneoNotFound_Returns404NotFound`<br>`CU08_GenerarFixture_TorneoInexistente_Retorna404NotFound` |
| 3b. Sin cupo mínimo | `409 Conflict` | `GenerarFixtureAsync_WithoutMinimumTeams_ThrowsConflictException` | `GenerarFixture_WhenMinimumTeamsNotReached_Returns409Conflict`<br>`CU08_GenerarFixture_SinCupoMinimoConfirmados_Retorna409Conflict` |
| 3c. Estado inválido | `409 Conflict` | `GenerarFixtureAsync_WhenNotOpen_ThrowsConflictException` | `GenerarFixture_WhenTorneoNotOpen_Returns409Conflict` |
