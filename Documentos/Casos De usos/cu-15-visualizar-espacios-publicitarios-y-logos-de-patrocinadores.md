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

### 3. FLUJO PRINCIPAL (Camino Feliz - HTTP 200 OK)
1. El Actor navega por las secciones públicas del torneo.
2. El cliente (UI) efectúa una petición automática a `GET /api/torneos/{id}/publicidad` en paralelo a la carga principal de datos.
3. La **Capa de Negocio** (`PublicidadService.GetPublicidadByTorneoAsync`) recupera los recursos publicitarios activos asociados a dicho torneo más los banners globales de la plataforma.
4. El Sistema devuelve un código **200 OK** con la lista de `PublicidadResponseDTO` (URLs de imagen y enlaces de destino), registrando asíncronamente una impresión en la base de datos.
5. El cliente carga los banners en la interfaz.
6. El usuario hace clic sobre una publicidad.
7. La UI dispara una solicitud a `POST /api/publicidad/{bannerId}/click` para contabilizar la acción.
8. La **Capa de Negocio** (`PublicidadService.RegisterClickAsync`) valida la existencia del banner (lanza `NotFoundException` 404 si no existe), incrementa atómicamente el contador de clics y devuelve un código **200 OK** con `ClickPublicidadResponseDTO` conteniendo la URL destino de redirección.

### 4. FLUJOS ALTERNATIVOS (Caminos Tristes / Excepciones)

* **3a. Sin publicidad asignada (HTTP 200 OK):**
  1. Si en el Paso 3 el Sistema detecta que el torneo no tiene patrocinadores asignados ni existen banners globales activos.
  2. La **Capa de Negocio** retorna una colección vacía.
  3. El Sistema devuelve un código **200 OK** con respuesta `[]` y la interfaz se renderiza sin los marcos de publicidad. Fin del caso de uso.

* **7a. Banner Inexistente al registrar clic (HTTP 404 Not Found):**
  1. Si en el Paso 7 el `{bannerId}` no corresponde a ningún banner registrado en la base de datos.
  2. La **Capa de Negocio** lanza `NotFoundException`.
  3. El Sistema devuelve un error **404 Not Found**. Fin del caso de uso.

* **7b. Identificador de Banner Inválido (HTTP 400 Bad Request):**
  1. Si en el Paso 7 el `{bannerId}` es un GUID malformado o `Guid.Empty`.
  2. La **Capa de Presentación** rechaza la solicitud.
  3. El Sistema devuelve un código **400 Bad Request**. Fin del caso de uso.

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
| `400` | Bad Request | Identificador de banner malformado o Guid vacío. |
| `404` | Not Found | Banner no encontrado al registrar clic. |

### Matriz de trazabilidad CU-15 → Test

| Paso del CU | Excepción / Código | Test unitario (BusinessLogic) | Test integración (HTTP) |
| --- | --- | --- | --- |
| Flujo principal (Ver) | `200 OK` | `GetPublicidadByTorneoAsync_ReturnsBannersAndLogsImpression` | `GetPublicidad_Returns200OKAndLogsMetric`<br>`CU15_GetPublicidad_TorneoIdCualquiera_Retorna200OKYBannersGlobales` |
| Flujo principal (Clic) | `200 OK` | `RegisterClickAsync_LogsClickMetric` | `PostPublicidadClick_Returns200OK` |
| 3a. Sin publicidad | `200 OK` | `GetPublicidadByTorneoAsync_WhenEmpty_ReturnsEmptyList` | `GetPublicidad_WhenNoBanners_Returns200OKAndEmpty` |
| 7a. Banner inexistente | `404 Not Found` | `RegisterClickAsync_WhenNotFound_ThrowsNotFoundException` | `PostPublicidadClick_WhenNotFound_Returns404NotFound`<br>`CU15_ClickPublicidad_BannerInexistente_Retorna404NotFound` |
