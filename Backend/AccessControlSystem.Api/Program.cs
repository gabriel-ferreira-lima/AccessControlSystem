using AccessControlSystem.Api.Extensions;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);


builder.AddConfiguration();
builder.AddDatabase();
builder.AddJwtAuthentication();
builder.AddControllersConfiguration();
builder.AddErrorHandling();
builder.AddCorsConfiguration();
builder.AddRateLimiting();
builder.AddForwardedHeaders();
builder.AddSwaggerDocumentation();

builder.AddAccountContext();


var app = builder.Build();
app.UseForwardedHeaders();

app.ApplyMigrations();
app.UseExceptionHandler();

if (app.Configuration.GetValue("HttpsRedirection:Enabled", true)) {
    app.UseHttpsRedirection();
}
app.UseCors(BuilderExtensions.FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSwagger();
app.MapSwaggerUI(setupAction: options => {
    options.DefaultModelsExpandDepth(-1);
});

app.Run();
