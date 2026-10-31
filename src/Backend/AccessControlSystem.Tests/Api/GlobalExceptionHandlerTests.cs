using System.Text.Json;
using AccessControlSystem.Api.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControlSystem.Tests.Api;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Escreve_500_com_o_envelope_padrao_de_erro()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, new Exception("boom"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Erro interno do servidor", body.RootElement.GetProperty("message").GetString());
    }
}
