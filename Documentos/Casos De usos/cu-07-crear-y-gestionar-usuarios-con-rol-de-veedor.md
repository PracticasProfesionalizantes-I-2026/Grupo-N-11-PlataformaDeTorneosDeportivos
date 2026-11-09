# Caso de Uso: Crear y gestionar usuarios con rol de "Veedor"

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-07 |
| **Nombre** | Crear y gestionar usuarios con rol de "Veedor" |
| **Actor Principal** | Organizador / Administrador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Organizador → delegar el control y carga de partidos en personal autorizado; Veedor → obtener sus credenciales para acceder a sus tareas |
| **Disparador (Trigger)** | El Organizador ingresa al apartado "Gestión de Personal/Veedores" dentro de su panel administrativo y selecciona "Nuevo Veedor" |
| **Prioridad / Frecuencia** | Media; baja frecuencia |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Permite al Organizador dar de alta, modificar permisos o dar de baja cuentas para veedores o árbitros que tendrán acceso a la planilla digital de carga de partidos.

### 2. PRECONDICIONES
- El Organizador debe estar autenticado en la plataforma.
- El sistema de roles y envío de correos debe estar operativo.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/veedores` con los datos del nuevo veedor (`Nombre`, `Apellido`, `Dni`, `Email`, `Telefono`, lista de `TorneoIds`, y opcionalmente `PasswordInicial`).
2. La **Capa de Presentación** (`VeedoresController.CreateVeedor`) valida que el formato de los datos en `VeedorCreateDTO` sea correcto:
   - `Dni`: exactamente 7 u 8 dígitos numéricos (`^\d{7,8}$`).
   - `Telefono`: numérico con prefijo `+` opcional, de 8 a 15 dígitos (`^\+?[0-9]{8,15}$`).
   - `Nombre` y `Apellido`: entre 2 y 60 caracteres alfabéticos (sin números ni caracteres especiales).
   - `Email`: formato de correo válido.
   - `PasswordInicial` (opcional): si se suministra, mínimo 8 caracteres con letras y números.
3. La **Capa de Negocio** (`VeedorService.CreateVeedorAsync`) normaliza los textos (`Trim()`), verifica que el DNI o Email no pertenezcan ya a un usuario registrado (regla **RN-10**) y genera una contraseña inicial provisoria (o utiliza `PasswordInicial`). Marca la cuenta con `RequiereCambioPassword = true`.
4. La **Capa de Persistencia** registra al usuario en la base de datos asignándole el rol `RolUsuario.Veedor` y vinculando sus torneos/sedes.
5. El Sistema devuelve un código **201 Created** con el payload `VeedorResponseDTO` conteniendo la `PasswordTemporal` generada y la bandera `RequiereCambioPassword = true` para que el administrador pueda comunicársela al veedor. (En consultas posteriores `GET /api/veedores/{id}` o listados, `PasswordTemporal` se mantiene en `null` por seguridad).
6. En el primer inicio de sesión (`POST /api/auth/login`), la respuesta indica `RequiereCambioPassword = true`. El veedor debe invocar `POST /api/auth/cambiar-password` (o `POST /api/auth/primer-ingreso`) con `CambiarPasswordInicialDTO`, ingresando su contraseña actual y una nueva contraseña distinta. Al completarse con éxito, se actualiza el hash de la clave y `RequiereCambioPassword` queda en `false`.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Datos incompletos o malformados (HTTP 400 Bad Request):**
  1. Si en el Paso 2 faltan campos obligatorios, el DNI contiene letras o longitud distinta de 7-8 dígitos, el teléfono contiene letras, o el nombre posee números.
  2. La **Capa de Presentación** o la **Capa de Negocio** rechaza la petición lanzando `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el formato unificado `{ "error": "mensaje descriptivo" }`. Fin del caso de uso.

* **3a. Veedor ya existente (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que el DNI o Email ya se encuentran registrados para otro usuario (violando **RN-10**).
  2. La **Capa de Negocio** lanza `BusinessRuleConflictException`.
  3. El Sistema devuelve un código **409 Conflict** indicando: `"El correo electrónico o DNI ya se encuentra registrado"`. Fin del caso de uso.

* **6a. Intento de cambio con contraseña idéntica a la provisoria (HTTP 400 Bad Request):**
  1. Si al cambiar la contraseña provisoria el veedor suministra una nueva contraseña idéntica a la actual.
  2. La **Capa de Negocio** rechaza la operación lanzando `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el mensaje descriptivo correspondiente.

* **6b. Contraseña actual incorrecta en cambio obligatorio (HTTP 401 Unauthorized):**
  1. Si el veedor suministra credenciales o contraseña actual errónea al intentar cambiar su contraseña.
  2. La **Capa de Negocio** lanza `UnauthorizedException`.
  3. El Sistema devuelve un código **401 Unauthorized**.

### 5. SUB-VARIACIONES
* **1a. Modificación o Desvinculación de Veedor:**
  1. El Organizador envía una petición a `PUT /api/veedores/{id}` o `DELETE /api/veedores/{id}` para actualizar asignaciones o desactivar la cuenta.
  2. El Sistema actualiza el estado (HTTP 200) o inactiva la cuenta (HTTP 204), bloqueando los accesos inmediatos del veedor a la carga de datos del torneo.

### 6. POSTCONDICIONES
- Queda habilitado (o deshabilitado) el perfil de Veedor en la base de datos para la carga de partidos de los torneos correspondientes.
- El Veedor recibe la notificación con sus credenciales.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Alta exitosa del veedor y su cuenta (incluye PasswordTemporal y RequiereCambioPassword). |
| `200` | OK | Modificación exitosa de los datos de un veedor o cambio de contraseña. |
| `204` | No Content | Baja o desactivación exitosa del veedor. |
| `400` | Bad Request | Datos de creación de veedor incompletos o cambio con contraseña idéntica a la provisoria. |
| `401` | Unauthorized | Contraseña actual incorrecta en cambio obligatorio. |
| `409` | Conflict | El Email o DNI provisto ya está registrado. |

### Matriz de trazabilidad CU-07 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateVeedorAsync_WithValidData_ReturnsCreated`<br>`CreateVeedorAsync_WithCustomPasswordInicial_UsesPasswordAndSetsRequiereCambio` | `CreateVeedor_WithValidData_Returns201Created`<br>`CreateVeedor_WithCustomPasswordInicial_Returns201CreatedAndPassword` |
| 2a. Datos incompletos | `400 Bad Request` | — (validación DTO DataAnnotations) | `CreateVeedor_WithMissingData_Returns400BadRequest` |
| 2a. DNI con formato inválido | `400 Bad Request` | `CreateVeedorAsync_WhenDniContainsLetters_ThrowsValidationException` | `RegisterUsuario_WithLettersInDni_Returns400BadRequest`<br>`Vector2_AtaquesAlDni_Retorna400BadRequest` |
| 3a. DNI/Email existente (RN-10) | `409 Conflict` | `CreateVeedorAsync_WithExistingDni_ThrowsConflictException` | `CreateVeedor_WhenDuplicateData_Returns409Conflict` |
| 6. Primer ingreso y cambio obligatorio | `200 OK` / `400` / `401` | `CambiarPasswordInicialAsync_WithValidData_UpdatesPasswordAndClearsFlag`<br>`CambiarPasswordInicialAsync_WhenSamePassword_ThrowsValidationException`<br>`CambiarPasswordInicialAsync_WhenWrongCurrentPassword_ThrowsUnauthorizedException` | `FlujoPrimerIngresoVeedor_CambioPasswordObligatorio_FuncionaCorrectamente` |
| Sub-variación (Modificar) | `200 OK` | `UpdateVeedorAsync_WithValidData_UpdatesVeedor` | `UpdateVeedor_Returns200OK` |
| Sub-variación (Desactivar) | `204 No Content` | `DeactivateVeedorAsync_UpdatesStatus` | `DeactivateVeedor_Returns204NoContent` |
| Seguridad en consulta | `200 OK` | `GetByIdAsync_DoesNotExposePasswordTemporal` | `CreateVeedor_WithValidData_Returns201Created` (GET posterior) |
