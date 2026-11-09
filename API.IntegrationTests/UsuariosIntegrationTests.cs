using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    public class UsuariosIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;

        public UsuariosIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task RegisterUsuario_ReturnsSuccessAndCreated()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Agustin",
                Apellido = "Rossi",
                Dni = "55667788",
                Email = "agustin.rossi@torneos.com",
                Telefono = "1122334455",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<UsuarioResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal("Agustin", result.Nombre);
            Assert.Equal("agustin.rossi@torneos.com", result.Email);
            Assert.Equal("Capitan", result.Rol);
            Assert.True(result.Activo);
        }

        [Fact]
        public async Task RegisterUsuario_WithInvalidPassword_Returns400BadRequest()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Test",
                Apellido = "Inseguro",
                Dni = "99887766",
                Email = "inseguro@torneos.com",
                Password = "123" // Contraseña corta e insegura
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RegisterUsuario_WhenDuplicateData_Returns409Conflict()
        {
            // Arrange - Primer registro exitoso
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Original",
                Apellido = "Usuario",
                Dni = "11221122",
                Email = "original@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Organizador
            };

            var firstResponse = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            // Act - Intento de registro con el mismo DNI y Email
            var duplicateResponse = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        }

        [Fact]
        public async Task RegisterUsuario_WithLettersInDni_Returns400BadRequest()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Test",
                Apellido = "Invalido",
                Dni = "1234567A",
                Email = "test.dniinvalido@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #region Prevención de Escalada de Privilegios (CU-01 / RNF-04)

        [Fact]
        public async Task CU01_AntiEscaladaPrivilegios_RegistroPublico_ConRolAdministradorUOrganizador_FuerzaRolCapitan()
        {
            // Arrange 1: Atacante intenta registrarse públicamente en /api/usuarios/registro inyectando Rol = Administrador
            var adminPayload = new UsuarioRegistroDTO
            {
                Nombre = "Atacante",
                Apellido = "Admin",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"atacante_admin_{Guid.NewGuid():N}@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Administrador
            };

            // Act 1: POST /api/usuarios/registro
            var res1 = await _client.PostAsJsonAsync("/api/usuarios/registro", adminPayload);
            Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

            var created1 = await res1.Content.ReadFromJsonAsync<UsuarioResponseDTO>();
            Assert.NotNull(created1);
            Assert.Equal("Capitan", created1.Rol); // Neutralizado: NUNCA Administrador

            // Arrange 2: Atacante intenta registrarse públicamente en /api/auth/register inyectando Rol = Organizador
            var orgPayload = new UsuarioRegistroDTO
            {
                Nombre = "Atacante",
                Apellido = "Organizador",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"atacante_org_{Guid.NewGuid():N}@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Organizador
            };

            // Act 2: POST /api/auth/register
            var res2 = await _client.PostAsJsonAsync("/api/auth/register", orgPayload);
            Assert.Equal(HttpStatusCode.Created, res2.StatusCode);

            var created2 = await res2.Content.ReadFromJsonAsync<UsuarioResponseDTO>();
            Assert.NotNull(created2);
            Assert.Equal("Capitan", created2.Rol); // Neutralizado: NUNCA Organizador

            // Act 3: El atacante inicia sesión e intenta usar su token para realizar una acción administrativa
            var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDTO
            {
                EmailOrDni = adminPayload.Email,
                Password = adminPayload.Password
            });
            Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

            var loginData = await loginRes.Content.ReadFromJsonAsync<LoginResponseDTO>();
            Assert.NotNull(loginData);
            Assert.Equal("Capitan", loginData.Rol);

            // Intenta invocar endpoint administrativo de veedores con su token
            var reqAdmin = new HttpRequestMessage(HttpMethod.Post, "/api/veedores")
            {
                Content = JsonContent.Create(new VeedorCreateDTO
                {
                    Nombre = "Nuevo",
                    Apellido = "Veedor",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    Email = $"veedor_{Guid.NewGuid():N}@torneos.com"
                })
            };
            reqAdmin.Headers.Add("Authorization", $"Bearer {loginData.Token}");

            var resAction = await _client.SendAsync(reqAdmin);
            Assert.Equal(HttpStatusCode.Forbidden, resAction.StatusCode); // 403 Forbidden: Sin acceso administrativo
        }

        [Fact]
        public async Task CU01_BloqueoAsignacionRolElevado_CapitanOAnonimo_Retorna401O403()
        {
            var adminPayload = new UsuarioRegistroDTO
            {
                Nombre = "Intento",
                Apellido = "Elevado",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"elevado_{Guid.NewGuid():N}@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Administrador
            };

            // 1. Usuario anónimo intenta acceder a /api/usuarios/admin -> 401 Unauthorized
            var reqAnon = new HttpRequestMessage(HttpMethod.Post, "/api/usuarios/admin")
            {
                Content = JsonContent.Create(adminPayload)
            };
            reqAnon.Headers.Add("X-Test-Anonymous", "true");

            var resAnon = await _client.SendAsync(reqAnon);
            Assert.Equal(HttpStatusCode.Unauthorized, resAnon.StatusCode);

            // 2. Usuario con rol Capitan intenta acceder a /api/usuarios/admin -> 403 Forbidden
            var reqCapitan = new HttpRequestMessage(HttpMethod.Post, "/api/usuarios/admin")
            {
                Content = JsonContent.Create(adminPayload)
            };
            reqCapitan.Headers.Add("X-Test-Role", "Capitan");

            var resCapitan = await _client.SendAsync(reqCapitan);
            Assert.Equal(HttpStatusCode.Forbidden, resCapitan.StatusCode);

            // 3. Usuario con rol Capitan intenta ascender rol mediante PUT /api/usuarios/{id}/rol -> 403 Forbidden
            var reqPutRol = new HttpRequestMessage(HttpMethod.Put, $"/api/usuarios/{Guid.NewGuid()}/rol")
            {
                Content = JsonContent.Create(new CambiarRolDTO { NuevoRol = RolUsuario.Administrador })
            };
            reqPutRol.Headers.Add("X-Test-Role", "Capitan");

            var resPutRol = await _client.SendAsync(reqPutRol);
            Assert.Equal(HttpStatusCode.Forbidden, resPutRol.StatusCode);
        }

        [Fact]
        public async Task CU01_AsignacionLegitimaRolElevado_Administrador_Retorna201Y200()
        {
            // Arrange: Usuario con rol Administrador crea un Organizador en el endpoint administrativo protegido
            var orgPayload = new UsuarioRegistroDTO
            {
                Nombre = "Legitimo",
                Apellido = "Organizador",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"legitimo_org_{Guid.NewGuid():N}@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Organizador
            };

            var reqCreate = new HttpRequestMessage(HttpMethod.Post, "/api/usuarios/admin")
            {
                Content = JsonContent.Create(orgPayload)
            };
            reqCreate.Headers.Add("X-Test-Role", "Administrador");

            // Act 1: POST /api/usuarios/admin con rol Administrador
            var resCreate = await _client.SendAsync(reqCreate);
            Assert.Equal(HttpStatusCode.Created, resCreate.StatusCode);

            var createdUser = await resCreate.Content.ReadFromJsonAsync<UsuarioResponseDTO>();
            Assert.NotNull(createdUser);
            Assert.Equal("Organizador", createdUser.Rol);

            // Act 2: Administrador actualiza el rol del usuario a Administrador vía PUT /api/usuarios/{id}/rol
            var reqPut = new HttpRequestMessage(HttpMethod.Put, $"/api/usuarios/{createdUser.Id}/rol")
            {
                Content = JsonContent.Create(new CambiarRolDTO { NuevoRol = RolUsuario.Administrador })
            };
            reqPut.Headers.Add("X-Test-Role", "Administrador");

            var resPut = await _client.SendAsync(reqPut);
            Assert.Equal(HttpStatusCode.OK, resPut.StatusCode);

            var updatedUser = await resPut.Content.ReadFromJsonAsync<UsuarioResponseDTO>();
            Assert.NotNull(updatedUser);
            Assert.Equal("Administrador", updatedUser.Rol);
        }

        #endregion
    }
}
