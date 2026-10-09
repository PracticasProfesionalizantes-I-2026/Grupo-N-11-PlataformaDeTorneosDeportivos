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

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201)
1. El Actor envía una petición al endpoint `POST /api/veedores` con los datos del nuevo veedor (Nombre, Apellido, DNI, Email y Torneos/Sedes asignadas).
2. La **Capa de Presentación** valida que el formato de los datos en el esquema enviado sea correcto y los campos requeridos estén completos.
3. La **Capa de Negocio** verifica que el DNI o Email no pertenezcan ya a un usuario registrado y genera una contraseña inicial automática.
4. La **Capa de Persistencia** registra al usuario en la base de datos asignándole el rol de "Veedor" y vinculando sus torneos/sedes.
5. El Sistema envía un correo electrónico automático al nuevo veedor con sus credenciales de acceso.
6. El Sistema devuelve un código **201 Created** indicando el alta exitosa del veedor en el sistema.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Datos incompletos o malformados (HTTP 400 Bad Request):**
  1. Si en el Paso 2 faltan campos obligatorios o el email posee un formato incorrecto.
  2. La **Capa de Presentación** rechaza la petición.
  3. El Sistema devuelve un código **400 Bad Request** detallando el error. Fin del caso de uso.

* **3a. Veedor ya existente (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que el DNI o Email ya se encuentran registrados para otro usuario.
  2. La **Capa de Negocio** lanza una excepción de dominio por conflicto de unicidad.
  3. El Sistema devuelve un código **409 Conflict** indicando: "El DNI o Email ya están registrados en el sistema". Fin del caso de uso.

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
| `201` | Created | Alta exitosa del veedor y su cuenta. |
| `200` | OK | Modificación exitosa de los datos de un veedor. |
| `204` | No Content | Baja o desactivación exitosa del veedor. |
| `400` | Bad Request | Datos de creación de veedor incompletos. |
| `409` | Conflict | El Email o DNI provisto ya está registrado. |

### Matriz de trazabilidad CU-07 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateVeedorAsync_WithValidData_ReturnsCreated` | `CreateVeedor_WithValidData_Returns201Created` |
| 2a. Datos incompletos | `400 Bad Request` | — (validación DTO) | `CreateVeedor_WithMissingData_Returns400BadRequest` |
| 3a. DNI/Email existente | `409 Conflict` | `CreateVeedorAsync_WithExistingDni_ThrowsConflictException` | `CreateVeedor_WhenDuplicateData_Returns409Conflict` |
| Sub-variación (Modificar) | `200 OK` | `UpdateVeedorAsync_WithValidData_UpdatesVeedor` | `UpdateVeedor_Returns200OK` |
| Sub-variación (Desactivar) | `204 No Content` | `DeactivateVeedorAsync_UpdatesStatus` | `DeactivateVeedor_Returns204NoContent` |
