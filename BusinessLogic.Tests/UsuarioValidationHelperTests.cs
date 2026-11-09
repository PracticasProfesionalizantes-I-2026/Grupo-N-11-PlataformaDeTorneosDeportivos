using System;
using System.Threading.Tasks;
using BusinessLogic.Common;
using DataAccess.Entities;
using DataAccess.Repositories;
using Moq;
using Shared.Enums;
using Shared.Exceptions;
using Xunit;

namespace BusinessLogic.Tests
{
    public class UsuarioValidationHelperTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ValidateAndNormalizeDni_EmptyOrNull_ThrowsValidationException(string? dni)
        {
            Assert.Throws<ValidationException>(() => UsuarioValidationHelper.ValidateAndNormalizeDni(dni!));
        }

        [Theory]
        [InlineData("123456")]     // 6 chars
        [InlineData("123456789")]   // 9 chars
        [InlineData("1234567A")]   // letters
        [InlineData("12-34567")]   // symbols
        public void ValidateAndNormalizeDni_InvalidFormat_ThrowsValidationException(string dni)
        {
            Assert.Throws<ValidationException>(() => UsuarioValidationHelper.ValidateAndNormalizeDni(dni));
        }

        [Fact]
        public void ValidateAndNormalizeDni_ValidDni_ReturnsTrimmed()
        {
            var result = UsuarioValidationHelper.ValidateAndNormalizeDni("  12345678  ");
            Assert.Equal("12345678", result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("Juan123")]
        [InlineData("<script>")]
        public void ValidateAndNormalizeNombre_Invalid_ThrowsValidationException(string nombre)
        {
            Assert.Throws<ValidationException>(() => UsuarioValidationHelper.ValidateAndNormalizeNombre(nombre));
        }

        [Fact]
        public void ValidateAndNormalizeNombre_Valid_ReturnsTrimmed()
        {
            var result = UsuarioValidationHelper.ValidateAndNormalizeNombre("  Carlos  ");
            Assert.Equal("Carlos", result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("correoInvalido")]
        [InlineData("test@")]
        [InlineData("@test.com")]
        public void ValidateAndNormalizeEmail_Invalid_ThrowsValidationException(string email)
        {
            Assert.Throws<ValidationException>(() => UsuarioValidationHelper.ValidateAndNormalizeEmail(email));
        }

        [Fact]
        public void ValidateAndNormalizeEmail_Valid_ReturnsTrimmedLower()
        {
            var result = UsuarioValidationHelper.ValidateAndNormalizeEmail("  Test.User@Domain.COM  ");
            Assert.Equal("test.user@domain.com", result);
        }

        [Theory]
        [InlineData("12345")]       // < 6 chars
        [InlineData("sololetras")]  // sin numero
        [InlineData("12345678")]    // sin letra
        public void ValidatePassword_Invalid_ThrowsValidationException(string password)
        {
            Assert.Throws<ValidationException>(() => UsuarioValidationHelper.ValidatePassword(password));
        }

        [Fact]
        public void ValidatePassword_Valid_DoesNotThrow()
        {
            UsuarioValidationHelper.ValidatePassword("ClaveSegura123");
        }

        [Fact]
        public async Task CheckUniquenessAsync_WhenDniExists_ThrowsUsuarioDuplicadoException()
        {
            var existingDniUser = new Usuario { Id = Guid.NewGuid(), Dni = "12345678", Email = "other@domain.com" };
            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync("new@domain.com")).ReturnsAsync((Usuario?)null);
            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync("12345678")).ReturnsAsync(existingDniUser);

            await Assert.ThrowsAsync<UsuarioDuplicadoException>(() =>
                UsuarioValidationHelper.CheckUniquenessAsync(_usuarioRepositoryMock.Object, "new@domain.com", "12345678"));
        }

        [Fact]
        public async Task CheckUniquenessAsync_WhenEmailExists_ThrowsUsuarioDuplicadoException()
        {
            var existingEmailUser = new Usuario { Id = Guid.NewGuid(), Dni = "87654321", Email = "existing@domain.com" };
            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync("existing@domain.com")).ReturnsAsync(existingEmailUser);
            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync("12345678")).ReturnsAsync((Usuario?)null);

            await Assert.ThrowsAsync<UsuarioDuplicadoException>(() =>
                UsuarioValidationHelper.CheckUniquenessAsync(_usuarioRepositoryMock.Object, "existing@domain.com", "12345678"));
        }

        [Fact]
        public async Task CheckUniquenessAsync_WhenDniAndEmailAreUnique_CompletesSuccessfully()
        {
            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync(It.IsAny<string>())).ReturnsAsync((Usuario?)null);
            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Usuario?)null);

            await UsuarioValidationHelper.CheckUniquenessAsync(_usuarioRepositoryMock.Object, "free@domain.com", "12345678");
        }
    }
}
