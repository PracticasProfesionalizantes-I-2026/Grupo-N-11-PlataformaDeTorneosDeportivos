# Issues y Tareas Pendientes (Auditoría QA & Arquitectura)

### 🔴 RIESGO ALTO (Crítico para la ejecución y completitud core)

- [ ] **Validación Algoritmo Fixture:** `BusinessLogic/Services/FixtureService.cs` - Módulo de generación de fixture automático (CU-08/16). Asegurar que el algoritmo soporte escenarios de cantidad impar de equipos (generación de fechas libres) y evite colisiones de partidos en la misma jornada.
- [ ] **Fugas de Autorización en Endpoints de Carga:** `API/Controllers/PartidosController.cs` y `IncidenciasController.cs` - Validar que únicamente los usuarios con rol de `Veedor` o `Admin` (CU-10, CU-11) puedan hacer POST/PUT de resultados e incidencias. Implementar claims/policy validation.
- [ ] **Consistencia de Transacciones:** `BusinessLogic/Services/InscripcionService.cs` - En la aprobación/rechazo de inscripciones (CU-06), asegurar el uso de transacciones de base de datos para evitar que un equipo quede a medias si el alta de sus jugadores falla en el proceso.

### 🟡 RIESGO MEDIO (Mantenibilidad y Flujos Secundarios)

- [ ] **Manejo de Roles (Usuarios vs Veedores):** `API/Controllers/VeedoresController.cs` y `BusinessLogic/Services/VeedorService.cs` - Analizar posible redundancia con `UsuarioService`. Un veedor es un actor/rol, no necesariamente una entidad separada. Refactorizar si se está duplicando la lógica de autenticación y perfilado (CU-07).
- [ ] **Optimización de Cálculo de Tablas:** `BusinessLogic/Services/TorneoService.cs` o `PartidoService.cs` - El cálculo de la tabla de posiciones (CU-13) podría sobrecargar la base de datos si se iteran todos los partidos en cada petición. Considerar implementar proyecciones, vistas materializadas en la DB, o una entidad temporal `TablaPosicion`.
- [ ] **Fuga de Información en Middleware:** `API/Middlewares/ExceptionHandlingMiddleware.cs` - Asegurarse de que las excepciones controladas no devuelvan el `StackTrace` completo en los entornos de Producción (Configurar validación basada en `IHostEnvironment`).

### 🟢 RIESGO BAJO (Deuda técnica y Mejoras Menores)

- [ ] **Archivos Basura (Scaffolding):** Eliminar `BusinessLogic/Class1.cs` y `DataAccess/Class1.cs`. Son archivos residuales generados al crear los proyectos en .NET.
- [ ] **Limpieza de Archivos de Sistema:** Ignorar o eliminar los archivos `.DS_Store` en todos los directorios (ej. `API.IntegrationTests/.DS_Store`, `DataAccess/.DS_Store`). Agregar al archivo `.gitignore`.
- [ ] **Dependencias Huérfanas de Testing:** Revisar `API.IntegrationTests/UnitTest1.cs` y `BusinessLogic.Tests/UnitTest1.cs`. Renombrar a clases de test descriptivas o eliminarlos si no contienen aserciones útiles.
- [ ] **Hardcoding de Configuración:** `API/appsettings.json` - Verificar que las cadenas de conexión o semillas del hasher en `BusinessLogic/Security/PasswordHasher.cs` no estén hardcodeadas en el código fuente, sino leídas desde variables de entorno.