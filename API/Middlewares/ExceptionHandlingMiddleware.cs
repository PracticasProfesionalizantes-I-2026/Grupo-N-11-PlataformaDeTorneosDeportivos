using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Shared.Exceptions;

namespace API.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
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

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            HttpStatusCode code;
            string message = exception.Message;

            switch (exception)
            {
                case NotFoundException _:
                    code = HttpStatusCode.NotFound; // 404
                    break;
                case ValidationException _:
                    code = HttpStatusCode.BadRequest; // 400
                    break;
                case BusinessRuleConflictException _:
                    code = HttpStatusCode.Conflict; // 409
                    break;
                default:
                    code = HttpStatusCode.InternalServerError; // 500
                    message = "Ha ocurrido un error inesperado en el servidor.";
                    break;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;

            var result = JsonSerializer.Serialize(new { error = message });
            return context.Response.WriteAsync(result);
        }
    }
}
