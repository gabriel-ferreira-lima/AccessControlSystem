using AccessControlSystem.Api.Extensions;
using AccessControlSystem.Api.Health;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);


builder.AddConfiguration();
builder.AddDatabase();
builder.AddJwtAuthentication();
builder.AddControllersConfiguration();
builder.AddErrorHandling();
builder.AddCorsConfiguration();
builder.AddRateLimiting();
builder.AddHealth();
builder.AddForwardedHeaders();

// A documentação interativa só existe em Development (Princípio IX): fora dele não há nem o gerador nem as rotas.
if (builder.Environment.IsDevelopment()) {
    builder.AddSwaggerDocumentation();
}

builder.AddAccountContext();


var app = builder.Build();
app.UseForwardedHeaders();

app.UseExceptionHandler();

if (app.Configuration.GetValue("HttpsRedirection:Enabled", true)) {
    app.UseHttpsRedirection();
}
app.UseCors(BuilderExtensions.FrontendCorsPolicy);
app.UseRateLimiter();

// Rota sem endpoint (inclusive /swagger/* fora de Development) responde 404. Sem isto, a política de fallback
// (que exige usuário autenticado) a transformaria em 401 e o Swagger "pareceria existir" (FR-003).
app.Use(async (context, next) => {
    if (context.GetEndpoint() is null) {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// As duas verificações de saúde (anônimas, fora de v1/): ver HealthEndpointExtensions.
app.MapHealthEndpoints();
if (app.Environment.IsDevelopment()) {
    // A documentação é anônima, mas só existe em Development (a política de fallback fecharia as rotas dela).
    app.MapSwagger().AllowAnonymous();
    app.MapSwaggerUI(setupAction: options => {
        options.DefaultModelsExpandDepth(-1);
    }).AllowAnonymous();
}

app.Run();

// Necessário para o WebApplicationFactory<Program> dos testes (o Program de nível superior é internal).
public partial class Program { }

// T066 caso 5: execução cancelada
