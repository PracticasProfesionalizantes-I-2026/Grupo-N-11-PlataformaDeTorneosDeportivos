# Caso de Uso: Solicitar generación automática de fixture

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-08 |
| **Nombre** | Solicitar generación automática de fixture |
| **Actor Principal** | Organizador / Administrador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Organizador → obtener un fixture calculado automáticamente |
| **Disparador (Trigger)** | El Organizador hace clic en "Cerrar Inscripciones y Generar Fixture". |
| **Prioridad / Frecuencia** | Alta; al cierre de inscripciones de cada torneo |
| **Reglas de negocio relacionadas** | Cupo mínimo reglamentario para el torneo |

---

### 1. BREVE DESCRIPCIÓN
Interfaz e instrucción donde el Organizador valida el cierre de la fase de inscripción e inicia la orden para que el sistema procese el calendario de partidos según los cupos.

### 2. PRECONDICIONES
- El torneo debe tener inscripciones abiertas.
- La cantidad de equipos inscritos debe alcanzar el mínimo permitido.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El Organizador selecciona el torneo desde su panel.
2. El Sistema muestra la cantidad de equipos confirmados actuales.
3. El Organizador define reglas de fecha de inicio, horarios y disponibilidad de canchas/servidores.
4. El Organizador confirma la acción presionando "Iniciar Generación".
5. El Sistema invoca al caso de uso CU-16: Generar el fixture automático.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. No se alcanzó el cupo mínimo (HTTP 409 Conflict):**
  1. Si en el Paso 2 el Sistema detecta que la cantidad de equipos confirmados es menor al mínimo reglamentario para crear el torneo.
  2. El Sistema cancela el pedido y muestra el error: "No se puede generar el fixture. No se alcanza el mínimo de equipos requeridos". Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- No especificado.

### 6. POSTCONDICIONES
- Se cierra la inscripción del torneo y se dispara el proceso de cálculo de partidos.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Instrucción recibida y proceso de fixture iniciado. |
| `409` | Conflict | Cupo mínimo no alcanzado. |

### Matriz de trazabilidad CU-08 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `InitiateFixtureGenerationAsync_ValidatesAndCallsGenerator` | `GenerateFixtureRequest_Returns200OK` |
| 2a. Cupo no alcanzado | `409 Conflict` | `InitiateFixtureGenerationAsync_BelowMinimum_ThrowsInvalidOperationException` | `GenerateFixtureRequest_BelowMinimum_Returns409Conflict` |
