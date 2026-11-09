# Documento Técnico de Implementación de Casos de Uso
## Solución: Plataforma de Torneos Deportivos

**Versión:** 1.0  
**Framework:** .NET 10 (C# 14)  
**Patrón Arquitectónico:** Arquitectura en N-Capas (N-Tier) / Clean Architecture  
**Persistencia:** Entity Framework Core con SQLite  
**Manejo de Errores:** Middleware Global (`ExceptionHandlingMiddleware`)  
**Pruebas Automatizadas:** xUnit, Moq, Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory), Bruno API Client  

---

## 1. Arquitectura y Convenciones Globales

La solución se encuentra estructurada estrictamente en cuatro capas desacopladas, garantizando separación de responsabilidades, testeabilidad y cumplimiento de principios SOLID:

1. **`API` (Capa de Presentación / Controladores):**
   - Expone los endpoints RESTful mediante controladores (`[ApiController]`).
   - Depende exclusivamente de abstracciones de servicios definidas en `BusinessLogic` y modelos DTO en `Shared`.
   - No contiene lógica de negocio, cálculos de dominio ni acceso directo a base de datos.
   - Captura y serializa todas las excepciones del dominio a través de [ExceptionHandlingMiddleware](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Middlewares/ExceptionHandlingMiddleware.cs).

2. **`BusinessLogic` (Capa de Lógica de Negocio):**
   - Contiene los servicios y sus interfaces correspondientes.
   - Implementa **todas** las validaciones y reglas de negocio (RN).
   - No tiene acoplamiento con tecnologías web (sin referencias a `HttpContext`, `IActionResult`, códigos HTTP directos).
   - Realiza mapeos manuales determinísticos entre Entidades de Dominio y Data Transfer Objects (DTOs).
   - Accede a los datos exclusivamente mediante inyección de dependencias de repositorios (`DataAccess.Repositories`).

3. **`DataAccess` (Capa de Acceso a Datos):**
   - Define el contexto de base de datos [AppDbContext](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Data/AppDbContext.cs) y las entidades del modelo relacional.
   - Implementa el patrón Repositorio Genérico (`GenericRepository<T>`) y repositorios especializados con consultas LINQ optimizadas y `AsNoTracking()` para lecturas.
   - Las relaciones foráneas se encuentran configuradas con `DeleteBehavior.Restrict` para preservar la integridad referencial.

4. **`Shared` (Capa Transversal / Contratos):**
   - Define los enumeradores de dominio (`RolUsuario`, `Disciplina`, `Modalidad`, `EstadoTorneo`, `EstadoInscripcion`, `EstadoPartido`, `TipoIncidencia`, etc.).
   - Contiene los DTOs de entrada y salida con atributos de validación declarativa (`DataAnnotations`).
   - Centraliza las excepciones de dominio tipadas (`ValidationException`, `NotFoundException`, `BusinessRuleConflictException`, `UnauthorizedException`, `ForbiddenException`, etc.).

---

## 2. Mapa Global de Códigos HTTP y Excepciones de Dominio

El middleware [ExceptionHandlingMiddleware](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Middlewares/ExceptionHandlingMiddleware.cs) traduce de forma centralizada las excepciones de negocio a códigos de estado HTTP estandarizados:

| Código HTTP | Significado | Excepciones Mapeadas |
| :---: | :--- | :--- |
| **`400 Bad Request`** | Parámetros inválidos, datos faltantes o formato erróneo | `ValidationException` |
| **`401 Unauthorized`** | Credenciales incorrectas o falta de autenticación | `UnauthorizedException` |
| **`403 Forbidden`** | Cuenta bloqueada/inactiva o permisos insuficientes | `ForbiddenException` |
| **`404 Not Found`** | Recurso solicitado inexistente | `NotFoundException`, `TorneoNotFoundException`, `RecursoNotFoundException`, `DisciplinaNotFoundException` |
| **`409 Conflict`** | Conflicto de estado o regla de negocio violada | `BusinessRuleConflictException`, `TorneoInscripcionCerradaException`, `JugadorYaInscriptoException`, `EquipoNombreDuplicadoException`, `TorneoNombreDuplicadoException`, `TorneoCupoLlenoException`, `EstadoEquipoInvalidoException` |
| **`422 Unprocessable Entity`** | Parámetros semánticamente inconsistentes con las reglas de negocio | `CupoInvalidoEliminacionException`, `CuentaRecaudadoraInactivaException`, `MinimoJugadoresInsuficienteException`, `CostoInvalidoException`, `PagoRequeridoException` |
| **`500 Internal Server Error`** | Error no contemplado | `Exception` no controlada |

---

## 3. Implementación Detallada por Caso de Uso

---

### CU-01: Registrarse en la plataforma (Crear cuenta)

* **Descripción:** Permite a un nuevo usuario darse de alta en el sistema suministrando sus datos personales y credenciales. Por diseño de seguridad y prevención de escalada de privilegios (RNF-04), el registro público fuerza estrictamente el rol base (`Capitan` / `Espectador`), neutralizando cualquier inyección de roles elevados (`Administrador`, `Organizador`, `Veedor`). La creación o asignación de roles privilegiados se reserva a endpoints administrativos protegidos.
* **Actor Principal:** Usuario No Autenticado.
* **Reglas de Negocio:**
  - **RN-10:** El DNI y el Email son de carácter único en toda la base de datos.
  - **Prevención de Escalada de Privilegios (RNF-04):** En el registro público (`POST /api/usuarios/registro` y `POST /api/auth/register`), el sistema fuerza el rol base (`Capitan`), bloqueando cualquier intento de autoproclamación como `Administrador`, `Organizador` o `Veedor`. La creación de cuentas con roles elevados o la modificación de roles se encuentra restringida a administradores/organizadores autenticados (`POST /api/usuarios/admin`, `PUT /api/usuarios/{id}/rol` y `POST /api/veedores`).
  - La contraseña debe cumplir requisitos mínimos de seguridad (mínimo 8 caracteres, al menos un número y una letra).
  - La contraseña se almacena de forma segura mediante hash criptográfico (PBKDF2 a través de `IPasswordHasher`).

#### Componentes de Implementación:
- **Controlador API:** [UsuariosController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/UsuariosController.cs)
  - Endpoints:
    - `POST /api/usuarios/registro` y `POST /api/auth/register` (Público, rol base forzado)
    - `GET /api/usuarios/{id}` (Protegido `[Authorize(Roles = "Administrador,Organizador")]`)
    - `POST /api/usuarios/admin` (Protegido `[Authorize(Roles = "Administrador,Organizador")]`)
    - `PUT /api/usuarios/{id}/rol` (Protegido `[Authorize(Roles = "Administrador,Organizador")]`)
  - Códigos de Respuesta: `200 OK`, `201 Created`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict`.
- **Capa BusinessLogic:**
  - Interfaz: [IUsuarioService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IUsuarioService.cs)
  - Servicio: [UsuarioService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/UsuarioService.cs)
  - Hasher de Seguridad: [IPasswordHasher.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Security/IPasswordHasher.cs)
- **Capa DataAccess:**
  - Repositorio: [IUsuarioRepository.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Repositories/IUsuarioRepository.cs)
  - Entidad: [Usuario.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Usuario.cs)
- **Contratos (Shared):**
  - DTOs: `UsuarioRegistroDTO`, `UsuarioResponseDTO`, `CambiarRolDTO`.
  - Excepciones: `ValidationException`, `UsuarioDuplicadoException`, `ForbiddenException`, `NotFoundException`.
- **Pruebas Automatizadas:**
  - Unitarias: `UsuarioServiceTests.cs` (Validación de contraseña, DNI/Email duplicado, forzado de rol base Capitan, validación de permisos en creación/cambio de rol).
  - Integración: `UsuariosIntegrationTests.cs` (Neutralización de escalada en `/api/usuarios/registro` y `/api/auth/register`, bloqueo 401/403 a no administradores, asignación legítima 201/200 por Administrador).
  - Archivos Bruno: `CU01_Registro_400_Invalido.bru`, `CU01_Registro_409_Duplicado.bru`, `RegistroUsuario.bru`.

---

### CU-02: Iniciar sesión

* **Descripción:** Permite a un usuario autenticarse mediante su correo electrónico y contraseña, obteniendo un token JWT con sus claims y rol asignado.
* **Actor Principal:** Usuario Registrado.
* **Reglas de Negocio:**
  - Verificación de credenciales contra el hash almacenado.
  - La cuenta debe estar activa (`Activo == true`), de lo contrario se deniega el acceso con `403 Forbidden`.
  - Se genera un token JWT firmado criptográficamente con tiempo de expiración y roles integrados.

#### Componentes de Implementación:
- **Controlador API:** [AuthController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/AuthController.cs)
  - Endpoint: `POST /api/auth/login`
  - Códigos de Respuesta: `200 OK`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`.
- **Capa BusinessLogic:**
  - Interfaz: [IAuthService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IAuthService.cs)
  - Servicio: [AuthService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/AuthService.cs)
- **Capa DataAccess:**
  - Repositorio: [IUsuarioRepository.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Repositories/IUsuarioRepository.cs)
- **Contratos (Shared):**
  - DTOs: `LoginRequestDTO`, `AuthResponseDTO`.
  - Excepciones: `UnauthorizedException`, `ForbiddenException`, `ValidationException`.
- **Pruebas Automatizadas:**
  - Unitarias: `AuthServiceTests.cs` (Login exitoso, credenciales erróneas, cuenta inactiva).
  - Integración: `AuthIntegrationTests.cs`.
  - Archivos Bruno: `CU02_Login_400_Incompleto.bru`, `CU02_Login_401_CredencialesIncorrectas.bru`, `CU02_Login_403_CuentaInactiva.bru`, `Login.bru`.

---

### CU-03: Recuperar contraseña

* **Descripción:** Permite a un usuario solicitar el restablecimiento de su clave mediante generación de un token temporal seguro y posterior actualización de la contraseña.
* **Actor Principal:** Usuario Registrado.
* **Reglas de Negocio:**
  - Solicitud idempotente: Si el correo no existe, por seguridad se informa que si el usuario existe recibirá las instrucciones (evitando enumeración de usuarios).
  - Token con caducidad temporal (ej. 24 horas) y de uso único.
  - Validación de robustez de la nueva clave.

#### Componentes de Implementación:
- **Controlador API:** [AuthController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/AuthController.cs)
  - Endpoints:
    - `POST /api/auth/recuperar-password`
    - `POST /api/auth/reset-password`
  - Códigos de Respuesta: `200 OK`, `400 Bad Request`.
- **Capa BusinessLogic:**
  - Servicio: [AuthService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/AuthService.cs)
- **Capa DataAccess:**
  - Entidad: [Usuario.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Usuario.cs) (`PasswordResetToken`, `ResetTokenExpiresAt`).
- **Contratos (Shared):**
  - DTOs: `RecuperarPasswordRequestDTO`, `ResetPasswordRequestDTO`, `GenericMessageResponseDTO`.
- **Pruebas Automatizadas:**
  - Unitarias: `AuthServiceTests.cs` (Token válido, token expirado, contraseña insegura).
  - Integración: `AuthIntegrationTests.cs`.
  - Archivos Bruno: `CU03_RecuperarPassword_200_CorreoNoRegistrado.bru`, `CU03_ResetPassword_400_PasswordInseguro.bru`, `CU03_ResetPassword_400_TokenExpirado.bru`.

---

### CU-04: Crear y configurar un nuevo torneo

* **Descripción:** Permite al Organizador dar de alta una competencia deportiva definiendo disciplina, modalidad, cupos y costos.
* **Actor Principal:** Organizador / Administrador.
* **Reglas de Negocio:**
  - **RN-04:** En modalidad Eliminación Directa, el cupo debe ser estrictamente potencia de 2 y mínimo 4 equipos (`CupoMaximo >= 4 && (CupoMaximo & (CupoMaximo - 1)) == 0`).
  - **RN-05:** Todo torneo pago (`EsPago == true` o `CostoInscripcion > 0`) requiere que el costo sea estrictamente superior a cero y que la cuenta recaudadora esté activa.
  - Validación de nombre único en el sistema (case-insensitive).
  - Estado inicial obligatorio: `EstadoTorneo.Abierto`.

#### Componentes de Implementación:
- **Controlador API:** [TorneosController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/TorneosController.cs)
  - Endpoint: `POST /api/torneos`
  - Códigos de Respuesta: `201 Created`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `422 Unprocessable Entity`.
- **Capa BusinessLogic:**
  - Interfaz: [ITorneoService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/ITorneoService.cs)
  - Servicio: [TorneoService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/TorneoService.cs)
  - Verificación externa: [ICuentaRecaudadoraService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/ICuentaRecaudadoraService.cs)
- **Capa DataAccess:**
  - Repositorio: [ITorneoRepository.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Repositories/ITorneoRepository.cs)
  - Entidad: [Torneo.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Torneo.cs)
- **Contratos (Shared):**
  - DTOs: `TorneoCreateDTO`, `TorneoResponseDTO`.
  - Excepciones: `TorneoNombreDuplicadoException` (409), `CupoInvalidoEliminacionException` (422), `CuentaRecaudadoraInactivaException` (422), `CostoInvalidoException` (422), `DisciplinaNotFoundException` (404).
- **Pruebas Automatizadas:**
  - Unitarias: `TorneoServiceTests.cs`.
  - Integración: `TorneosIntegrationTests.cs`.
  - Archivos Bruno: `CU04_CreateTorneo_400_CamposFaltantes.bru`, `CU04_CreateTorneo_409_NombreDuplicado.bru`, `CU04_CreateTorneo_422_CupoEliminacionInvalido.bru`, `CU04_CreateTorneo_422_CuentaRecaudadoraInactiva.bru`, `CreateTorneo.bru`.

---

### CU-05: Registrar y dar de alta equipos y listas de jugadores

* **Descripción:** Permite a un Capitán inscribir a su equipo en un torneo abierto y suministrar la lista de buena fe de sus deportistas.
* **Actor Principal:** Capitán de Equipo.
* **Reglas de Negocio:**
  - **RN-02:** Un jugador (identificado por su DNI) no puede encontrarse inscripto en más de un equipo dentro del mismo torneo.
  - **RN-03:** Al enviar la inscripción, el equipo queda en estado `PendienteDeValidacion` y se bloquea la edición (`EdicionListaHabilitada = false`).
  - **Prevención de Vulnerabilidad IDOR y Control de Titularidad:** La entidad `Equipo` cuenta con clave foránea hacia `Usuario` (`CapitanId` / `Usuario? Capitan`) configurada con `DeleteBehavior.Restrict`. En los endpoints `POST /api/torneos/{torneoId}/equipos`, `POST /api/equipos/{equipoId}/jugadores` y `DELETE /api/jugadores/{id}`, la Capa de Presentación extrae los claims `ClaimTypes.NameIdentifier` y `ClaimTypes.Role`, delegando la verificación a la Capa de Negocio (`JugadorService` y `EquipoService`). Si un Capitán intenta agregar/eliminar jugadores en un equipo ajeno o registrar un equipo suplantando a otro capitán, se lanza `ForbiddenException` (`403 Forbidden`). Se reserva el bypass administrativo para usuarios con rol `Organizador` o `Administrador`.
  - **RNF-04 (Privacidad y Protección de Datos Personales - PII):** En las consultas públicas (`GET /api/equipos`, `GET /api/torneos/{torneoId}/equipos`, `GET /api/equipos/{id}`, `GET /api/equipos/{equipoId}/jugadores`), los jugadores se exponen a través de `JugadorPublicResponseDTO` (y `EquipoPublicResponseDTO`), excluyendo estrictamente su `Dni` y `DocumentacionUrl`. El acceso a la nómina completa y sensible queda restringido a usuarios autenticados con rol `Organizador`/`Administrador` o al `Capitan` del equipo correspondiente (`GET /api/equipos/{id}/nomina-completa`).
  - No se admiten DNIs duplicados dentro de la misma lista de envío.
  - Se valida el mínimo reglamentario de jugadores según la disciplina del torneo (ej. Fútbol: mínimo 5; Básquetbol: mínimo 5; Voleibol: mínimo 6; E-Sports: mínimo 3).
  - El torneo debe encontrarse en estado `EstadoTorneo.Abierto`.

#### Componentes de Implementación:
- **Controlador API:** [EquiposController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/EquiposController.cs) y [JugadoresController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/JugadoresController.cs)
  - Endpoints:
    - `POST /api/torneos/{torneoId}/equipos` (Inscripción de equipo y lista)
    - `GET /api/equipos` y `GET /api/equipos/{id}` (Consulta pública con `EquipoPublicResponseDTO`)
    - `GET /api/equipos/{id}/nomina-completa` y `GET /api/equipos/{id}/admin` (Nómina completa protegida con `[Authorize]`)
    - `GET /api/equipos/{equipoId}/jugadores` y `GET /api/equipos/{equipoId}/jugadores/nomina-completa`
  - Códigos de Respuesta: `200 OK`, `201 Created`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict`, `422 Unprocessable Entity`.
- **Capa BusinessLogic:**
  - Interfaz: [IEquipoService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IEquipoService.cs), [IJugadorService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IJugadorService.cs)
  - Servicio: [EquipoService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/EquipoService.cs), [JugadorService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/JugadorService.cs)
- **Capa DataAccess:**
  - Repositorios: [IEquipoRepository.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Repositories/IEquipoRepository.cs), [IJugadorRepository.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Repositories/IJugadorRepository.cs).
  - Entidades: [Equipo.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Equipo.cs), [Jugador.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Jugador.cs).
- **Contratos (Shared):**
  - DTOs: `EquipoCreateDTO`, `EquipoResponseDTO`, `EquipoPublicResponseDTO`, `JugadorCreateDTO`, `JugadorResponseDTO`, `JugadorPublicResponseDTO`.
  - Excepciones: `JugadorYaInscriptoException` (409), `TorneoInscripcionCerradaException` (409), `EquipoNombreDuplicadoException` (409), `TorneoCupoLlenoException` (409), `MinimoJugadoresInsuficienteException` (422), `ForbiddenException` (403).
- **Pruebas Automatizadas:**
  - Unitarias: `EquipoServiceTests.cs`, `JugadorServiceTests.cs`.
  - Integración: `EquiposIntegrationTests.cs`, `JugadoresIntegrationTests.cs`.
  - Archivos Bruno: `CU05_CreateEquipo_400_DniDuplicado.bru`, `CU05_CreateEquipo_409_CupoLleno.bru`, `CU05_CreateEquipo_409_JugadorYaInscripto.bru`, `CU05_CreateEquipo_409_TorneoCerrado.bru`, `CU05_CreateEquipo_422_MinimoJugadores.bru`, `CreateEquipo.bru`.

---

### CU-06: Validar y dictaminar la inscripción y documentación de equipos

* **Descripción:** Permite al Administrador/Organizador evaluar la documentación y pagos de un equipo, aprobando su inclusión definitiva o rechazándola con observaciones para corrección.
* **Actor Principal:** Organizador / Administrador.
* **Reglas de Negocio:**
  - **RN-05 / RN-06:** No se puede marcar a un equipo como `InscritoYConfirmado` si no acredita comprobante de pago válido (`ComprobanteUrl` no vacío y persistido) y validado por el Organizador (`PagoValidado = true`) en torneos pagos, o si el torneo ya completó su cupo máximo (`confirmados >= torneo.CupoMaximo`).
  - Solo pueden dictaminarse equipos que estén en estado `PendienteDeValidacion`.
  - **RN-03:** Si la inscripción es rechazada u observada (`RechazadoObservado`), el sistema desbloquea la edición (`EdicionListaHabilitada = true`) para permitir al capitán subsanar la nómina.
  - El Organizador puede validar la acreditación de pagos de un equipo mediante `PATCH /api/torneos/{torneoId}/equipos/{equipoId}/validar-pago` o `PATCH /api/equipos/{id}/validar-pago`, actualizando `PagoValidado = true`.

#### Componentes de Implementación:
- **Controlador API:** [InscripcionesController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/InscripcionesController.cs) y [EquiposController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/EquiposController.cs)
  - Endpoints:
    - `PATCH /api/torneos/{torneoId}/equipos/{equipoId}/aprobar`
    - `PATCH /api/torneos/{torneoId}/equipos/{equipoId}/rechazar`
    - `PATCH /api/torneos/{torneoId}/equipos/{equipoId}/validar-pago`
    - `PATCH /api/equipos/{id}/validar-pago`
    - `PUT /api/equipos/{id}/estado` (Máquina de estados estricta centralizada en `InscripcionService`)
  - Códigos de Respuesta: `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `422 Unprocessable Entity`.
- **Capa BusinessLogic:**
  - Interfaz: [IInscripcionService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IInscripcionService.cs)
  - Servicio: [InscripcionService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/InscripcionService.cs)
  - Delegación: [EquipoService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/EquipoService.cs) delega las transiciones de estado a `_inscripcionService.CambiarEstadoEquipoAsync` y persiste `ComprobanteUrl`.
- **Contratos (Shared):**
  - DTOs: `AprobarInscripcionResponseDTO`, `RechazarInscripcionDTO`, `RechazarInscripcionResponseDTO`, `EquipoCreateDTO`, `EquipoUpdateDTO`, `EquipoResponseDTO`, `ValidarPagoDTO`, `ValidarPagoResponseDTO`.
  - Excepciones: `TorneoCupoLlenoException` (409), `EstadoEquipoInvalidoException` (409), `PagoRequeridoException` (422), `RecursoNotFoundException` (404), `BusinessRuleConflictException` (409).
- **Pruebas Automatizadas:**
  - Unitarias: `InscripcionServiceTests.cs`, `EquipoServiceTests.cs`.
  - Integración: `InscripcionesIntegrationTests.cs`, `EquiposIntegrationTests.cs`.
  - Archivos Bruno: `CU06_AprobarInscripcion_409_CupoLleno.bru`, `CU06_AprobarInscripcion_422_SinPago.bru`, `CU06_ValidarPago_200.bru`, `CU06_RechazarInscripcion_400_SinMotivo.bru`, `AprobarInscripcion.bru`, `RechazarInscripcion.bru`.

---

### CU-07: Crear y gestionar usuarios con rol de Veedor

* **Descripción:** Permite al Organizador crear, actualizar datos o dar de baja lógica cuentas de veedores/árbitros para la fiscalización de partidos, con entrega de contraseña provisoria y cambio obligatorio en el primer inicio de sesión.
* **Actor Principal:** Organizador / Administrador.
* **Reglas de Negocio:**
  - El veedor se crea con rol `RolUsuario.Veedor`.
  - Validación de unicidad de DNI y Email (RN-10).
  - Al crearse, se puede suministrar una `PasswordInicial` o se genera automáticamente una contraseña provisoria segura.
  - La respuesta de creación (`VeedorResponseDTO`) entrega la `PasswordTemporal` generada y la bandera `RequiereCambioPassword = true`.
  - En consultas posteriores (`GET /api/veedores/{id}`, listados o actualizaciones), `PasswordTemporal` se mantiene oculta (`null`) por seguridad.
  - Al iniciar sesión con la clave provisoria, el sistema informa `RequiereCambioPassword = true`. El usuario debe completar el cambio obligatorio mediante `POST /api/auth/cambiar-password` o `POST /api/auth/primer-ingreso` antes de operar.
  - La baja es un borrado lógico (`Activo = false`), revocando permisos de carga de planillas de forma inmediata.

#### Componentes de Implementación:
- **Controlador API:** [VeedoresController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/VeedoresController.cs) / [AuthController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/AuthController.cs)
  - Endpoints: `GET /api/veedores`, `GET /api/veedores/{id}`, `POST /api/veedores`, `PUT /api/veedores/{id}`, `DELETE /api/veedores/{id}`, `POST /api/auth/cambiar-password`, `POST /api/auth/primer-ingreso`.
  - Códigos de Respuesta: `200 OK`, `201 Created`, `204 NoContent`, `400 Bad Request`, `401 Unauthorized`, `404 Not Found`, `409 Conflict`.
- **Capa BusinessLogic:**
  - Interfaz: [IVeedorService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IVeedorService.cs) / [IAuthService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IAuthService.cs)
  - Servicio: [VeedorService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/VeedorService.cs) / [AuthService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/AuthService.cs)
- **Contratos (Shared):**
  - DTOs: `VeedorCreateDTO`, `VeedorUpdateDTO`, `VeedorResponseDTO`, `CambiarPasswordInicialDTO`, `CambiarPasswordResponseDTO`, `LoginResponseDTO`.
- **Pruebas Automatizadas:**
  - Unitarias: `VeedorServiceTests.cs`, `AuthServiceTests.cs`.
  - Integración: `VeedoresIntegrationTests.cs`, `AuthIntegrationTests.cs`.
  - Archivos Bruno: `CU07_CreateVeedor_400_DatosIncompletos.bru`, `CU07_CreateVeedor_409_Duplicado.bru`, `CreateVeedor.bru`, `UpdateVeedor.bru`, `DeactivateVeedor.bru`.

---

### CU-08 / CU-16: Solicitar y generar el fixture automático

* **Descripción:** Permite cerrar las inscripciones y generar matemáticamente los emparejamientos y fechas del torneo mediante algoritmos parametrizados (Round Robin o Eliminación Directa).
* **Actor Principal:** Organizador / Administrador.
* **Reglas de Negocio:**
  - **RN-08:** Al generarse y publicarse el fixture, se bloquean definitivamente las inscripciones y el torneo transiciona a `EstadoTorneo.EnDesarrollo`.
  - **RN-09:** Ningún equipo puede disputar más de un encuentro en el mismo día calendario.
  - Si el número de equipos en Liga es impar, se computa una jornada libre ("bye") por fecha.
  - Si en Eliminación Directa la cantidad no es potencia perfecta de 2, el algoritmo asigna pases directos ("byes") en la primera ronda.
  - Requiere un mínimo de equipos confirmados según la modalidad (mínimo 2 para Liga, mínimo 4 para Eliminación Directa).

#### Componentes de Implementación:
- **Controlador API:** [TorneosController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/TorneosController.cs) / [FixtureController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/FixtureController.cs)
  - Endpoints:
    - `POST /api/torneos/{id}/generar-fixture`
    - `POST /api/torneos/{torneoId}/fixture`
  - Códigos de Respuesta: `200 OK`, `201 Created`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `422 Unprocessable Entity`.
- **Capa BusinessLogic:**
  - Interfaz: [IFixtureService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IFixtureService.cs)
  - Servicio: [FixtureService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/FixtureService.cs)
- **Capa DataAccess:**
  - Repositorios: [IPartidoRepository.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Repositories/IPartidoRepository.cs), [ITorneoRepository.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Repositories/ITorneoRepository.cs).
  - Entidad: [Partido.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Partido.cs).
- **Contratos (Shared):**
  - DTOs: `GenerarFixtureRequestDTO`, `GenerarFixtureResponseDTO`, `FixtureParametrosDTO`, `FixtureResponseDTO`, `PartidoResponseDTO`.
  - Excepciones: `TorneoCupoMinimoNoAlcanzadoException` (409), `TorneoEstadoInvalidoException` (409).
- **Pruebas Automatizadas:**
  - Unitarias: `FixtureServiceTests.cs`.
  - Integración: `FixtureIntegrationTests.cs`, `FixtureAutomaticoIntegrationTests.cs`.
  - Archivos Bruno: `CU08_GenerarFixture_409_SinCupoMinimo.bru`, `CU16_GenerarFixtureAutomatico_201_Exitoso.bru`, `CU16_GenerarFixtureAutomatico_422_ConflictoHorario.bru`, `GenerarFixture.bru`.

---

### CU-09: Consultar el cronograma de partidos asignados

* **Descripción:** Permite a los veedores/árbitros consultar su grilla de encuentros asignados filtrando por fecha y torneo.
* **Actor Principal:** Veedor / Árbitro.
* **Reglas de Negocio:**
  - Un veedor únicamente tiene visibilidad sobre sus partidos asignados (a menos que el solicitante sea Administrador).
  - Los partidos se retornan ordenados cronológicamente con datos de cancha, equipos y estado.

#### Componentes de Implementación:
- **Controlador API:** [VeedoresController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/VeedoresController.cs)
  - Endpoint: `GET /api/veedores/{id}/partidos?fecha=...&torneoId=...`
  - Códigos de Respuesta: `200 OK`, `403 Forbidden`, `404 Not Found`.
- **Capa BusinessLogic:**
  - Servicio: [VeedorService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/VeedorService.cs) (`GetPartidosByVeedorAsync`)
- **Pruebas Automatizadas:**
  - Unitarias: `VeedorServiceTests.cs`.
  - Integración: `VeedoresIntegrationTests.cs`.
  - Archivos Bruno: `CU09_GetPartidosVeedor_200_Exitoso.bru`, `CU09_GetPartidosVeedor_403_Forbidden.bru`.

---

### CU-10: Cargar resultados básicos del partido

* **Descripción:** Permite al veedor u organizador cargar el marcador final (goles/puntos) de un partido, gestionando el avance automático de llaves en eliminación directa y la coronación del campeón.
* **Actor Principal:** Veedor / Organizador.
* **Reglas de Negocio:**
  - El partido debe encontrarse en estado `Pendiente` o `EnProgreso`.
  - Los marcadores deben ser números enteros no negativos (`>= 0`).
  - En torneos de Eliminación Directa no se permiten empates; debe existir un ganador registrado (mediante marcador o penales/desempate).
  - Al cargar el resultado, el estado del partido pasa a `Finalizado`.
  - **Avance de Rondas (RF-04):** En torneos de `Modalidad.EliminacionDirecta`, al completarse ambos partidos de un cruce en la ronda actual (ej. Semifinales), el sistema programa automáticamente el partido de la siguiente ronda (ej. Final) emparejando a los ganadores en estado `Pendiente`.
  - **Coronación y Cierre de Torneo:** Al finalizar el partido de la Final, el sistema proclama al ganador como Campeón (`torneo.CampeonId = ganadorId`), transiciona el torneo a `EstadoTorneo.Finalizado` y persiste la operación atómicamente con `IUnitOfWork`.

#### Componentes de Implementación:
- **Controlador API:** [PartidosController.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/PartidosController.cs)
  - Endpoint: `PUT /api/partidos/{id}/resultado`
  - Códigos de Respuesta: `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict`.
- **Capa BusinessLogic:**
  - Interfaz: [IPartidoService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IPartidoService.cs)
  - Servicio: [PartidoService.cs](file:///c:/PracticaProfe/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/PartidoService.cs)
- **Contratos (Shared):**
  - DTOs: `CargarResultadoDTO`, `PartidoResultadoResponseDTO`, `BracketTorneoDTO`, `TorneoResponseDTO`.
- **Pruebas Automatizadas:**
  - Unitarias: `PartidoServiceTests.cs`.
  - Integración: `PartidosIntegrationTests.cs`, `EliminacionDirectaAvanceLlavesIntegrationTests.cs`.
  - Archivos Bruno: `CU10_LoadResultado_200_Exitoso.bru`, `CU10_LoadResultado_400_MarcadorNegativo.bru`, `CU10_LoadResultado_409_EmpateEliminatoria.bru`.

---

### CU-11: Registrar incidencias del partido

* **Descripción:** Permite a la mesa de control registrar eventos en tiempo real: goles, puntos, tarjetas amarillas/rojas y faltas asociadas a jugadores.
* **Actor Principal:** Veedor / Árbitro.
* **Reglas de Negocio:**
  - El partido debe estar habilitado para carga (`EnProgreso` o `Pendiente`).
  - **RN-07:** Si un jugador recibe dos tarjetas amarillas o una tarjeta roja directa, el sistema cambia automáticamente su estado a expulsado, impidiendo registrarle más incidencias en dicho encuentro.
  - El minuto del evento debe encontrarse en el rango válido del encuentro.

#### Componentes de Implementación:
- **Controlador API:** [IncidenciasController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/IncidenciasController.cs)
  - Endpoint: `POST /api/partidos/{partidoId}/incidencias`
  - Códigos de Respuesta: `201 Created`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `422 Unprocessable Entity`.
- **Capa BusinessLogic:**
  - Interfaz: [IIncidenciaService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IIncidenciaService.cs)
  - Servicio: [IncidenciaService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IncidenciaService.cs)
- **Capa DataAccess:**
  - Entidad: [Incidencia.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Incidencia.cs).
- **Contratos (Shared):**
  - DTOs: `IncidenciaCreateDTO`, `IncidenciaResponseDTO`.
- **Pruebas Automatizadas:**
  - Unitarias: `IncidenciaServiceTests.cs`.
  - Integración: `IncidenciasIntegrationTests.cs`.
  - Archivos Bruno: `CU11_RegistrarIncidencia_201_GolExitoso.bru`, `CU11_RegistrarIncidencia_409_JugadorExpulsado.bru`, `CU11_RegistrarIncidencia_422_JugadorNoPertenece.bru`.

---

### CU-12: Consultar el fixture y calendario de partidos del torneo

* **Descripción:** Endpoint público de consulta que devuelve todos los enfrentamientos del torneo agrupados por jornada y fase.
* **Actor Principal:** Público General (Capitanes, Jugadores, Espectadores).
* **Reglas de Negocio:**
  - Operación idempotente de solo lectura.
  - Si el fixture no ha sido generado, devuelve colección vacía con mensaje descriptivo.

#### Componentes de Implementación:
- **Controlador API:** [TorneosController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/TorneosController.cs)
  - Endpoint: `GET /api/torneos/{id}/fixture`
  - Códigos de Respuesta: `200 OK`, `404 Not Found`.
- **Capa BusinessLogic:**
  - Servicio: [FixtureService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/FixtureService.cs) (`GetFixtureAsync`)
- **Pruebas Automatizadas:**
  - Unitarias: `FixtureServiceTests.cs`.
  - Integración: `FixtureIntegrationTests.cs`.
  - Archivos Bruno: `CU12_GetFixture_200_Exitoso.bru`, `CU12_GetFixture_404_TorneoInexistente.bru`.

---

### CU-13: Consultar tablas de posiciones y llaves de competencia en tiempo real

* **Descripción:** Computa y expone la tabla clasificatoria (Puntos, PJ, PG, PE, PP, GF, GC, Dif) o el árbol de llaves (Brackets) según la modalidad del certamen.
* **Actor Principal:** Público General.
* **Reglas de Negocio:**
  - Modalidad Liga: Cálculo dinámico de puntos (3 victoria, 1 empate, 0 derrota) y diferencia de goles/puntos.
  - Modalidad Eliminación Directa: Estructuración de cruces por rondas (Octavos, Cuartos, Semifinal, Final).

#### Componentes de Implementación:
- **Controlador API:** [TorneosController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/TorneosController.cs)
  - Endpoints:
    - `GET /api/torneos/{id}/posiciones`
    - `GET /api/torneos/{id}/llaves`
  - Códigos de Respuesta: `200 OK`, `400 Bad Request` (si se solicita tabla en torneo de eliminación directa), `404 Not Found`.
- **Capa BusinessLogic:**
  - Servicio: [TorneoService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/TorneoService.cs)
- **Contratos (Shared):**
  - DTOs: `PosicionTablaDTO`, `LlaveTorneoDTO`.
- **Pruebas Automatizadas:**
  - Unitarias: `TorneoServiceTests.cs`.
  - Integración: `TorneosIntegrationTests.cs`.
  - Archivos Bruno: `CU13_GetPosiciones_200_LigaExitoso.bru`, `CU13_GetLlaves_200_BracketsExitoso.bru`, `CU13_GetPosiciones_400_ModalidadIncompatible.bru`.

---

### CU-14: Consultar perfiles estadísticos e historial (Jugador / Equipo)

* **Descripción:** Provee el currículum deportivo y estadísticas consolidadas tanto a nivel individual de jugadores como a nivel colectivo de equipos.
* **Actor Principal:** Público General.
* **Reglas de Negocio:**
  - Métricas de Jugador: Goles/puntos totales, tarjetas amarillas, rojas y partidos disputados.
  - Métricas de Equipo: Partidos jugados, victorias, empates, derrotas y porcentaje de efectividad.

#### Componentes de Implementación:
- **Controladores API:**
  - [JugadoresController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/JugadoresController.cs) (`GET /api/jugadores/{id}/estadisticas`)
  - [EquiposController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/EquiposController.cs) (`GET /api/equipos/{id}/estadisticas`)
  - Códigos de Respuesta: `200 OK`, `404 Not Found`.
- **Capa BusinessLogic:**
  - Servicios: [JugadorService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/JugadorService.cs), [EquipoService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/EquipoService.cs).
- **Contratos (Shared):**
  - DTOs: `EstadisticasJugadorDTO`, `EstadisticasEquipoDTO`.
- **Pruebas Automatizadas:**
  - Unitarias: `JugadorServiceTests.cs`, `EquipoServiceTests.cs`.
  - Integración: `JugadoresIntegrationTests.cs`, `EquiposIntegrationTests.cs`.
  - Archivos Bruno: `CU14_GetEstadisticasJugador_200_Exitoso.bru`, `CU14_GetEstadisticasEquipo_200_Exitoso.bru`.

---

### CU-15: Visualizar espacios publicitarios y logos de patrocinadores

* **Descripción:** Gestiona la exposición de banners y marcas en las secciones públicas y registra la telemetría de clics/impresiones para los sponsors.
* **Actor Principal:** Usuario Espectador / Patrocinadores.
* **Reglas de Negocio:**
  - Los banners activos se vinculan a un torneo o a nivel global.
  - Cada clic en una publicidad incrementa de forma atómica su contador (`Clicks++`) y retorna la URL destino de redirección.

#### Componentes de Implementación:
- **Controlador API:** [PublicidadController.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/API/Controllers/PublicidadController.cs)
  - Endpoints:
    - `GET /api/torneos/{id}/publicidad`
    - `POST /api/publicidad/{bannerId}/click`
  - Códigos de Respuesta: `200 OK`, `404 Not Found`.
- **Capa BusinessLogic:**
  - Interfaz: [IPublicidadService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/IPublicidadService.cs)
  - Servicio: [PublicidadService.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/BusinessLogic/Services/PublicidadService.cs)
- **Capa DataAccess:**
  - Entidad: [Publicidad.cs](file:///e:/2do%20a%C3%B1o%20Desarrollo%20de%20software/Practica%20Profesionalizante%20I/Grupo-N-11-PlataformaDeTorneosDeportivos/DataAccess/Entities/Publicidad.cs).
- **Contratos (Shared):**
  - DTOs: `PublicidadResponseDTO`, `ClickPublicidadResponseDTO`.
- **Pruebas Automatizadas:**
  - Unitarias: `PublicidadServiceTests.cs`.
  - Integración: `PublicidadIntegrationTests.cs`.
  - Archivos Bruno: `CU15_GetPublicidad_200_Exitoso.bru`, `CU15_PostPublicidadClick_200_Exitoso.bru`.

---

## 4. Matriz Resumen de Trazabilidad End-to-End

| CU | Nombre Oficial | Endpoint HTTP | Servicio (`BusinessLogic`) | Repositorio / Entidad | Excepciones Clave | Prueba Bruno (`bruno/`) |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| **01** | Registrarse en la plataforma | `POST /api/usuarios/registro` | `UsuarioService` | `IUsuarioRepository` / `Usuario` | `ValidationException` (400), `BusinessRuleConflictException` (409) | `CU01_Registro_409_Duplicado.bru` |
| **02** | Iniciar sesión | `POST /api/auth/login` | `AuthService` | `IUsuarioRepository` / `Usuario` | `UnauthorizedException` (401), `ForbiddenException` (403) | `CU02_Login_401_CredencialesIncorrectas.bru` |
| **03** | Recuperar contraseña | `POST /api/auth/recuperar-password`<br>`POST /api/auth/reset-password` | `AuthService` | `IUsuarioRepository` / `Usuario` | `ValidationException` (400) | `CU03_ResetPassword_400_TokenExpirado.bru` |
| **04** | Crear y configurar torneo | `POST /api/torneos` | `TorneoService` | `ITorneoRepository` / `Torneo` | `TorneoNombreDuplicadoException` (409), `CupoInvalidoEliminacionException` (422) | `CU04_CreateTorneo_422_CupoEliminacionInvalido.bru` |
| **05** | Alta de equipos y jugadores | `POST /api/torneos/{id}/equipos` | `EquipoService` | `IEquipoRepository`, `IJugadorRepository` | `JugadorYaInscriptoException` (409), `MinimoJugadoresInsuficienteException` (422) | `CU05_CreateEquipo_409_JugadorYaInscripto.bru` |
| **06** | Dictaminar inscripciones | `PATCH /api/.../aprobar`<br>`PATCH /api/.../rechazar` | `InscripcionService` | `ITorneoRepository`, `IEquipoRepository` | `TorneoCupoLlenoException` (409), `PagoRequeridoException` (422) | `CU06_AprobarInscripcion_422_SinPago.bru` |
| **07** | Gestionar veedores | `POST /api/veedores`<br>`PUT /api/veedores/{id}` | `VeedorService` | `IUsuarioRepository` / `Usuario` | `ValidationException` (400), `NotFoundException` (404) | `CU07_CreateVeedor_409_Duplicado.bru` |
| **08** | Solicitar generación fixture | `POST /api/torneos/{id}/generar-fixture` | `FixtureService` | `ITorneoRepository`, `IPartidoRepository` | `TorneoCupoMinimoNoAlcanzadoException` (409) | `CU08_GenerarFixture_409_SinCupoMinimo.bru` |
| **09** | Cronograma de partidos | `GET /api/veedores/{id}/partidos` | `VeedorService` | `IPartidoRepository` / `Partido` | `ForbiddenException` (403), `NotFoundException` (404) | `CU09_GetPartidosVeedor_200_Exitoso.bru` |
| **10** | Cargar resultados de partido | `PUT /api/partidos/{id}/resultado` | `PartidoService` | `IPartidoRepository` / `Partido` | `ValidationException` (400), `BusinessRuleConflictException` (409) | `CU10_LoadResultado_409_EmpateEliminatoria.bru` |
| **11** | Registrar incidencias | `POST /api/partidos/{id}/incidencias` | `IncidenciaService` | `IPartidoRepository`, `Incidencia` | `BusinessRuleConflictException` (409), `ValidationException` (400) | `CU11_RegistrarIncidencia_409_JugadorExpulsado.bru` |
| **12** | Consultar fixture y calendario | `GET /api/torneos/{id}/fixture` | `FixtureService` | `IPartidoRepository` / `Partido` | `NotFoundException` (404) | `CU12_GetFixture_200_Exitoso.bru` |
| **13** | Tablas de posiciones y llaves | `GET /api/torneos/{id}/posiciones`<br>`GET /api/torneos/{id}/llaves` | `TorneoService` | `ITorneoRepository`, `IPartidoRepository` | `ValidationException` (400), `NotFoundException` (404) | `CU13_GetPosiciones_200_LigaExitoso.bru` |
| **14** | Estadísticas de jugador/equipo | `GET /api/jugadores/{id}/estadisticas`<br>`GET /api/equipos/{id}/estadisticas` | `JugadorService`<br>`EquipoService` | `IJugadorRepository`, `IEquipoRepository` | `NotFoundException` (404) | `CU14_GetEstadisticasJugador_200_Exitoso.bru` |
| **15** | Banners y publicidad | `GET /api/torneos/{id}/publicidad`<br>`POST /api/publicidad/{id}/click` | `PublicidadService` | `AppDbContext` / `Publicidad` | `NotFoundException` (404) | `CU15_GetPublicidad_200_Exitoso.bru` |
| **16** | Algoritmo de fixture automático | `POST /api/torneos/{id}/fixture` | `FixtureService` | `ITorneoRepository`, `IPartidoRepository` | `ValidationException` (400), `BusinessRuleConflictException` (409) | `CU16_GenerarFixtureAutomatico_201_Exitoso.bru` |

---

## 5. Estado y Cobertura de las Pruebas

Toda la solución se valida con una suite integral de **497 pruebas automatizadas** que corren en integración continua:
- **`BusinessLogic.Tests`**: 197 pruebas unitarias completas bajo el patrón Arrange-Act-Assert con `Moq`, incluyendo auditoría y fuzzing adversarial (`RedTeamFuzzingServiceTests.cs`), propagación de concurrencia optimista (`InscripcionServiceTests.cs`), validación de RN-03/CU-05 en listas de jugadores y mitigación IDOR con titularidad (`JugadorServiceTests.cs`), comportamiento transaccional ACID con Rollback (`FixtureServiceTests.cs` e `InscripcionServiceTests.cs`), titularidad de equipos y protección PII de jugadores RNF-04 (`EquipoServiceTests.cs`) y prevención de escalada de privilegios CU-01 / RNF-04 (`UsuarioServiceTests.cs`).
- **`API.IntegrationTests`**: 300 pruebas de integración con servidor en memoria `CustomWebApplicationFactory<Program>`, SQLite y deserialización JSON, incluyendo batería adversarial (`AdversarialCasosDeUsoIntegrationTests.cs`), validaciones exhaustivas de entrada (`ValidacionesExhaustivasIntegrationTests.cs`), control de concurrencia optimista (`TorneoConcurrenciaOptimistaIntegrationTests.cs`), control de lista de buena fe y bloqueo de vectores IDOR en modificación/eliminación (`JugadoresIntegrationTests.cs`), transacciones atómicas reales sobre SQLite (`TransaccionesAcidIntegrationTests.cs`), acreditación legítima de pagos CU-06 (`InscripcionesIntegrationTests.cs`), privacidad PII RNF-04 (`EquiposIntegrationTests.cs`) y neutralización de escalada de privilegios en endpoints públicos y protegidos CU-01 (`UsuariosIntegrationTests.cs`).
- **Colección Bruno (`bruno/`)**: 74 archivos de pruebas `.bru` que cubren escenarios exitosos (200, 201) y todas las ramas de error (400, 401, 403, 404, 409, 422).
