# Caso de Uso: Cargar resultados básicos del partido

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-10 |
| **Nombre** | Cargar resultados básicos del partido (Goles o puntos finales) |
| **Actor Principal** | Veedor / Árbitro / Organizador |
| **Alcance / Nivel** | Sistema; meta de usuario |
| **Stakeholders e intereses** | Veedor → registrar resultado; Organizador → mantener la competencia actualizada; Sistema → automatizar la tabla o llave |
| **Disparador (Trigger)** | El usuario selecciona la opción "Cargar Resultado Final" en un partido del cronograma. |
| **Prioridad / Frecuencia** | Alta; ocurre al finalizar cada partido |
| **Reglas de negocio relacionadas** | Sin empates en Eliminación Directa |

---

### 1. BREVE DESCRIPCIÓN
Permite al Veedor o Administrador cargar únicamente el marcador final de un partido de forma rápida para actualizar tablas o llaves.

### 2. PRECONDICIONES
- El partido debe estar en estado "Pendiente" o "En Juego".

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El Veedor/Organizador abre la tarjeta del partido.
2. El Sistema muestra los campos numéricos para el marcador del Equipo A y Equipo B.
3. El usuario ingresa los tantos/goles y presiona "Guardar Resultado".
4. El Sistema valida que los marcadores sean valores numéricos no negativos.
5. El Sistema cambia el estado del partido a "Finalizado", actualiza en tiempo real la tabla de posiciones o el cruce de eliminatoria e impacta en la cartelera pública.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Empate no permitido en Eliminación Directa (Brackets) (HTTP 400 Bad Request):**
  1. Si en el Paso 3 el partido pertenece a una llave de eliminación directa y se carga un marcador igualado.
  2. El Sistema muestra el mensaje: "Esta modalidad no admite empates. Debe definir el ganador (Penales/Tiempo Extra)".
  3. El Sistema despliega los campos adicionales para definir la definición por penales/desempate. Fin del caso de uso alternativo (continúa con el registro del desempate).

* **4a. Valores inválidos (HTTP 400 Bad Request):**
  1. Si en el Paso 4 el marcador tiene formato inválido o números negativos.
  2. El Sistema rechaza la petición. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- La carga puede implicar goles (fútbol), puntos (básquet), rondas (e-sports), etc.

### 6. POSTCONDICIONES
- El marcador queda registrado, el partido finalizado y se actualizan los standings (tabla/bracket).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Resultado guardado y estado actualizado correctamente. |
| `400` | Bad Request | Marcador inválido o empate en modalidad no permitida. |

### Matriz de trazabilidad CU-10 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `SaveMatchResultAsync_UpdatesMatchAndStandings` | `SaveMatchResult_Returns200OK` |
| 3a. Empate no permitido | `400 Bad Request` | `SaveMatchResultAsync_WhenDrawInBrackets_ThrowsValidationException` | `SaveMatchResult_WhenDrawInBrackets_Returns400BadRequest` |
| 4a. Valores inválidos | `400 Bad Request` | — (validación DTO en Presentación) | `SaveMatchResult_WithNegativeScore_Returns400BadRequest` |
