# Caso de Uso: Consultar las tablas de posiciones y llaves de competencia

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-13 |
| **Nombre** | Consultar las tablas de posiciones y llaves de competencia (Brackets) en tiempo real |
| **Actor Principal** | Capitán / Jugador / Espectador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Espectador/Jugador → informarse sobre la clasificación de los equipos |
| **Disparador (Trigger)** | El usuario selecciona la opción "Tabla de Posiciones" o "Llaves / Brackets". |
| **Prioridad / Frecuencia** | Alta |
| **Reglas de negocio relacionadas** | Criterios de desempate de la Liga (ej. diferencia de gol) |

---

### 1. BREVE DESCRIPCIÓN
Permite a los usuarios consultar las tablas (puntos, PG, PE, PP, GF, GC, Dif) o el diagrama de llaves (eliminatoria) actualizadas automáticamente tras cada resultado.

### 2. PRECONDICIONES
- El torneo debe estar en estado "En Desarrollo" o "Finalizado".

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El usuario navega al torneo deseado.
2. El Sistema verifica la modalidad (Liga o Eliminación Directa):
   - Si es Liga: Muestra la tabla de posiciones con los criterios de desempate computados.
   - Si es Brackets: Muestra el diagrama dinámico con los equipos avanzando ronda tras ronda.
3. La información se renderiza en tiempo real de forma responsiva.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **1a. Torneo sin partidos disputados (HTTP 200 OK):**
  1. Si en el Paso 1 el torneo recién inicia y no hay resultados, el Sistema muestra la tabla en ceros o el bracket inicial.

### 5. SUB-VARIACIONES (opcional)
- Renderización diferenciada para dispositivos móviles vs escritorio (diagrama dinámico o tabla compacta).

### 6. POSTCONDICIONES
- Muestra visual precisa e inmediata del estado de la competencia. No altera datos persistentes.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Tabla de posiciones o bracket generado y retornado con éxito. |

### Matriz de trazabilidad CU-13 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetTournamentStandingsAsync_ReturnsComputedTableOrBrackets` | `GetTournamentStandings_Returns200OK` |
