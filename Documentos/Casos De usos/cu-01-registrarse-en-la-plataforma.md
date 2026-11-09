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
| **Reglas de negocio relacionadas** | RN-10 (DNI y Email únicos en toda la base de datos); RNF-04 (Prevención de Escalada de Privilegios: rol base forzado en registro público) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un nuevo usuario crear un perfil en la plataforma completando los datos obligatorios. Por diseño de seguridad (RNF-04), el registro público únicamente otorga el rol base (`Capitan` / `Espectador`), neutralizando cualquier intento de asignación no autorizada de roles elevados (`Administrador`, `Organizador`, `Veedor`). La concesión de roles administrativos se gestiona exclusivamente a través de endpoints administrativos protegidos.

### 2. PRECONDICIONES
- El usuario no debe haber iniciado sesión en el sistema.
- El sistema debe estar en funcionamiento y con la Capa de Persistencia accesible.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 201 Created)
1. El Actor envía una petición al endpoint `POST /api/usuarios/registro` con los datos necesarios (`Nombre`, `Apellido`, `Dni`, `Email`, `Telefono`, `Password`, y opcionalmente `Rol`).
2. La **Capa de Presentación** (`UsuariosController.Registrar`) valida que el formato de los datos (esquema DataAnnotations de `UsuarioRegistroDTO`) sea correcto:
   - `Dni`: exactamente de 7 u 8 dígitos numéricos (`^\d{7,8}$`).
   - `Telefono`: formato telefónico válido de 8 a 15 dígitos con prefijo `+` opcional (`^\+?[0-9]{8,15}$`).
   - `Nombre` y `Apellido`: entre 2 y 50 caracteres alfabéticos (sin números ni símbolos especiales).
   - `Email`: formato de correo electrónico válido y longitud máxima de 100 caracteres.
   - `Password`: mínimo 8 caracteres, conteniendo al menos una letra y un número.
   - `Rol`: si se envía, debe corresponder a un valor definido en el enumerador `RolUsuario` (`Enum.IsDefined`).
3. La **Capa de Negocio** (`UsuarioService.RegisterUsuarioAsync`):
   - Normaliza los textos con `.Trim()`, verifica la integridad de los datos y confirma que el DNI y el Email no existan previamente en la base de datos (aplicando la regla **RN-10**).
   - Aplica el principio de mínimo privilegio y **Prevención de Escalada de Privilegios (RNF-04)**: si la petición pública contiene un rol elevado (`Administrador`, `Organizador`, `Veedor`), el sistema lo ignora y fuerza el rol base (`Capitan`).
4. La **Capa de Persistencia** registra la cuenta en la tabla `Usuarios` con la contraseña procesada mediante hash criptográfico seguro (`IPasswordHasher`).
5. El Sistema envía un correo de activación/confirmación al usuario.
6. El Sistema devuelve un código **201 Created** con el payload `UsuarioResponseDTO` (ID, Nombre, Apellido, Email, Rol, Estado).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Formato de Contraseña Inseguro o Datos Inválidos (HTTP 400 Bad Request):**
  1. Si en el Paso 2 la contraseña no cumple la complejidad mínima, o el DNI contiene letras/longitud distinta a 7-8 dígitos, o el teléfono posee letras/longitud menor a 8 dígitos, o el nombre contiene números/símbolos, o el `Rol` es un entero fuera de rango (ej. 99).
  2. La **Capa de Presentación** rechaza la petición por error de validación de modelo o la **Capa de Negocio** lanza `ValidationException`.
  3. El Sistema devuelve un código **400 Bad Request** con el formato JSON unificado `{ "error": "mensaje descriptivo" }`. Fin del caso de uso.

* **2b. Intento de Escalada de Privilegios en Registro Público (Prevención RNF-04 - Rol Forzado a Capitan):**
  1. Si un usuario no autenticado envía en el payload público (`POST /api/usuarios/registro` o `POST /api/auth/register`) un rol administrativo (`Administrador`, `Organizador` o `Veedor`).
  2. El Sistema intercepta la petición, valida la integridad de los datos, neutraliza la inyección del rol y registra la cuenta con el rol base (`Capitan`).
  3. Si dicho usuario inicia sesión e intenta invocar endpoints protegidos (ej. `POST /api/veedores` o `POST /api/torneos`), el Sistema rechaza la operación con código **403 Forbidden**.

* **2c. Creación o Modificación no Autorizada de Roles Elevados (HTTP 401 Unauthorized / HTTP 403 Forbidden):**
  1. Si un usuario anónimo intenta acceder a los endpoints administrativos de creación o asignación de roles (`POST /api/usuarios/admin`, `PUT /api/usuarios/{id}/rol`), el Sistema responde **401 Unauthorized**.
  2. Si un usuario autenticado con rol base (`Capitan` o `Veedor`) intenta crear usuarios administrativos o modificar roles mediante dichos endpoints, el Sistema responde **403 Forbidden** vía `ForbiddenException`.

* **3a. Usuario o Email duplicado (HTTP 409 Conflict):**
  1. Si en el Paso 3 el Sistema detecta que el DNI o el Email ya existen en la base de datos (violando **RN-10**).
  2. La **Capa de Negocio** lanza `BusinessRuleConflictException`.
  3. El Sistema devuelve un código **409 Conflict** con el mensaje de error: `"El correo electrónico o DNI ya se encuentra registrado"`. Fin del caso de uso.

### 5. SUB-VARIACIONES
- Registro público: accesible anónimamente vía `POST /api/usuarios/registro` y `POST /api/auth/register`, asignando siempre rol base `Capitan` o `Espectador`.
- Alta y asignación de roles elevados: disponible únicamente para `Administrador` u `Organizador` autenticados mediante `POST /api/usuarios/admin`, `PUT /api/usuarios/{id}/rol` y `POST /api/veedores`.

### 6. POSTCONDICIONES
- Se crea el perfil del usuario en la base de datos en estado "Activo" con rol verificado.
- El usuario queda habilitado para iniciar sesión en el sistema una vez verificado.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Asignación / modificación exitosa de rol por parte de un Administrador (`PUT /api/usuarios/{id}/rol`). |
| `201` | Created | Confirmación de registro exitoso en registro público o alta administrativa (`POST /api/usuarios/admin`). |
| `400` | Bad Request | Formato JSON corrupto, contraseña insegura, DNI/teléfono inválido, nombre con números o rol inexistente. |
| `401` | Unauthorized | Intento anónimo de consumir endpoints de administración de roles. |
| `403` | Forbidden | Intento de usuario no autorizado (ej. Capitán) de crear usuarios administrativos o modificar roles. |
| `409` | Conflict | Violación de regla RN-10 por DNI o Email ya existente en la base de datos. |

### Matriz de trazabilidad CU-01 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `RegisterUsuarioAsync_WithValidData_ReturnsCreated` | `RegisterUsuario_ReturnsSuccessAndCreated` |
| 2a. Password inseguro | `400 Bad Request` | — (validación esquema DTO) | `RegisterUsuario_WithInvalidPassword_Returns400BadRequest` |
| 2a. DNI con letras o fuera de rango | `400 Bad Request` | `RegisterUsuarioAsync_WhenDniContainsLettersOrInvalidCharacters_ThrowsValidationException` | `RegisterUsuario_WithLettersInDni_Returns400BadRequest`<br>`Vector2_AtaquesAlDni_Retorna400BadRequest` |
| 2a. Teléfono inválido | `400 Bad Request` | `CasoC_UsuarioService_TelefonoInvalido_LanzaValidationException` | `CasoC_Integration_RegistroUsuario_TelefonoInvalido_Returns400BadRequest` |
| 2a. Nombre con números / símbolos | `400 Bad Request` | `CasoD_UsuarioService_NombreConNumeros_ODniFueraDeRango_LanzaValidationException` | `CasoD_Integration_RegistroUsuario_NombreConNumeros_ODniInvalido_Returns400BadRequest` |
| 2a. Rol fuera de rango | `400 Bad Request` | `Vector1_UsuarioService_RolFueraDeRango_LanzaValidationException` | `Vector1_EnumsFueraDeRango_Retorna4xxNunca500` |
| 2b. Inyección rol elevado en registro público | `201 Created` (Rol = `Capitan`) | `RegisterUsuarioAsync_IntentoEscaladaRolElevado_FuerzaRolCapitan` | `CU01_AntiEscaladaPrivilegios_RegistroPublico_ConRolAdministradorUOrganizador_FuerzaRolCapitan` |
| 2c. Intento de alta/cambio de rol no autorizado | `401 Unauthorized`<br>`403 Forbidden` | `CreateUsuarioAdminAsync_UsuarioSinRolAdministrativo_ThrowsForbiddenException`<br>`CambiarRolAsync_UsuarioSinRolAdministrativo_ThrowsForbiddenException` | `CU01_BloqueoAsignacionRolElevado_CapitanOAnonimo_Retorna401O403` |
| Alta y asignación legítima por Admin | `201 Created`<br>`200 OK` | `CreateUsuarioAdminAsync_Administrador_PermiteAsignarRolElevado`<br>`CambiarRolAsync_Administrador_ModificaRolCorrectamente` | `CU01_AsignacionLegitimaRolElevado_Administrador_Retorna201Y200` |
| 3a. DNI o Email duplicado (RN-10) | `409 Conflict` | `RegisterUsuarioAsync_WhenEmailOrDniExists_ThrowsConflictException`<br>`RegisterUsuarioAsync_WhenDniAlreadyExists_ThrowsConflictException` | `RegisterUsuario_WhenDuplicateData_Returns409Conflict` |
