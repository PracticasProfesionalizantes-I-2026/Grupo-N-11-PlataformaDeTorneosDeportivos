# Caso de Uso: Consultar el cronograma de partidos asignados

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-09 |
| **Nombre** | Consultar el cronograma de partidos asignados |
| **Actor Principal** | Veedor / Árbitro |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Veedor → saber qué partidos debe supervisar; Organizador → asegurar que los veedores conozcan sus asignaciones |
| **Disparador (Trigger)** | El Veedor ingresa a la sección "Mis Partidos Asignados". |
| **Prioridad / Frecuencia** | Alta; revisión frecuente antes de las jornadas |
| **Reglas de negocio relacionadas** | Ninguna (consulta de datos) |

---

### 1. BREVE DESCRIPCIÓN
Permite al Veedor/Árbitro visualizar desde su dispositivo móvil o web la lista de partidos donde ha sido asignado para controlar mesa y cargar incidencias.

### 2. PRECONDICIONES
- El Veedor debe estar autenticado con rol activo.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El Veedor accede a su panel.
2. El Sistema recupera y despliega la lista cronológica de partidos donde figura asignado.
3. El Veedor selecciona una fecha o filtro de torneo.
4. El Sistema muestra los partidos con detalle de: Horario, Cancha/Servidor, Equipos y Estado del Partido (Pendiente, En Progreso, Finalizado).

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Sin Partidos Asignados (HTTP 200 OK - Lista vacía):**
  1. Si en el Paso 2 el Sistema no encuentra asignaciones vigentes para el Veedor.
  2. El Sistema muestra el mensaje: "No posee partidos asignados para la fecha seleccionada". Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- Acceso a la vista desde dispositivo móvil (app/web responsiva) o computadora de escritorio.

### 6. POSTCONDICIONES
- El Veedor accede a la información relevante para iniciar la planilla del partido.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Partidos recuperados exitosamente (incluso si la lista está vacía). |

### Matriz de trazabilidad CU-09 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetAssignedMatchesAsync_ReturnsMatchList` | `GetAssignedMatches_Returns200OK` |
| 2a. Sin partidos asignados | `200 OK` | `GetAssignedMatchesAsync_WhenEmpty_ReturnsEmptyList` | `GetAssignedMatches_WhenEmpty_Returns200OK` |
