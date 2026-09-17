# Caso de Uso: Consultar el fixture y calendario de partidos del torneo

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-12 |
| **Nombre** | Consultar el fixture y calendario de partidos del torneo |
| **Actor Principal** | Capitán / Jugador / Espectador (Usuarios Públicos) |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Espectador/Jugador → informarse sobre días, horarios y rivales de la competencia |
| **Disparador (Trigger)** | El usuario ingresa a la sección "Fixture / Calendario" dentro de un torneo. |
| **Prioridad / Frecuencia** | Alta; consulta frecuente del público general |
| **Reglas de negocio relacionadas** | Ninguna (consulta de datos públicos) |

---

### 1. BREVE DESCRIPCIÓN
Permite a cualquier usuario público (Capitanes, Jugadores, Espectadores) consultar las fechas, enfrentamientos y horarios planificados.

### 2. PRECONDICIONES
- El torneo debe tener el fixture generado e introducido públicamente.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El usuario selecciona un torneo del listado general.
2. El usuario hace clic en la pestaña "Fixture".
3. El Sistema presenta los partidos agrupados por Jornada/Fecha o por Fase de Eliminación.
4. El usuario puede filtrar por nombre de equipo para ver su calendario específico.
5. El Sistema muestra los resultados pasados, horarios futuros, lugares y estado del encuentro.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Fixture aún no generado (HTTP 200 OK - Simulado o HTTP 404):**
  1. Si en el Paso 2 el torneo aún no cuenta con partidos programados.
  2. El Sistema muestra una leyenda: "El fixture de este torneo aún no ha sido publicado por la organización". Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- La visualización varía si el torneo es una liga (por fechas) o eliminación directa (por fases/rondas).

### 6. POSTCONDICIONES
- Operación de solo lectura. Ningún estado del sistema es alterado.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Fixture recuperado exitosamente para la vista (incluso vacío). |

### Matriz de trazabilidad CU-12 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetTournamentFixtureAsync_ReturnsMatchesScheduled` | `GetTournamentFixture_Returns200OK` |
| 2a. Fixture no generado | `200 OK` | `GetTournamentFixtureAsync_WhenNotPublished_ReturnsEmptyInfo` | `GetTournamentFixture_WhenNotPublished_Returns200OK` |
