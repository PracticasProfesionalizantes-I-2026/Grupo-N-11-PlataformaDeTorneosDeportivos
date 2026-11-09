using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using API.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;
using Xunit;

namespace API.IntegrationTests
{
    public class ExceptionHandlingMiddlewareTests
    {
        private class FakeHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = "API";
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }

        private static DefaultHttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static async Task<string> ReadResponseBodyAsync(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            return await reader.ReadToEndAsync();
        }

        [Fact]
        public async Task Production_UnhandledException_Returns500WithoutLeakingDetails()
        {
            var fakeEnv = new FakeHostEnvironment { EnvironmentName = Environments.Production };

            var middleware = new ExceptionHandlingMiddleware(
                next: (innerContext) => throw new InvalidOperationException("Sensitive DB connection string: Server=secret;Password=123"),
                environment: fakeEnv,
                logger: null);

            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            var responseJson = await ReadResponseBodyAsync(context);
            var doc = JsonDocument.Parse(responseJson);
            var error = doc.RootElement.GetProperty("error").GetString();

            Assert.Equal("Ocurrió un error interno en el servidor.", error);
            Assert.DoesNotContain("Sensitive DB", responseJson);
            Assert.DoesNotContain("Password=123", responseJson);
        }

        [Fact]
        public async Task Development_UnhandledException_Returns500WithErrorMessage()
        {
            var fakeEnv = new FakeHostEnvironment { EnvironmentName = Environments.Development };

            var middleware = new ExceptionHandlingMiddleware(
                next: (innerContext) => throw new InvalidOperationException("Detailed dev error description"),
                environment: fakeEnv,
                logger: null);

            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            var responseJson = await ReadResponseBodyAsync(context);
            var doc = JsonDocument.Parse(responseJson);
            var error = doc.RootElement.GetProperty("error").GetString();

            Assert.Equal("Detailed dev error description", error);
        }

        [Fact]
        public async Task DomainException_NotFound_Returns404()
        {
            var middleware = new ExceptionHandlingMiddleware(
                next: (innerContext) => throw new NotFoundException("Torneo no encontrado"),
                environment: null,
                logger: null);

            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
            var responseJson = await ReadResponseBodyAsync(context);
            var doc = JsonDocument.Parse(responseJson);
            Assert.Equal("Torneo no encontrado", doc.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public async Task DomainException_Validation_Returns400()
        {
            var middleware = new ExceptionHandlingMiddleware(
                next: (innerContext) => throw new ValidationException("El DNI es inválido."),
                environment: null,
                logger: null);

            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
            var responseJson = await ReadResponseBodyAsync(context);
            var doc = JsonDocument.Parse(responseJson);
            Assert.Equal("El DNI es inválido.", doc.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public async Task ConcurrencyException_Returns409WithFriendlyMessage()
        {
            var middleware = new ExceptionHandlingMiddleware(
                next: (innerContext) => throw new DbUpdateConcurrencyException("Concurrency conflict"),
                environment: null,
                logger: null);

            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
            var responseJson = await ReadResponseBodyAsync(context);
            var doc = JsonDocument.Parse(responseJson);
            Assert.Contains("modificado simultáneamente", doc.RootElement.GetProperty("error").GetString());
        }
    }
}
