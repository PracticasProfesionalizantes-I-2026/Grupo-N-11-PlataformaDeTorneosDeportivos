# Resumen de Evaluación Integral del Proyecto
## Plataforma de Torneos Deportivos — Auditoría SDLC (Diseño, Implementación y Calidad)

**Fecha de auditoría:** 08/11/2026  
**Alcance:** Documentación de negocio, 16 casos de uso, `README.md`, documentación técnica, código fuente de las 4 capas, migraciones, pruebas unitarias, pruebas de integración y colección Bruno.  
**Evidencia ejecutada:** `dotnet build` → 0 errores / 0 advertencias. `dotnet test` → **406 pruebas aprobadas** (146 unitarias + 260 de integración), 0 fallos.

---

## 1. Visión General

La entrega demuestra un **SDLC sensiblemente más maduro que el promedio de un trabajo académico de práctica profesionalizante**. No se limita a "hacer que la API responda": existe una cadena completa y reconocible de *análisis de negocio → especificación de casos de uso → diseño de arquitectura → implementación por capas → pruebas automatizadas → evidencia de API*.

Elementos que evidencian esa madurez:

- **Documentación de negocio formal** (`.docx`) con situación actual, oportunidad, stakeholders, alcance MVP, riesgos con mitigaciones, 6 requerimientos funcionales y 6 no funcionales.
- **16 casos de uso** en dos formatos complementarios: una especificación narrativa consolidada (`especificaciones - caso de uso.txt`) y **16 archivos Markdown individuales** con precondiciones, flujo principal, flujos alternativos, postcondiciones, matriz de códigos HTTP y **matriz de trazabilidad CU → test**.
- **Arquitectura N-Capas real y verificable**: `API → BusinessLogic → DataAccess → Shared`, con interfaces, inyección de dependencias, middleware global de excepciones, DTOs, repositorios y migraciones EF Core.
- **Suite de pruebas considerable y en verde**: 406 tests, incluyendo pruebas adversariales/fuzzing y validaciones exhaustivas, más 77 archivos Bruno.
- **Contratos HTTP consistentes**: manejo centralizado de errores con formato `{ "error": "..." }`, códigos 400/401/403/404/409/422 bien diferenciados.

El proyecto está **funcionalmente muy por encima de un MVP básico**, pero conserva brechas importantes en los flujos que exigen estado persistido (pagos, credenciales de veedor, progresión de llaves) y en seguridad a nivel de objeto (ownership/IDOR). Se puede describir como un **MVP avanzado con deuda de seguridad y de completitud de reglas de negocio**.

**Nivel de madurez estimado:** Alto en documentación y arquitectura; medio-alto en implementación funcional; medio en seguridad y consistencia transaccional.

---

## 2. Fortalezas Identificadas

### 2.1 Análisis y documentación
1. **Cobertura documental completa y trazable.** Los 16 CU están numerados, con actor, disparador, precondiciones, flujo, alternativos, postcondiciones y reglas de negocio.
2. **Trazabilidad explícita CU → endpoint → servicio → repositorio → excepción → test.** La sección 4 de `Documentos/IMPLEMENTACION_CASOS_DE_USO.md` y las matrices de cada CU son un activo muy valioso.
3. **Stakeholders bien identificados** (Organizador, Veedor/Árbitro, Capitán/Jugador/Espectador, Patrocinadores) y reflejados en los actores de los CU.
4. **Riesgos de negocio con mitigación** (fraude de resultados, curva de aprendizaje, escalabilidad) y una separación clara de qué queda fuera del MVP.
5. **Documentación de arquitectura pedagógica** (`ARQUITECTURA_Y_CONEXION_DE_CAPAS.md`) que explica el *por qué* de cada capa, el ciclo de vida de una petición, DI, patrón repositorio y estrategia de testing.

### 2.2 Diseño de casos de uso
6. **Flujos alternativos ricos**: duplicados, cupo lleno, empate en eliminación directa, jugador expulsado, token expirado, DNI repetido, etc.
7. **Reglas de negocio numeradas y citadas en el código** (RN-02 a RN-10), lo que facilita la auditoría y el mantenimiento.
8. **Definición explícita de códigos HTTP esperados** por flujo, algo poco habitual en trabajos de este nivel.

### 2.3 Arquitectura e implementación
9. **Separación de responsabilidades real**: los controladores son delgados, la lógica vive en `BusinessLogic`, la persistencia en `DataAccess` y los contratos en `Shared`. No se observan consultas EF dentro de los controladores.
10. **Inyección de dependencias por interfaz** y ciclo de vida `Scoped` correcto para `DbContext` y repositorios.
11. **Middleware global de excepciones** que elimina `try/catch` repetidos y unifica la respuesta de error.
12. **Uso correcto de `AsNoTracking()`** en la mayoría de lecturas y `DeleteBehavior.Restrict` para preservar integridad referencial.
13. **Hash de contraseñas con PBKDF2-SHA256, salt aleatorio y comparación de tiempo constante** (`PasswordHasher`), muy por encima de un hash débil.
14. **JWT con claims de rol** y autorización declarativa por rol en los endpoints de escritura.
15. **Diseño de DTOs de entrada/salida** que evita exponer entidades y mitiga *over-posting* (por ejemplo, `PagoValidado` no es asignable desde el alta de equipo).

### 2.4 Calidad y pruebas
16. **406 pruebas automatizadas en verde**, con separación limpia entre unitarias (Moq) e integración (`WebApplicationFactory`).
17. **Pruebas adversariales/fuzzing** (`RedTeamFuzzingServiceTests`, `AdversarialCasosDeUsoIntegrationTests`, `ValidacionesExhaustivasIntegrationTests`) que buscan 4xx en lugar de 500.
18. **Colección Bruno extensa** (77 archivos) que cubre caminos felices y errores, lo que facilita la verificación manual reproducible.
19. **Validaciones de entrada muy completas** (longitudes, regex, rangos, fechas, enums, HTML básico, DNIs, teléfonos, emails).

---

## 3. Análisis de Brechas (Gap Analysis)

### 3.1 Trazabilidad Requerimientos → Casos de Uso

| Requerimiento funcional | Caso(s) de uso que lo cubren | Estado de trazabilidad |
| :--- | :--- | :--- |
| RF-01 Gestión de torneos | CU-04 | ✅ Trazable |
| RF-02 Registro de equipos y jugadores | CU-05 | ✅ Trazable |
| RF-03 Generación automática de fixture | CU-08, CU-16 | ⚠️ Trazable pero **duplicado** (dos CU y dos endpoints para el mismo proceso) |
| RF-04 Carga de resultados y estadísticas | CU-10, CU-13, CU-14 | ⚠️ Parcial: la tabla de Liga se calcula, pero la **llave de eliminación no avanza** al ganador |
| RF-05 Validación de identidad (DNI único por torneo) | CU-05 | ✅ Trazable e implementado |
| RF-06 Gestión de sanciones | CU-11 | ⚠️ Parcial: expulsión **dentro del partido**, sin acumulación ni suspensión para el partido siguiente |

**Hallazgos de trazabilidad:**

- **Requerimientos huérfanos de CU:** el módulo de **publicidad** (CU-15) no deriva de ningún RF explícito (solo de la visión y de los stakeholders "Patrocinadores"). Es una funcionalidad legítima, pero debería formalizarse como RF-07.
- **Funciones documentadas sin CU ni código:** la visión de la solución promete un **"Centro de Notificaciones y Difusión Automatizada"** y un **"Tablero de Control y Reportes para Organizadores"**. Ninguno tiene caso de uso ni implementación. Son brechas de alcance, no solo de código.
- **CU-08 y CU-16 se solapan** (solicitar vs. generar fixture) y además tienen reglas contradictorias: CU-08 admite torneo `Abierto`, CU-16 exige `InscripcionesCerradas`; CU-08 fija mínimo 2 para Liga, CU-16 exige 4 para cualquier modalidad. La documentación no resuelve cuál prevalece.
- **Numeración de reglas inconsistente:** en el documento de negocio, "RN-02" se usa tanto para "jugador no puede estar en dos equipos" como para "cupo mínimo 4 en eliminación directa". Eso rompe la trazabilidad de reglas.
- **Actores:** el mapeo es correcto y completo, pero "Sistema" aparece como actor principal de CU-16, lo cual es una imprecisión metodológica (el actor es el Organizador; el sistema es el ejecutante). Los patrocinadores figuran solo como actor secundario y nunca interactúan con el sistema.

### 3.2 NFRs vs. casos de uso

| NFR | Reflejo en CU | Cobertura real en el código |
| :--- | :--- | :--- |
| RNF-01 Disponibilidad 24h | No aparece en pre/postcondiciones | No hay health checks, despliegue ni evidencias de disponibilidad |
| RNF-02 Respuesta < 2 s | No aparece | No hay pruebas de rendimiento ni índices más allá de DNI/Email |
| RNF-03 Facilidad de uso | No aparece en los CU detallados | No evaluable: la entrega es API, sin frontend |
| RNF-04 Seguridad de acceso | Parcial (precondición "Token JWT" en varios CU) | Parcial: hay JWT y roles, pero hay escalada de privilegios, IDOR y exposición de PII |
| RNF-05 Adaptabilidad móvil | No aparece | No evaluable: no hay UI |
| RNF-06 Organización del código | Implícita en la arquitectura | Parcialmente cumplida: buena separación por capas, pero con clases Dios y duplicación |

**Conclusión de esta fase:** la documentación funcional es fuerte, pero los **NFRs no están operacionalizados** en las pre/postcondiciones ni en criterios de aceptación medibles. RNF-04 es el único parcialmente reflejado, y precisamente donde el código tiene más deuda.

### 3.3 Diseño vs. Código — Brechas funcionales principales

| # | Concepto documentado | Estado en el código | Impacto |
| :--- | :--- | :--- | :--- |
| 1 | CU-06: validar comprobante de pago y aprobar solo con pago acreditado (`PagoValidado`) | `PagoValidado` **nunca se establece en `true`** en ningún endpoint; el `ComprobanteUrl` se valida en el DTO pero **no se persiste** en la entidad `Equipo` | 🔴 El flujo de aprobación de torneos pagos es **inalcanzable** |
| 2 | CU-16: generar llaves "de primera ronda y **ramas subsiguientes**"; CU-04/RF-04: "avanzar al ganador en la llave" | Solo se generan partidos de ronda 1. `PartidoService` guarda el resultado pero **no crea el partido de la ronda siguiente** | 🔴 El bracket queda congelado en primera ronda |
| 3 | CU-07: enviar credenciales al veedor | Se genera una contraseña temporal pero **no se devuelve ni se envía**; el veedor queda con credenciales desconocidas | 🔴 El veedor no puede iniciar sesión |
| 4 | CU-01/CU-06/CU-07/CU-08: notificaciones por correo | No existe servicio de correo ni abstracción de notificaciones | 🟡 Flujos secundarios incompletos |
| 5 | CU-15: el Organizador configura banners | `PublicidadService.CreateAsync` existe, pero **ningún endpoint lo expone**; tampoco hay seed de banners | 🟡 La vitrina publicitaria nunca tiene contenido |
| 6 | CU-10/CU-13: actualización automática de posiciones y llaves | La tabla de posiciones se recalcula al leer; la llave no avanza y el torneo nunca pasa a `Finalizado` | 🟡 Parcial |
| 7 | CU-11: "Finalizar Partido", acta digital y consulta de incidencias | Solo existe `POST /incidencias`; no hay cierre de partido ni `GET` de incidencias | 🟡 Parcial |
| 8 | CU-05: persistir foto/deslinde, dorsal, posición, edad, contacto del jugador | Se validan en el DTO pero la entidad `Jugador` solo guarda Nombre, Apellido, DNI y DocumentacionUrl | 🟡 Pérdida silenciosa de datos |
| 9 | CU-04: descripción, reglamento y fechas del torneo | Se validan en el DTO pero `Torneo` no tiene esas columnas: se descartan al persistir | 🟡 Pérdida silenciosa de datos |
| 10 | CU-08: mínimo 2 equipos en Liga | El código exige **4 equipos para toda modalidad** | 🟡 Sobrerrestricción no documentada |
| 11 | RN-03: la lista no se edita tras el envío salvo rechazo | `EdicionListaHabilitada` se escribe pero **nunca se lee**; `JugadorService.CreateAsync` permite agregar jugadores siempre | 🔴 Regla de negocio invertida/no aplicada |
| 12 | CU-05: equipo ligado al perfil del Capitán | `Equipo` no tiene `CapitanId` ni relación con `Usuario` | 🔴 Sin ownership: cualquier Capitán opera sobre cualquier equipo |
| 13 | RN-05: cuenta recaudadora activa | `CuentaRecaudadoraService` siempre devuelve `true` y además el cliente puede sobrescribir el resultado con `CuentaRecaudadoraActiva` en el DTO | 🟡 Regla controlada por el cliente |
| 14 | RNF-04: solo administradores modifican | El registro público acepta `Rol = Administrador` u `Organizador` sin restricción | 🔴 Escalada de privilegios |

### 3.4 Orfandad y *scope creep*

- **Orfandad documental:** CU-15 (publicidad) no tiene RF; los módulos de notificaciones y reportes no tienen CU.
- **Código sin diseño explícito (scope creep controlado):** `EquipoService.CambiarEstadoAsync` y el endpoint `PUT /api/equipos/{id}/estado` introducen un **segundo mecanismo de aprobación** que no está en CU-06 y que **omite la validación de pago**. Es el hallazgo de mayor riesgo funcional del proyecto.
- **Código muerto:** `EquipoUpdateDTO`, `PublicidadService.MapToClickResponseDTO`, overloads `CreateAsync`/`CreateEquipoAsync` y `Class1.cs` en tres proyectos.
- **Duplicación de contrato:** `GeneratedFixture` tiene dos DTOs y dos endpoints (`/generar-fixture` y `/fixture`) con reglas distintas.

---

## 4. Conclusión de Arquitectura y Calidad

### 4.1 Lo que está bien
La arquitectura en N-Capas **se cumple en la práctica**, no solo en el diagrama: no hay `DbContext` en controladores, no hay lógica de negocio en la capa API y los contratos están centralizados en `Shared`. El middleware de excepciones y las excepciones tipadas son una decisión de diseño acertada y bien ejecutada. La cobertura de tests y la disciplina de documentación son claramente superiores a la media.

### 4.2 Deuda técnica relevante
1. **Clases Dios y duplicación:** `TorneoService` (659 líneas) y `EquipoService` (594 líneas) concentran validación, mapeo, reglas y orquestación. `TorneoService.CreateTorneoAsync` y `UpdateAsync` repiten ~120 líneas de validación casi idénticas; `EquipoService` valida dos veces el mismo bloque de jugadores (líneas 129-136 y 167-180).
2. **Validación duplicada DTO + Servicio:** las mismas reglas se implementan en `IValidatableObject` y otra vez en el servicio con regex copiadas. Es un riesgo de divergencia y un coste de mantenimiento alto.
3. **Primitive Obsession y modelado débil:** `TorneosAsignadosIds` se guarda como **string CSV** en `Usuario` en lugar de una tabla de relación; los estados y roles se transportan como `string`; `CambiarEstadoAsync` recibe `string`. Esto impide integridad referencial y complica consultas.
4. **Dependencias opcionales que ocultan errores:** varios servicios reciben repositorios `nullable = null` y, si no se inyectan, **omiten validaciones o devuelven resultados vacíos** en silencio (`TorneoService.GetPosicionesAsync`, `JugadorService`, `EquipoService`, `PublicidadService`). Un error de configuración de DI se convierte en un bug de negocio.
5. **Falta de transacciones:** la generación de fixture y la aprobación de inscripción hacen **dos `SaveChanges` separados**. Si falla el segundo, el torneo queda `EnDesarrollo` sin partidos o el equipo confirmado sin actualizar el cupo. El CU-16 exige explícitamente comportamiento transaccional.
6. **Ausencia casi total de logging/observabilidad:** el middleware captura `Exception` pero no registra nada; no hay `ILogger`, correlation IDs ni métricas. El caso de uso 6a de CU-04/CU-16 promete "registrar la excepción técnica en el log de auditoría", y no ocurre.
7. **Concurrencia optimista inexistente:** el control de cupo es *check-then-act*; dos aprobaciones simultáneas pueden superar el cupo. No hay `RowVersion` ni transacción serializable.
8. **Seguridad a nivel de objeto:** no hay ownership. Un Capitán autenticado puede borrar cualquier jugador por ID y consultar/operar equipos ajenos; los endpoints públicos de equipos exponen DNI y URLs de documentación de jugadores.
9. **Persistencia y migraciones inconsistentes:** `DbInitializer` usa `EnsureCreated()` mientras el proyecto mantiene migraciones EF. En producción, `EnsureCreated` no aplica migraciones futuras y puede crear un esquema divergente.
10. **Acoplamiento a detalles:** `GenericRepository` usa reflexión para asignar `Guid`, cada operación hace `SaveChanges` (no hay Unit of Work explícito) y los repositorios especializados mezclan lectura con escritura masiva (`AddRangeAsync`).

### 4.3 Diagnóstico final
El proyecto **aprueba con solvencia en análisis, diseño documental y arquitectura**, y **aprueba en funcionalidad núcleo** (torneos, equipos, jugadores, fixture de Liga, resultados, posiciones, incidencias, estadísticas). Sin embargo, **no está listo para producción** por tres motivos concretos:

1. **Seguridad:** escalada de privilegios en el registro, token de recuperación devuelto en la respuesta, IDOR en jugadores/equipos y exposición pública de PII.
2. **Completitud de flujos core:** el pago no puede validarse, el veedor no recibe credenciales y el bracket de eliminación no avanza.
3. **Integridad transaccional y observabilidad:** operaciones multi-paso sin transacción y ausencia de logging.

La buena noticia es que **ninguna de estas brechas exige reescribir la arquitectura**. La base (capas, DTOs, excepciones, tests) es sólida y permite corregirlas de forma incremental. El esfuerzo recomendado se concentra en: cerrar los flujos de pago/credenciales, agregar ownership y autorización a nivel de recurso, completar la progresión de llaves y añadir transacciones + logging.

**Recomendación:** tratar los ítems de riesgo alto del archivo `issues_github_pendientes.md` como *release blockers* antes de considerar la versión 1.0 entregable; el resto puede planificarse como deuda técnica de la versión 1.1.
