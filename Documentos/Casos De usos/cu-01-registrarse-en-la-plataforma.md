# Caso de Uso: Registrarse en la plataforma (Crear cuenta)

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-01 |
| **Nombre** | Registrarse en la plataforma |
| **Actor Principal** | Usuario No Autenticado |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario → crear un perfil válido para acceder al sistema; Administración → mantener un registro único de usuarios con correos y DNI validados |
| **Disparador (Trigger)** | El usuario hace clic en el botón "Registrarse" o "No tengo cuenta" desde la pantalla de bienvenida/login |
| **Prioridad / Frecuencia** | Alta; media frecuencia |
| **Reglas de negocio relacionadas** | RN-10 (DNI y Email únicos en toda la base de datos) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un nuevo usuario (sea Organizador, Capitán, Veedor o Espectador) crear un perfil en la plataforma completando los datos obligatorios.

### 2. PRECONDICIONES
- El usuario no debe haber iniciado sesión en el sistema.
- El sistema debe estar en funcionamiento y con la Capa de Persistencia accesible.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201)
1. El Actor envía una petición al endpoint `POST /api/usuarios/registro` con los datos necesarios (Nombre, Apellido, DNI/ID, Correo Electrónico, Teléfono y Contraseña).
2. La **Capa de Presentación** valida que el formato de los datos (esquema) sea correcto.
3. La **Capa de Negocio** ejecuta las validaciones lógicas y verifica la integridad de los datos, confirmando que el email y DNI no estén duplicados (aplicando **RN-10**).
4. La **Capa de Persistencia** registra la cuenta en la Base de Datos.
5. El Sistema envía un correo de activación/confirmación al usuario.
6. El Sistema devuelve un código **201 Created** indicando éxito y que revise su correo para activar la cuenta.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Formato de Contraseña Inseguro o Datos Inválidos (HTTP 400 Bad Request):**
  1. Si en el Paso 2 la contraseña ingresada no cumple con las reglas mínimas de complejidad o faltan datos obligatorios.
  2. La **Capa de Presentación** rechaza la petición por error de esquema o validación.
  3. El Sistema devuelve un código **400 Bad Request** con el detalle del campo en conflicto. Fin del caso de uso.

* **3a. Usuario o Email duplicado (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que el DNI o el Email ya existen en la base de datos (violando **RN-10**).
  2. La **Capa de Negocio** lanza una excepción de dominio por conflicto de unicidad.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje de error: "El correo o DNI ya se encuentra registrado en la plataforma". Fin del caso de uso.

### 5. SUB-VARIACIONES
- El usuario puede registrarse seleccionando un rol específico deseado (Organizador, Capitán, Veedor, Espectador) durante la carga de datos.

### 6. POSTCONDICIONES
- Se crea el perfil del usuario en la base de datos en estado "Pendiente de Confirmación" o "Activo".
- Se dispara el envío del correo de activación correspondiente.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Confirmación de registro exitoso del usuario en el sistema. |
| `400` | Bad Request | Fallo en la validación de formato de contraseña o datos obligatorios. |
| `409` | Conflict | Violación de regla RN-10 por DNI o Email ya existente. |

### Matriz de trazabilidad CU-01 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `RegisterUsuarioAsync_WithValidData_ReturnsCreated` | `RegisterUsuario_ReturnsSuccessAndCreated` |
| 2a. Formato/Datos Inválidos | `400 Bad Request` | — (validación de esquema DTO) | `RegisterUsuario_WithInvalidPassword_Returns400BadRequest` |
| 3a. DNI/Email duplicado | `409 Conflict` | `RegisterUsuarioAsync_WhenEmailOrDniExists_ThrowsConflictException` | `RegisterUsuario_WhenDuplicateData_Returns409Conflict` |
