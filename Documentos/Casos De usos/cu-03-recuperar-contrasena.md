# Caso de Uso: Recuperar contraseña

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-03 |
| **Nombre** | Recuperar contraseña ("Olvidé mi contraseña") |
| **Actor Principal** | Usuario Registrado |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario → recuperar acceso a su cuenta; Sistema → garantizar un mecanismo seguro de reseteo |
| **Disparador (Trigger)** | El usuario selecciona la opción "¿Olvidaste tu contraseña?" e ingresa su correo electrónico |
| **Prioridad / Frecuencia** | Alta; media frecuencia |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Permite a un usuario restablecer su contraseña mediante un enlace seguro enviado a su casilla de correo electrónico.

### 2. PRECONDICIONES
- El usuario debe estar registrado en el sistema previamente.
- El servicio de envío de correos electrónicos debe estar operativo.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor envía una petición al endpoint `POST /api/auth/recuperar-password` con su correo electrónico registrado.
2. La **Capa de Presentación** valida que el formato del correo electrónico en el DTO sea correcto.
3. La **Capa de Negocio** verifica la existencia del email en la Base de Datos y genera un token único de recuperación con tiempo de expiración.
4. El Sistema envía un correo electrónico al usuario con un enlace seguro que contiene el token de recuperación.
5. El Sistema devuelve un código **200 OK** confirmando el envío del enlace de recuperación al correo provisto.
6. El usuario hace clic en el enlace, ingresa su nueva contraseña y envía la petición a `POST /api/auth/reset-password` con el token.
7. La **Capa de Negocio** valida el token, actualiza la contraseña en la Base de Datos, y devuelve **200 OK** confirmando la operación.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Correo no registrado (HTTP 200 OK):**
  1. Si en el Paso 3 el Sistema no encuentra el correo en la base de datos de usuarios.
  2. La **Capa de Negocio** finaliza el proceso de forma silenciosa para evitar revelar información sobre la existencia de usuarios por motivos de seguridad.
  3. El Sistema devuelve un código **200 OK** informando el mensaje estándar: "Si el correo existe, se enviará un enlace de recuperación". Fin del caso de uso.

* **6a. Formato de Nueva Contraseña Inseguro (HTTP 400 Bad Request):**
  1. Si en el Paso 6 la nueva contraseña ingresada no cumple con las reglas mínimas de complejidad del esquema de seguridad.
  2. La **Capa de Presentación** rechaza la petición por error de validación.
  3. El Sistema devuelve un código **400 Bad Request** indicando los requisitos de la contraseña. Fin del caso de uso.

* **7a. Token Expirado o Usado (HTTP 400 Bad Request):**
  1. Si en el Paso 7 el usuario utiliza un enlace de recuperación cuya vigencia expiró (ej. mayor a 24 hs) o cuyo token ya fue redimido previamente.
  2. La **Capa de Negocio** detecta la invalidez del token de recuperación y lanza una excepción de validación.
  3. El Sistema devuelve un error **400 Bad Request** notificando: "El enlace de recuperación ha expirado, intenta solicitar uno nuevo". Fin del caso de uso.

### 5. SUB-VARIACIONES
- No presenta variaciones significativas.

### 6. POSTCONDICIONES
- Se actualiza el hash de la contraseña del usuario en la base de datos.
- El token de recuperación utilizado se marca como inválido para impedir su reutilización.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Confirmación genérica de procesamiento o éxito del restablecimiento. |
| `400` | Bad Request | Token de recuperación inválido o caducado, o formato de nueva contraseña incorrecto. |

### Matriz de trazabilidad CU-03 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `RecoverPasswordAsync_WithValidEmail_GeneratesToken` / `ResetPasswordAsync_UpdatesPassword` | `RecoverPassword_And_ResetPassword_Returns200OK` |
| 3a. Correo no registrado | `200 OK` | `RecoverPasswordAsync_WithNonExistentEmail_DoesNothing` | `RecoverPassword_WithNonExistentEmail_Returns200OK` |
| 6a. Contraseña inválida | `400 Bad Request` | — (validación DTO) | `ResetPassword_WithInvalidPasswordFormat_Returns400BadRequest` |
| 7a. Token Expirado/Usado | `400 Bad Request` | `ResetPasswordAsync_WithInvalidToken_ThrowsValidationException` | `ResetPassword_WithInvalidToken_Returns400BadRequest` |
