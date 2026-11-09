using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace API.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment? _environment;
        private readonly ILogger<ExceptionHandlingMiddleware>? _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            IHostEnvironment? environment = null,
            ILogger<ExceptionHandlingMiddleware>? logger = null)
        {
            _next = next;
            _environment = environment;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            HttpStatusCode code;
            string message = exception.Message;

            switch (exception)
            {
                case CupoInvalidoEliminacionException _:
                case CuentaRecaudadoraInactivaException _:
                case EquiposInsuficientesException _:
                case DisponibilidadHorariaInsuficienteException _:
                    code = HttpStatusCode.UnprocessableEntity; // 422
                    break;
                case NotFoundException _:
                    code = HttpStatusCode.NotFound; // 404
                    break;
                case ValidationException _:
                case JsonException _:
                case BadHttpRequestException _:
                    code = HttpStatusCode.BadRequest; // 400
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = "El formato de la petición es inválido.";
                    }
                    break;
                case DbUpdateConcurrencyException _:
                    code = HttpStatusCode.Conflict; // 409
                    _logger?.LogWarning(exception, "Conflicto de concurrencia detectado al procesar {Method} {Path}.", context.Request?.Method, context.Request?.Path);
                    message = "El registro fue modificado simultáneamente por otro usuario. Por favor, reintente la operación.";
                    break;
                case UsuarioDuplicadoException _:
                case PartidoEstadoInvalidoException _:
                case EmpateNoPermitidoException _:
                case BusinessRuleConflictException _:
                    code = HttpStatusCode.Conflict; // 409
                    break;
                case UnauthorizedException _:
                    code = HttpStatusCode.Unauthorized; // 401
                    break;
                case ForbiddenException _:
                    code = HttpStatusCode.Forbidden; // 403
                    break;
                case MinimoJugadoresInsuficienteException _:
                case PagoRequeridoException _:
                case JugadorNoAsignadoException _:
                    code = HttpStatusCode.UnprocessableEntity; // 422
                    break;
                default:
                    code = HttpStatusCode.InternalServerError; // 500
                    _logger?.LogError(exception, "Error no controlado procesando la solicitud {Method} {Path}.", context.Request?.Method, context.Request?.Path);
                    
                    // En entornos que no sean Development (ej. Staging, Production), prevenir fugas de información
                    if (_environment != null && !_environment.IsDevelopment())
                    {
                        message = "Ocurrió un error interno en el servidor.";
                    }
                    else if (_environment != null && _environment.IsDevelopment())
                    {
                        message = string.IsNullOrWhiteSpace(exception.Message) ? "Ocurrió un error interno en el servidor." : exception.Message;
                    }
                    else
                    {
                        message = "Ocurrió un error interno en el servidor.";
                    }
                    break;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;

            var result = JsonSerializer.Serialize(new { error = message });
            return context.Response.WriteAsync(result);
        }
    }
}
