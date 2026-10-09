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

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor envía una petición al endpoint `POST /api/torneos/{id}/generar-fixture` con los parámetros de configuración (fecha de inicio, horarios y disponibilidad de canchas/servidores).
2. La **Capa de Presentación** valida que los datos de la configuración del fixture sean correctos.
3. La **Capa de Negocio** verifica que el torneo esté en fase de inscripción y cuente con la cantidad de equipos confirmados necesaria.
4. El Sistema invoca al proceso de generación de fixture (Caso de Uso **CU-16**).
5. La **Capa de Persistencia** cierra la inscripción del torneo (cambia estado a "En Desarrollo") y guarda los partidos generados.
6. El Sistema devuelve un código **200 OK** confirmando que el fixture se generó exitosamente y se cerró la inscripción.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Parámetros de generación inválidos (HTTP 400 Bad Request):**
  1. Si en el Paso 2 no se envían los horarios o sedes necesarias para el algoritmo.
  2. La **Capa de Presentación** rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **3a. No se alcanzó el cupo mínimo (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que la cantidad de equipos confirmados es menor al mínimo reglamentario para crear el torneo.
  2. La **Capa de Negocio** frena la ejecución y cancela el pedido de generación.
  3. El Sistema devuelve un código **409 Conflict** con el error: "No se puede generar el fixture. No se alcanza el mínimo de equipos requeridos". Fin del caso de uso.

* **3b. Torneo no apto para generar fixture (HTTP 409 Conflict):**
  1. Si en el Paso 3 el torneo ya tiene un fixture generado o no se encuentra en estado "Abierto".
  2. La **Capa de Negocio** detecta la violación de estado y lanza una excepción.
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
| `400` | Bad Request | Falta de parámetros obligatorios de fechas u horarios. |
| `409` | Conflict | Cupo mínimo no alcanzado, o torneo en estado inválido (ej. ya en desarrollo). |

### Matriz de trazabilidad CU-08 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GenerarFixtureAsync_WithValidData_ReturnsSuccess` | `GenerarFixture_Returns200OK` |
| 2a. Parámetros inválidos | `400 Bad Request` | — (validación DTO) | `GenerarFixture_WithMissingParams_Returns400BadRequest` |
| 3a. Sin cupo mínimo | `409 Conflict` | `GenerarFixtureAsync_WithoutMinimumTeams_ThrowsConflictException` | `GenerarFixture_WhenMinimumTeamsNotReached_Returns409Conflict` |
| 3b. Estado inválido | `409 Conflict` | `GenerarFixtureAsync_WhenNotOpen_ThrowsConflictException` | `GenerarFixture_WhenTorneoNotOpen_Returns409Conflict` |
