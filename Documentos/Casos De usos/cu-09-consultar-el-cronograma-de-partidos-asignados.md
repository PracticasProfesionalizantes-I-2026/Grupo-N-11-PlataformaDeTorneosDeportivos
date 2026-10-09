# Caso de Uso: Consultar el cronograma de partidos asignados

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-09 |
| **Nombre** | Consultar el cronograma de partidos asignados |
| **Actor Principal** | Veedor / Árbitro |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Veedor → conocer qué partidos debe controlar, horarios y lugares; Organización → asegurar que el personal asignado tenga visibilidad de sus tareas |
| **Disparador (Trigger)** | El Veedor ingresa a la sección "Mis Partidos Asignados" |
| **Prioridad / Frecuencia** | Alta; alta frecuencia |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Permite al Veedor/Árbitro visualizar desde su dispositivo móvil o web la lista de partidos donde ha sido asignado para controlar mesa y cargar incidencias.

### 2. PRECONDICIONES
- El Veedor debe estar autenticado en el sistema con un rol activo.
- Los torneos y partidos deben encontrarse ya generados y asignados en la base de datos.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor envía una petición al endpoint `GET /api/veedores/{id}/partidos` (opcionalmente con filtros de fecha o torneo).
2. La **Capa de Presentación** procesa los parámetros de búsqueda.
3. La **Capa de Negocio** valida los permisos de consulta del Veedor.
4. La **Capa de Persistencia** recupera la lista cronológica de partidos donde la cuenta del Veedor figura asignada.
5. El Sistema devuelve un código **200 OK** con la colección de partidos (detalle de Horario, Cancha/Servidor, Equipos y Estado del Partido).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **4a. Sin Partidos Asignados (HTTP 200 OK):**
  1. Si en el Paso 4 la **Capa de Persistencia** no encuentra registros que coincidan con la fecha o el Veedor.
  2. La **Capa de Negocio** retorna una colección vacía.
  3. El Sistema devuelve un código **200 OK** con una lista vacía, y la UI mostrará el mensaje: "No posee partidos asignados para la fecha seleccionada". Fin del caso de uso.

* **3a. Acceso no autorizado (HTTP 403 Forbidden):**
  1. Si en el Paso 3 el Veedor intenta consultar los partidos de otro Veedor sin tener permisos administrativos.
  2. La **Capa de Negocio** bloquea el acceso.
  3. El Sistema devuelve un código **403 Forbidden**. Fin del caso de uso.

### 5. SUB-VARIACIONES
- El usuario puede filtrar la vista de partidos enviando query params en la solicitud (ej. `?fecha=2026-10-10` o `?torneoId=5`).

### 6. POSTCONDICIONES
- Ninguna alteración de estado. El Veedor accede a la información de solo lectura relevante para su labor.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Retorno exitoso de la lista de partidos asignados (incluso si está vacía). |
| `403` | Forbidden | Intento de un Veedor de acceder a las asignaciones de otro usuario sin permisos. |

### Matriz de trazabilidad CU-09 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetPartidosByVeedorAsync_ReturnsListaPartidos` | `GetPartidosVeedor_Returns200OKAndList` |
| 4a. Sin partidos | `200 OK` | `GetPartidosByVeedorAsync_WhenEmpty_ReturnsEmptyList` | `GetPartidosVeedor_WhenNoMatches_Returns200OKAndEmpty` |
| 3a. Acceso denegado | `403 Forbidden` | `GetPartidosByVeedorAsync_WithDifferentUserId_ThrowsForbidden` | `GetPartidosVeedor_WithDifferentUserToken_Returns403Forbidden` |
