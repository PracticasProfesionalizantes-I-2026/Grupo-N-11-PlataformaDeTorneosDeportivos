# Caso de Uso: Visualizar espacios publicitarios y logos de patrocinadores

> Especificación elaborada siguiendo la guía `GUIA-Especificacion-Casos-de-Uso.md` (sección 3).

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-15 |
| **Nombre** | Visualizar espacios publicitarios y logos de patrocinadores |
| **Actor Principal** | Espectador / Usuario Público |
| **Alcance / Nivel** | Sistema; subfunción |
| **Stakeholders e intereses** | Patrocinadores → visibilidad de su marca; Organizador → retorno de inversión / monetización |
| **Disparador (Trigger)** | La carga o renderizado de cualquier pantalla pública del torneo (Fixture, Tablas, Perfiles). |
| **Prioridad / Frecuencia** | Media; constante durante la navegación |
| **Reglas de negocio relacionadas** | Ninguna (renderizado condicionado a la configuración) |

---

### 1. BREVE DESCRIPCIÓN
Módulo pasivo del sistema que renderiza banners, logos e imágen de marcas patrocinadoras en la vista pública para generar retorno de inversión a los sponsors.

### 2. PRECONDICIONES
- El Organizador debe haber configurado los banners y patrocinadores en la administración del torneo.

### 3. FLUJO PRINCIPAL (Camino Feliz)
1. El usuario navega por las secciones públicas del torneo.
2. El Sistema carga en los marcos superior, lateral o inferior los banners/logos paramétricos asociados a dicho torneo.
3. El usuario puede hacer clic sobre una publicidad.
4. El Sistema contabiliza la impresión/clic y redirige al usuario a la URL externa configurada por el patrocinador en una nueva pestaña.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **2a. Sin patrocinadores configurados (Flujo transparente):**
  1. Si en el Paso 2 el torneo no cuenta con patrocinadores activos configurados.
  2. El Sistema omite la renderización de los marcos publicitarios, adaptando el layout para aprovechar el espacio. Fin del caso de uso.

### 5. SUB-VARIACIONES (opcional)
- La disposición y tamaño de los banners puede diferir según el tamaño de pantalla del dispositivo del usuario (responsive design).

### 6. POSTCONDICIONES
- Se registra la métrica de visualización/clic para el panel de métricas del organizador.

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Banners cargados exitosamente. |
| `201` | Created | Registro de métrica de clic/impresión guardada. |

### Matriz de trazabilidad CU-15 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal | `200 OK` | `GetTournamentSponsorsAsync_ReturnsActiveBanners` | `GetTournamentSponsors_Returns200OK` |
| Paso 4 (Clic) | `201 Created` | `RecordAdClickMetricAsync_SavesClickEvent` | `RecordAdClick_Returns201Created` |
| 2a. Sin patrocinadores | `200 OK` | `GetTournamentSponsorsAsync_WhenNone_ReturnsEmpty` | `GetTournamentSponsors_WhenNone_Returns200OK` |
