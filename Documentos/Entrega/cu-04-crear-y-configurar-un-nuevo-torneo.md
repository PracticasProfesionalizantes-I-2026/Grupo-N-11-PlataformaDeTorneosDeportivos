# Caso de Uso: Crear y Configurar un Nuevo Torneo

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).
> Reglas de negocio RN-04 (cupo en eliminación directa potencia de 2, mínimo 4 equipos) y RN-05 (cuenta recaudadora activa obligatoria en torneos pagos) **implementadas** en el código; cada caso borde cuenta con su test unitario e integración (ver matriz de trazabilidad).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-04 |
| **Nombre** | Crear y configurar un nuevo torneo |
| **Actor Principal** | Organizador / Administrador del Sistema |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | **Organizador / Administrador:** dar de alta competencias deportivas definiendo parámetros esenciales (disciplina, cupos, costo, modalidad) para habilitar inscripciones.<br>**Capitanes / Equipos:** consultar torneos disponibles con reglas y condiciones de participación transparentes.<br>**Administración:** asegurar que los torneos arancelados posean una cuenta recaudadora válida para canalizar los fondos. |
| **Disparador (Trigger)** | El Organizador selecciona la opción "Crear Torneo" desde su panel de gestión administrativa. |
| **Prioridad / Frecuencia** | Alta; frecuencia media (altas de competencias al inicio de cada temporada o circuito). |
| **Reglas de negocio relacionadas** | RN-04 (cupo potencia de 2 en eliminación directa); RN-05 (cuenta recaudadora activa para torneos pagos) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Organizador del sistema dar de alta una nueva competencia deportiva configurando sus parámetros esenciales (nombre, disciplina, modalidad, cupo máximo y arancel de inscripción) para habilitar la posterior postulación e inscripción de equipos.

### 2. PRECONDICIONES
- El usuario debe estar autenticado en la plataforma con un Token JWT válido con rol de Organizador o Administrador.
- Las disciplinas base (Fútbol, E-Sports, etc.) deben estar previamente registradas y activas en la Capa de Persistencia.
- El sistema debe encontrarse operativo y con la Capa de Persistencia accesible.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/torneos` con un JSON que contiene los datos de la competencia (`nombre`, `disciplinaId`, `modalidad`, `cupoMaximo`, `esPago`, `costoInscripcion`).
2. La **Capa de Presentación** (`TorneosController.CreateTorneo`) valida que el JSON sea sintácticamente correcto y que los campos obligatorios estén presentes y cumplan las restricciones de formato (data annotations `[Required]`, `[StringLength]` sobre `TorneoCreateDTO`).
3. La **Capa de Negocio** (`TorneoService.CreateTorneoAsync`) normaliza los campos de texto (`Trim()`), verifica que la disciplina referenciada exista y comprueba que no exista otro torneo registrado con el mismo nombre.
4. La **Capa de Negocio** valida que si la modalidad es "Eliminación Directa" (Brackets), el cupo máximo de equipos sea estrictamente una potencia de 2 y mayor o igual a 4, aplicando la regla de negocio **RN-04**.
5. La **Capa de Negocio** valida que si el torneo es configurado como "Pago" (`esPago == true`), el monto sea estrictamente mayor a cero y que la cuenta recaudadora de la organización se encuentre vinculada y activa, aplicando la regla de negocio **RN-05**.
6. La **Capa de Persistencia** genera un nuevo identificador único (GUID/ID) y almacena el registro en la tabla `Torneos` con estado "Abierto para Inscripción".
7. El Sistema devuelve un código **201 Created** con la cabecera `Location` y el payload resultante (ID, nombre, disciplina, modalidad, cupo, costo y estado).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. JSON inválido o malformado (HTTP 400 Bad Request):**
  1. Si en el Paso 1 el cuerpo de la petición no es un JSON válido (sintaxis corrupta, cuerpo vacío o tipos de datos incompatibles).
  2. El Sistema (Capa de Presentación / model binding) rechaza la petición por error de esquema.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **2a. Campo obligatorio faltante o vacío (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON no incluye `nombre`, `disciplinaId`, `modalidad` o `cupoMaximo`.
  2. El Sistema (Capa de Presentación) rechaza la petición por validación de modelo (`ModelState.IsValid == false`).
  3. El Sistema devuelve un código **400 Bad Request** detallando el campo faltante. Fin del caso de uso.

* **2b. Disciplina inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 la `disciplinaId` proporcionada no existe en la base de datos o se encuentra inactiva.
  2. El Sistema (Capa de Negocio) no encuentra la entidad correspondiente y lanza `DisciplinaNotFoundException`.
  3. El Sistema devuelve un código **404 Not Found** con el mensaje: `"La disciplina especificada no existe"`. Fin del caso de uso.

* **3a. Nombre de torneo duplicado (HTTP 409 Conflict):**
  1. Si en el Paso 3 se detecta que ya existe otro torneo con el mismo nombre registrado en el sistema.
  2. El Sistema (Capa de Negocio) frena la ejecución y lanza `TorneoNombreDuplicadoException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje: `"Ya existe un torneo con el nombre ingresado"`. Fin del caso de uso.

* **4a. Cupo inválido para modalidad Eliminación Directa (HTTP 422 Unprocessable Entity):**
  1. Si en el Paso 4 la modalidad es Eliminación Directa y el cupo no es potencia de 2 (ej. 6, 10, 14 equipos) o es menor a 4, violando la regla **RN-04**.
  2. El Sistema (Capa de Negocio) lanza `CupoInvalidoEliminacionException`.
  3. El Sistema devuelve un código **422 Unprocessable Entity** con el mensaje: `"Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)"`. Fin del caso de uso.

* **5a. Monto de inscripción inválido en torneo pago (HTTP 400 Bad Request):**
  1. Si en el Paso 5 el torneo está marcado como pago (`esPago == true`) pero el `costoInscripcion` es menor o igual a cero.
  2. El Sistema (Capa de Negocio) lanza `CostoInvalidoException`.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje: `"El costo debe ser mayor a cero para torneos pagos"`. Fin del caso de uso.

* **5b. Cuenta recaudadora no vinculada o inactiva (HTTP 422 Unprocessable Entity):**
  1. Si en el Paso 5 el torneo es de pago pero la cuenta recaudadora de la organización no se encuentra activa en el sistema, violando la regla **RN-05**.
  2. El Sistema (Capa de Negocio) interrumpe la publicación y lanza `CuentaRecaudadoraInactivaException`.
  3. El Sistema devuelve un código **422 Unprocessable Entity** con el mensaje: `"No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)"`. Fin del caso de uso.

* **6a. Error no controlado en la Capa de Persistencia (HTTP 500 Internal Server Error):**
  1. Si en el Paso 6 la Capa de Persistencia no puede guardar el registro por fallo de conectividad o corrupción de base de datos.
  2. El Sistema interrumpe la operación y registra la excepción técnica en el log de auditoría.
  3. El Sistema devuelve un código **500 Internal Server Error**. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
1. El Organizador puede crear el torneo desde el panel web de gestión administrativa, desde la app móvil o mediante herramientas de prueba de API (Swagger, Scalar, Postman, Bruno).
2. El torneo puede configurarse como Gratuito (`esPago = false`, costo cero sin validación de cuenta recaudadora) o Pago (`esPago = true`, con validación obligatoria de cuenta recaudadora según RN-05).

### 6. POSTCONDICIONES
- Se genera un nuevo registro persistente en la tabla `Torneos` con ID único en estado "Abierto para Inscripción".
- El torneo queda publicado y visible en la cartelera general pública, habilitando la recepción de solicitudes de equipos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Confirmación de persistencia exitosa del nuevo recurso Torneo. |
| `400` | Bad Request | Formato JSON roto, datos obligatorios faltantes o monto de inscripción inválido (<= 0 en torneos pagos). |
| `404` | Not Found | Inexistencia de la disciplina referenciada (`disciplinaId`) en la Capa de Persistencia. |
| `409` | Conflict | Violación de unicidad de nombre de torneo. |
| `422` | Unprocessable Entity | Violación de reglas de negocio de dominio (RN-04: cupo no es potencia de 2 en eliminación directa; RN-05: cuenta recaudadora inactiva). |
| `500` | Internal Server Error | Error técnico no controlado durante la persistencia en la base de datos. |

### Nota: Validación vs. Verificación aplicada

- **Validación (Presentación, → 400):** campos obligatorios, cadenas vacías y tipos numéricos correctos mediante Data Annotations (`[Required]`, `[StringLength]`, `[Range]`) en `TorneoCreateDTO` y validación de `ModelState.IsValid` en `TorneosController`.
- **Verificación (Negocio, → 404/409/422):** normalización `Trim()` de nombres; existencia de disciplina en base de datos (`DisciplinaNotFoundException` → 404); unicidad de nombre de torneo (`TorneoNombreDuplicadoException` → 409); validación algorítmica de potencia de 2 para Brackets (**RN-04** → 422); y verificación del estado activo de la cuenta recaudadora para torneos arancelados (**RN-05** → 422).

### Matriz de trazabilidad CU-04 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateTorneoAsync_WithValidData_ReturnsCreatedTorneoDTO` | `CreateTorneo_WithValidPayload_Returns201Created` |
| 1a. JSON inválido | `400 Bad Request` | — (model binding de ASP.NET Core) | `CreateTorneo_WithMalformedJson_Returns400BadRequest` |
| 2a. Campo obligatorio faltante | `400 Bad Request` | — (detectado por `[Required]` en Presentación) | `CreateTorneo_WithMissingRequiredFields_Returns400BadRequest` |
| 2b. Disciplina inexistente | `404 Not Found` | `CreateTorneoAsync_WhenDisciplinaDoesNotExist_ThrowsDisciplinaNotFoundException` | `CreateTorneo_WhenDisciplinaNotFound_Returns404NotFound` |
| 3a. Nombre de torneo duplicado | `409 Conflict` | `CreateTorneoAsync_WhenNameAlreadyExists_ThrowsTorneoNombreDuplicadoException` | `CreateTorneo_WhenDuplicateName_Returns409Conflict` |
| 4a. Cupo no potencia de 2 (RN-04) | `422 Unprocessable Entity` | `CreateTorneoAsync_WhenEliminacionDirectaAndCupoNotPowerOfTwo_ThrowsCupoInvalidoEliminacionException` | `CreateTorneo_WhenEliminacionCupoInvalid_Returns422UnprocessableEntity` |
| 5a. Monto <= 0 en torneo pago | `400 Bad Request` | `CreateTorneoAsync_WhenPagoAndCostoZeroOrNegative_ThrowsCostoInvalidoException` | `CreateTorneo_WhenPagoWithInvalidCosto_Returns400BadRequest` |
| 5b. Cuenta recaudadora inactiva (RN-05) | `422 Unprocessable Entity` | `CreateTorneoAsync_WhenPagoAndCuentaRecaudadoraInactive_ThrowsCuentaRecaudadoraInactivaException` | `CreateTorneo_WhenCuentaRecaudadoraInactive_Returns422UnprocessableEntity` |

> Regla de oro: cada flujo del caso de uso cuenta con al menos un test unitario o de integración HTTP. La suite de pruebas completa se ejecuta mediante `dotnet test`.
