# Caso de Uso: Iniciar sesión

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-02 |
| **Nombre** | Iniciar sesión |
| **Actor Principal** | Usuario Registrado (Organizador, Veedor, Capitán, etc.) |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario → autenticarse exitosamente y obtener acceso al sistema; Sistema → validar credenciales y proteger la información |
| **Disparador (Trigger)** | El usuario ingresa credenciales y presiona el botón "Iniciar Sesión" |
| **Prioridad / Frecuencia** | Alta; alta frecuencia |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Permite a un usuario registrado autenticarse en el sistema utilizando su email/DNI y contraseña para acceder a las funcionalidades según su rol asignado.

### 2. PRECONDICIONES
- El usuario debe tener una cuenta registrada e identificada en la plataforma.
- El sistema debe estar en funcionamiento y conectado a la base de datos.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor envía una petición al endpoint `POST /api/auth/login` con sus credenciales (Email/DNI y Contraseña).
2. La **Capa de Presentación** valida que el formato de los datos (esquema) sea correcto.
3. La **Capa de Negocio** valida que las credenciales sean correctas comprobando el hash de la contraseña contra la Base de Datos.
4. El Sistema verifica que la cuenta se encuentra activa (no inhabilitada ni pendiente de confirmación).
5. El Sistema devuelve un código **200 OK** junto con el token de sesión (JWT) y redirige lógicamente al panel de control correspondiente a su rol.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Datos de petición incompletos (HTTP 400 Bad Request):**
  1. Si en el Paso 2 el JSON de la petición no incluye Email/DNI o Contraseña.
  2. La **Capa de Presentación** rechaza la petición por error de esquema o validación.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

* **3a. Credenciales Incorrectas (HTTP 401 Unauthorized):**
  1. Si en el Paso 3 el Sistema verifica que la contraseña o el email/DNI no coinciden en los registros.
  2. La **Capa de Negocio** lanza una excepción de autenticación fallida.
  3. El Sistema devuelve un código **401 Unauthorized** con el mensaje de error: "Usuario o contraseña incorrectos". Fin del caso de uso.

* **4a. Cuenta Bloqueada / Inactiva (HTTP 403 Forbidden):**
  1. Si en el Paso 4 el Sistema detecta que la cuenta está inhabilitada por sanciones o falta de activación por correo electrónico.
  2. La **Capa de Negocio** frena la generación del token y lanza excepción de autorización por estado de cuenta.
  3. El Sistema devuelve un código **403 Forbidden** notificando: "Cuenta no verificada o inhabilitada. Verifique su correo o contacte al administrador". Fin del caso de uso.

### 5. SUB-VARIACIONES
- El usuario puede iniciar sesión mediante DNI en lugar de email, y el sistema gestionará la autenticación de igual modo.

### 6. POSTCONDICIONES
- El usuario obtiene una sesión activa con los tokens (JWT) de permisos de su rol correspondiente, listos para interactuar con la plataforma.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Autenticación exitosa y generación de Token JWT. |
| `400` | Bad Request | Datos de autenticación incompletos o malformados. |
| `401` | Unauthorized | Credenciales incorrectas (usuario no existe o password inválido). |
| `403` | Forbidden | Cuenta inactiva, bloqueada o pendiente de activación. |

### Matriz de trazabilidad CU-02 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `LoginAsync_WithValidCredentials_ReturnsToken` | `Login_WithValidCredentials_Returns200OK` |
| 2a. Datos incompletos | `400 Bad Request` | — (validación DTO) | `Login_WithMissingCredentials_Returns400BadRequest` |
| 3a. Credenciales incorrectas | `401 Unauthorized` | `LoginAsync_WithInvalidCredentials_ThrowsUnauthorizedException` | `Login_WithInvalidCredentials_Returns401Unauthorized` |
| 4a. Cuenta bloqueada | `403 Forbidden` | `LoginAsync_WhenAccountIsInactive_ThrowsForbiddenException` | `Login_WhenAccountIsInactive_Returns403Forbidden` |
