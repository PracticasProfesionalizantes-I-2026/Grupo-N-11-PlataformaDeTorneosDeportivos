using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DataAccess.Repositories;
using Shared.Exceptions;

namespace BusinessLogic.Common
{
    public static class UsuarioValidationHelper
    {
        private static readonly Regex NombrePersonaRegex = new(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", RegexOptions.Compiled);
        private static readonly Regex TelefonoRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PasswordRegex = new(@"^(?=.*[a-zA-Z])(?=.*\d).+$", RegexOptions.Compiled);
        private static readonly Regex SedeRegex = new(@"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$", RegexOptions.Compiled);

        public static string ValidateAndNormalizeDni(string? dni)
        {
            var normalizedDni = dni?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDni) || !normalizedDni.All(c => c >= '0' && c <= '9'))
            {
                throw new ValidationException("El DNI debe contener solo números.");
            }
            if (normalizedDni.Length < 7 || normalizedDni.Length > 8)
            {
                throw new ValidationException("El DNI debe tener entre 7 y 8 dígitos numéricos.");
            }
            return normalizedDni;
        }

        public static string ValidateAndNormalizeNombre(string? nombre, string campo = "nombre")
        {
            var nombreTrim = nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 2 || nombreTrim.Length > 60 || !NombrePersonaRegex.IsMatch(nombreTrim))
            {
                throw new ValidationException($"El {campo} debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }
            return nombreTrim;
        }

        public static string ValidateAndNormalizeApellido(string? apellido, string campo = "apellido")
        {
            var apellidoTrim = apellido?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apellidoTrim) || apellidoTrim.Length < 2 || apellidoTrim.Length > 60 || !NombrePersonaRegex.IsMatch(apellidoTrim))
            {
                throw new ValidationException($"El {campo} debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }
            return apellidoTrim;
        }

        public static string ValidateAndNormalizeEmail(string? email)
        {
            var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedEmail.Length > 150 ||
                normalizedEmail.Contains('<') || normalizedEmail.Contains('>') || !EmailRegex.IsMatch(normalizedEmail))
            {
                throw new ValidationException("El formato del correo electrónico no es válido.");
            }
            return normalizedEmail;
        }

        public static string? ValidateAndNormalizeTelefono(string? telefono, string campo = "teléfono")
        {
            if (telefono == null) return null;
            var telTrim = telefono.Trim();
            if (string.IsNullOrWhiteSpace(telTrim) || !TelefonoRegex.IsMatch(telTrim))
            {
                throw new ValidationException($"El {campo} debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
            }
            return telTrim;
        }

        public static void ValidatePassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128 ||
                !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                throw new ValidationException("La contraseña debe tener entre 8 y 128 caracteres e incluir letras y números.");
            }
        }

        public static string ValidatePasswordInicial(string? passwordInicial)
        {
            if (!string.IsNullOrWhiteSpace(passwordInicial))
            {
                var pwd = passwordInicial.Trim();
                if (pwd.Length < 8 || pwd.Length > 128 || !PasswordRegex.IsMatch(pwd))
                {
                    throw new ValidationException("La contraseña inicial debe tener entre 8 y 128 caracteres y contener al menos una letra y un número.");
                }
                return pwd;
            }
            return $"Veedor_{Guid.NewGuid():N}".Substring(0, 10) + "1A!";
        }

        public static string? ValidateSedeAsignada(string? sede)
        {
            if (sede == null) return null;
            var sedeTrim = sede.Trim();
            if (string.IsNullOrWhiteSpace(sedeTrim) || sedeTrim.Length < 2 || sedeTrim.Length > 100 ||
                sedeTrim.Contains('<') || sedeTrim.Contains('>') ||
                !SedeRegex.IsMatch(sedeTrim))
            {
                throw new ValidationException("La sede asignada debe tener entre 2 y 100 caracteres válidos.");
            }
            return sedeTrim;
        }

        public static void ValidateTorneosAsignados(List<Guid>? torneos)
        {
            if (torneos == null) return;
            if (torneos.Count > 50)
            {
                throw new ValidationException("La lista de torneos asignados no puede superar los 50 torneos.");
            }
            if (torneos.Any(t => t == Guid.Empty))
            {
                throw new ValidationException("La lista de torneos asignados no puede contener identificadores vacíos.");
            }
            if (torneos.Distinct().Count() != torneos.Count)
            {
                throw new ValidationException("La lista de torneos asignados no puede contener identificadores duplicados.");
            }
        }

        public static async Task CheckUniquenessAsync(IUsuarioRepository repo, string email, string dni, string? customErrorMessage = null)
        {
            var usuarioPorEmail = await repo.GetByEmailAsync(email);
            if (usuarioPorEmail != null)
            {
                if (!string.IsNullOrEmpty(customErrorMessage))
                    throw new BusinessRuleConflictException(customErrorMessage);
                throw new UsuarioDuplicadoException();
            }

            var usuarioPorDni = await repo.GetByDniAsync(dni);
            if (usuarioPorDni != null)
            {
                if (!string.IsNullOrEmpty(customErrorMessage))
                    throw new BusinessRuleConflictException(customErrorMessage);
                throw new UsuarioDuplicadoException();
            }
        }
    }
}

