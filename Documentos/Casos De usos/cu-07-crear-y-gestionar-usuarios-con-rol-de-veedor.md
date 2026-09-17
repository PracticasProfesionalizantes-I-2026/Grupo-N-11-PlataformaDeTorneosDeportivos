# Caso de Uso: Crear y gestionar usuarios con rol de Veedor

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-07 |
| **Nombre** | Crear y gestionar usuarios con rol de "Veedor" |
| **Actor Principal** | Organizador / Administrador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Organizador → delegar carga de partidos; Veedor → recibir acceso al sistema para cargar resultados |
| **Disparador (Trigger)** | El Organizador ingresa al apartado "Gestión de Personal/Veedores" dentro de su panel administrativo. |
| **Prioridad / Frecuencia** | Media |
| **Reglas de negocio relacionadas** | Ninguna (gestión de roles y permisos) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Organizador dar de alta, modificar permisos o dar de baja cuentas para veedores o árbitros que tendrán acceso a la planilla digital de carga de partidos.

### 2. PRECONDICIONES
- El Organizador debe estar autenticado.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El Organizador selecciona "Nuevo Veedor".
2. El Sistema solicita Nombre, Apellido, DNI, Email y Torneos/Sedes asignadas.
3. El Organizador completa la información y presiona "Guardar y Asignar".
4. El Sistema registra al usuario asignándole el rol "Veedor".
5. El Sistema envía un correo automático al nuevo veedor con sus credenciales de acceso.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. Modificación o Desvinculación de Veedor:**
  1. Si en el Paso 1 el Organizador selecciona un veedor existente.
  2. El Organizador puede cambiar los torneos asignados o seleccionar "Desactivar Cuenta".
  3. El Sistema actualiza el estado y bloquea los accesos inmediatos del veedor a la carga de datos del torneo.

### 5. SUB-VARIACIONES (opcional)
- La gestión incluye tanto alta como modificación y baja lógica.

### 6. POSTCONDICIONES
- Queda habilitado/deshabilitado el perfil de Veedor para la carga de partidos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `201` | Created | Veedor creado exitosamente. |
| `200` | OK | Actualización exitosa del Veedor. |
| `400` | Bad Request | Datos inválidos. |
| `404` | Not Found | Veedor inexistente en modificación. |

### Matriz de trazabilidad CU-07 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `201 Created` | `CreateVeedorAsync_SavesAndReturnsCreatedVeedor` | `CreateVeedor_ReturnsSuccessAnd201Created` |
| 1a. Modificar/Desvincular | `200 OK` | `UpdateVeedorStatusAsync_UpdatesRolesAndAccess` | `UpdateVeedor_Returns200OK` |
