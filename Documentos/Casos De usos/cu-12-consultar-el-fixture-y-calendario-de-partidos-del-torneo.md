# Caso de Uso: Consultar el fixture y calendario de partidos del torneo

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-12 |
| **Nombre** | Consultar el fixture y calendario de partidos del torneo |
| **Actor Principal** | Capitán / Jugador / Espectador (Usuarios Públicos) |
| **Alcance / Nivel** | Sistema; subfunción / meta de usuario |
| **Stakeholders e intereses** | Usuarios Públicos → conocer cuándo, dónde y contra quién juegan sus equipos; Sistema → proveer acceso a la información pública del torneo de forma ágil |
| **Disparador (Trigger)** | El usuario ingresa a la sección "Fixture / Calendario" dentro de un torneo |
| **Prioridad / Frecuencia** | Alta; muy alta frecuencia |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Permite a cualquier usuario público (Capitanes, Jugadores, Espectadores) consultar las fechas, enfrentamientos y horarios planificados de un torneo en particular.

### 2. PRECONDICIONES
- El sistema debe tener acceso público o el usuario debe poder acceder a la vista del torneo.
- El torneo seleccionado debe existir en la base de datos.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor envía una petición al endpoint `GET /api/torneos/{id}/fixture` para obtener el calendario.
2. La **Capa de Presentación** recibe la solicitud y pasa el identificador a la lógica.
3. La **Capa de Negocio** verifica que el torneo exista en la base de datos.
4. La **Capa de Persistencia** recupera todos los partidos programados agrupados por Jornada/Fecha o por Fase de Eliminación.
5. El Sistema devuelve un código **200 OK** con los resultados pasados, horarios futuros, lugares y estado actual de los encuentros.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **4a. Fixture aún no generado (HTTP 200 OK):**
  1. Si en el Paso 4 el Sistema detecta que el torneo existe pero no posee partidos generados (estado de inscripción abierta).
  2. La **Capa de Negocio** retorna una colección vacía.
  3. El Sistema devuelve un código **200 OK** junto con una colección vacía, y la UI mostrará la leyenda: "El fixture de este torneo aún no ha sido publicado por la organización". Fin del caso de uso.

* **3a. Torneo Inexistente (HTTP 404 Not Found):**
  1. Si en el Paso 3 el ID del torneo no existe en los registros.
  2. La **Capa de Negocio** no encuentra la entidad correspondiente.
  3. El Sistema devuelve un error **404 Not Found**. Fin del caso de uso.

### 5. SUB-VARIACIONES
- El usuario puede filtrar el fixture obtenido para buscar por nombre de equipo específico o por fecha desde la interfaz de cliente.

### 6. POSTCONDICIONES
- Ninguna. Es una operación de solo lectura.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Retorno exitoso de la lista de partidos (incluso si está vacía). |
| `404` | Not Found | Inexistencia del torneo consultado. |

### Matriz de trazabilidad CU-12 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetFixtureAsync_ReturnsListaPartidos` | `GetFixture_Returns200OK` |
| 4a. Sin fixture generado | `200 OK` | `GetFixtureAsync_WhenNoMatches_ReturnsEmptyList` | `GetFixture_WhenNotGenerated_Returns200OKAndEmpty` |
| 3a. Torneo inexistente | `404 Not Found` | `GetFixtureAsync_WhenTorneoNotFound_ThrowsNotFoundException` | `GetFixture_WhenTorneoDoesNotExist_Returns404NotFound` |
