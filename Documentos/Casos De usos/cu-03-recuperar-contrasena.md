# Caso de Uso: Recuperar contraseña

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-03 |
| **Nombre** | Recuperar contraseña ("Olvidé mi contraseña") |
| **Actor Principal** | Usuario Registrado |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario → recuperar el acceso a su cuenta; Sistema → garantizar un restablecimiento seguro mediante verificación |
| **Disparador (Trigger)** | El usuario selecciona la opción "¿Olvidaste tu contraseña?". |
| **Prioridad / Frecuencia** | Media |
| **Reglas de negocio relacionadas** | Ninguna (gestión de seguridad y tokens) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un usuario restablecer su contraseña mediante un enlace seguro enviado a su casilla de correo electrónico.

### 2. PRECONDICIONES
- Estar registrado en el sistema previamente.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El usuario ingresa su correo electrónico registrado en la pantalla de recuperación.
2. El usuario presiona "Enviar enlace de recuperación".
3. El Sistema verifica la existencia del email y genera un token único de recuperación con tiempo de expiración.
4. El Sistema envía un correo electrónico al usuario con un enlace seguro.
5. El usuario hace clic en el enlace, es dirigido a una pantalla donde ingresa la nueva contraseña y presiona "Cambiar contraseña".
6. El Sistema actualiza la contraseña y confirma la operación exitosa.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Correo no registrado (HTTP 200 OK - Simulado):**
  1. Si en el Paso 3 el Sistema no encuentra el correo en la base de datos.
  2. Por motivos de seguridad, el sistema informa que "Si el correo existe se enviará un enlace", o muestra "Correo no encontrado". Fin del caso de uso.

* **5a. Token Expirado o Usado (HTTP 400 Bad Request):**
  1. Si en el Paso 5 el usuario hace clic en un enlace de recuperación cuya vigencia expiró (ej. mayor a 24 hs).
  2. El Sistema notifica: "El enlace de recuperación ha expirado, intenta solicitar uno nuevo". Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- No especificado.

### 6. POSTCONDICIONES
- Se actualiza la contraseña del usuario en la base de datos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Solicitud procesada y contraseña actualizada correctamente. |
| `400` | Bad Request | Token inválido, expirado o malformado. |

### Matriz de trazabilidad CU-03 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `ResetPasswordAsync_UpdatesPasswordAndInvalidatesToken` | `ResetPassword_WithValidToken_Returns200OK` |
| 3a. Correo no registrado | `200 OK` | `GenerateResetTokenAsync_WhenEmailNotFound_CompletesSilently` | `RequestPasswordReset_WithUnknownEmail_Returns200OK` |
| 5a. Token Expirado | `400 Bad Request` | `ResetPasswordAsync_WithExpiredToken_ThrowsValidationException` | `ResetPassword_WithExpiredToken_Returns400BadRequest` |
