using AccessControlSystem.Api.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

// P-07 (R-14): o OnMessageReceived do JwtBearer enxerga os metadados do endpoint (o roteamento já
// rodou), então dá para ignorar o token nos endpoints marcados com [UseDefaultLoginTenant].
// Não precisa de banco: usa um host de teste mínimo, só com primitivas do ASP.NET.
public class P07LoginTokenIgnoredPremiseTests
{
    private const string SigningKey = "chave-de-teste-da-premissa-p07-com-mais-de-256-bits-aaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task Token_enviado_ao_login_e_ignorado()
    {
        var tokensValidados = 0;

        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                                ValidateIssuer = false,
                                ValidateAudience = false
                            };
                            options.Events = new JwtBearerEvents
                            {
                                OnMessageReceived = context =>
                                {
                                    var marcado = context.HttpContext.GetEndpoint()?.Metadata
                                        .GetMetadata<UseDefaultLoginTenantAttribute>() is not null;
                                    if (marcado) context.NoResult();
                                    return Task.CompletedTask;
                                },
                                OnTokenValidated = _ =>
                                {
                                    Interlocked.Increment(ref tokensValidados);
                                    return Task.CompletedTask;
                                }
                            };
                        });
                    services.AddAuthorization();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/login", () => "login")
                            .WithMetadata(new UseDefaultLoginTenantAttribute());
                        endpoints.MapGet("/outro", () => "outro");
                    });
                });
            })
            .StartAsync();

        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NewToken());

        var login = await client.GetAsync("/login");
        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(0, tokensValidados);   // o token enviado ao login foi ignorado

        var outro = await client.GetAsync("/outro");
        Assert.Equal(System.Net.HttpStatusCode.OK, outro.StatusCode);
        Assert.Equal(1, tokensValidados);   // em outro endpoint o mesmo token é validado
    }

    private static string NewToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: new[] { new Claim("Id", Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
