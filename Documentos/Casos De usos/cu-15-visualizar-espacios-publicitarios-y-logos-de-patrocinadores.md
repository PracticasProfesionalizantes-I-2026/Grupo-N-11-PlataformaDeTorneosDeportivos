# Caso de Uso: Visualizar espacios publicitarios y logos de patrocinadores

| Campo | Valor |
| --- | --- |
| **ID del Caso de Uso** | CU-15 |
| **Nombre** | Visualizar espacios publicitarios y logos de patrocinadores |
| **Actor Principal** | Espectador / Usuario Público |
| **Alcance / Nivel** | Sistema; subfunción pasiva |
| **Stakeholders e intereses** | Patrocinadores (Sponsors) → obtener retorno de inversión y visibilidad de sus marcas; Organización → monetizar la plataforma |
| **Disparador (Trigger)** | La carga o renderizado de cualquier pantalla pública del torneo (Fixture, Tablas, Perfiles) |
| **Prioridad / Frecuencia** | Baja; muy alta frecuencia (se repite en cada request) |
| **Reglas de negocio relacionadas** | Ninguna específica |

---

### 1. BREVE DESCRIPCIÓN
Módulo pasivo del sistema que renderiza banners, logos e imágenes de marcas patrocinadoras en la vista pública para generar retorno de inversión a los sponsors, registrando métricas de impresión y clic.

### 2. PRECONDICIONES
- El Organizador debe haber configurado y asignado los banners y patrocinadores en la administración del torneo.
- El servidor de medios estáticos (imágenes) debe estar operativo.

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200)
1. El Actor navega por las secciones públicas del torneo.
2. El cliente (UI) efectúa una petición automática a `GET /api/torneos/{id}/publicidad` en paralelo a la carga principal de datos.
3. La **Capa de Negocio** recupera los recursos publicitarios paramétricos activos asociados a dicho torneo.
4. El Sistema devuelve un código **200 OK** con las URLs de las imágenes y enlaces de destino, registrando asíncronamente una "Impresión" (visualización) en la base de datos.
5. El cliente carga en los marcos superior, lateral o inferior los banners.
6. El usuario hace clic sobre una publicidad.
7. La UI dispara una solicitud a `POST /api/publicidad/{bannerId}/click` para contabilizar la acción.
8. La **Capa de Persistencia** registra la métrica de clic para el panel del organizador y el cliente redirige al usuario a la URL externa configurada por el patrocinador en una nueva pestaña.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Sin publicidad asignada (HTTP 200 OK):**
  1. Si en el Paso 3 el Sistema detecta que el torneo no tiene patrocinadores o banners configurados/activos.
  2. La **Capa de Negocio** retorna una colección vacía.
  3. El Sistema devuelve un código **200 OK** con respuesta vacía y la interfaz se renderiza sin los marcos de publicidad (colapsando dichos espacios). Fin del caso de uso.

### 5. SUB-VARIACIONES
- No presenta variaciones significativas desde el punto de vista del negocio.

### 6. POSTCONDICIONES
- Se registra la métrica de visualización y/o clic asociada a la campaña del patrocinador en la tabla de métricas (impacta en reportes estadísticos).

---

## Anexo: matrices de referencia

### Códigos HTTP usados

| Código HTTP | Nombre Técnico | Contexto de Aplicación en el Caso de Uso |
| --- | --- | --- |
| `200` | OK | Retorno exitoso de la lista de publicidades activas, o confirmación del registro de clic. |

### Matriz de trazabilidad CU-15 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal (Ver) | `200 OK` | `GetPublicidadByTorneoAsync_ReturnsBannersAndLogsImpression` | `GetPublicidad_Returns200OKAndLogsMetric` |
| Flujo principal (Clic) | `200 OK` | `RegisterClickAsync_LogsClickMetric` | `PostPublicidadClick_Returns200OK` |
| 3a. Sin publicidad | `200 OK` | `GetPublicidadByTorneoAsync_WhenEmpty_ReturnsEmptyList` | `GetPublicidad_WhenNoBanners_Returns200OKAndEmpty` |
