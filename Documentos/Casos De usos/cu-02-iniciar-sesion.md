# Caso de Uso: Iniciar sesión

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-02 |
| **Nombre** | Iniciar sesión |
| **Actor Principal** | Usuario Registrado (Organizador, Veedor, Capitán, etc.) |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario → acceder a sus funciones y panel de control; Sistema → asegurar autenticación y asignación de roles |
| **Disparador (Trigger)** | El usuario presiona el botón "Iniciar Sesión". |
| **Prioridad / Frecuencia** | Alta |
| **Reglas de negocio relacionadas** | Ninguna específica (gestión de acceso) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un usuario registrado autenticarse en el sistema utilizando su email/DNI y contraseña para acceder a las funcionalidades según su rol asignado.

### 2. PRECONDICIONES
- El usuario debe tener una cuenta registrada e identificada en la plataforma.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El usuario ingresa a la pantalla de Login.
2. El Sistema solicita Email/DNI y Contraseña.
3. El usuario completa los campos y presiona "Ingresar".
4. El Sistema valida que las credenciales sean correctas.
5. El Sistema inicia la sesión y redirige al usuario a su panel de control personalizado según su rol (Organizador, Veedor, Capitán).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **4a. Credenciales Incorrectas (HTTP 401 Unauthorized):**
  1. Si en el Paso 4 el Sistema verifica que la contraseña o el email no coinciden.
  2. El Sistema muestra el mensaje de error: "Usuario o contraseña incorrectos".
  3. El Sistema mantiene al usuario en la pantalla de Login. Fin del caso de uso.

* **4b. Cuenta Bloqueada / Inactiva (HTTP 403 Forbidden):**
  1. Si en el Paso 4 el Sistema detecta que la cuenta está inhabilitada por sanciones o falta de activación por correo.
  2. El Sistema notifica: "Cuenta no verificada o inhabilitada. Verifique su correo o contacte al administrador". Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- Redirección pos-login: Dependiendo del rol, el panel de control mostrado será distinto (panel de Organizador, panel de Capitán, etc.).

### 6. POSTCONDICIONES
- El usuario obtiene una sesión activa con los token de permisos de su rol correspondiente.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Autenticación exitosa, retorno de token JWT. |
| `400` | Bad Request | Formato de petición inválido. |
| `401` | Unauthorized | Credenciales incorrectas. |
| `403` | Forbidden | Cuenta bloqueada o inactiva. |

### Matriz de trazabilidad CU-02 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `AuthenticateUserAsync_ReturnsValidJwtToken` | `Login_WithValidCredentials_Returns200OK` |
| 4a. Credenciales incorrectas | `401 Unauthorized` | `AuthenticateUserAsync_WithInvalidCredentials_ThrowsUnauthorizedException` | `Login_WithInvalidCredentials_Returns401Unauthorized` |
| 4b. Cuenta Bloqueada | `403 Forbidden` | `AuthenticateUserAsync_WhenAccountIsInactive_ThrowsForbiddenException` | `Login_WithInactiveAccount_Returns403Forbidden` |
