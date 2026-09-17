# Caso de Uso: Registrarse en la plataforma

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-01 |
| **Nombre** | Registrarse en la plataforma (Crear cuenta / "No tengo cuenta") |
| **Actor Principal** | Usuario No Autenticado |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Usuario → obtener una cuenta para acceder a la plataforma; Sistema → registrar al usuario verificando unicidad de datos |
| **Disparador (Trigger)** | El usuario hace clic en el botón "Registrarse" o "No tengo cuenta" desde la pantalla de bienvenida/login. |
| **Prioridad / Frecuencia** | Alta |
| **Reglas de negocio relacionadas** | RN-10 (El DNI y el Email son de carácter único en toda la base de datos) |

---

### 1. BREVE DESCRIPCIÓN
Permite a un nuevo usuario (sea Organizador, Capitán, Veedor o Espectador) crear un perfil en la plataforma completando los datos obligatorios.

### 2. PRECONDICIONES
- El usuario no debe haber iniciado sesión en el sistema.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El usuario accede al formulario de registro.
2. El Sistema solicita el Nombre, Apellido, DNI/ID, Correo Electrónico, Teléfono y Contraseña.
3. El usuario completa los campos obligatorios y presiona "Crear Cuenta".
4. El Sistema valida la integridad de los datos, confirma que el email/DNI no estén duplicados y registra la cuenta.
5. El Sistema envía un correo de activación/confirmación y redirige al usuario a la pantalla de inicio de sesión indicando que revise su correo.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **4a. Usuario o Email duplicado (HTTP 409 Conflict):**
  1. Si en el Paso 4 el Sistema detecta que el DNI o el Email ya existen en la base de datos (**RN-10**).
  2. El Sistema muestra el mensaje de error: "El correo o DNI ya se encuentra registrado en la plataforma".
  3. El Sistema mantiene los datos en el formulario y solicita corregir el campo en conflicto. Fin del caso de uso.

* **4b. Formato de Contraseña Inseguro (HTTP 400 Bad Request):**
  1. Si en el Paso 4 la contraseña ingresada no cumple con las reglas mínimas de complejidad (ej. 8 caracteres, números y letras).
  2. El Sistema muestra la alerta correspondiente y permanece en el formulario. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- El registro puede ser realizado con miras a adquirir diferentes roles: Organizador, Capitán, Veedor o Espectador.

### 6. POSTCONDICIONES
- Se crea el perfil del usuario en estado "Pendiente de Confirmación" o "Activo".

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Confirmación de creación exitosa de la cuenta de usuario. |
| `400` | Bad Request | Formato de contraseña inseguro o datos incompletos. |
| `409` | Conflict | DNI o Email ya registrados en el sistema (RN-10). |
| `500` | Internal Server Error | Error no controlado durante el proceso de registro. |

### Matriz de trazabilidad CU-01 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `RegisterUserAsync_SavesAndReturnsCreatedUser` | `RegisterUser_ReturnsSuccessAndCreatedUser` |
| 4a. Usuario/Email duplicado | `409 Conflict` | `RegisterUserAsync_WhenDniOrEmailExists_ThrowsDuplicateUserException` | `RegisterUser_WhenDuplicateData_Returns409Conflict` |
| 4b. Contraseña Insegura | `400 Bad Request` | `RegisterUserAsync_WithWeakPassword_ThrowsValidationException` | `RegisterUser_WithWeakPassword_Returns400BadRequest` |
