using AccessControlSystem.Api.Handlers;
using AccessControlSystem.Application;
using AccessControlSystem.Application.SharedContext.UseCases;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Services;
using Flunt.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace AccessControlSystem.Api.Extensions {
    public static class BuilderExtensions {

        public const string FrontendCorsPolicy = "FrontendCorsPolicy";

        public static void AddConfiguration(this WebApplicationBuilder builder) {

            Configuration.Database.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

            Configuration.Secrets.JwtPrivateKey = builder.Configuration.GetSection("Secrets").GetValue<string>("JwtPrivateKey") ?? string.Empty;
            Configuration.Secrets.PasswordSaltKey = builder.Configuration.GetSection("Secrets").GetValue<string>("PasswordSaltKey") ?? string.Empty;
        }

        public static void AddDatabase(this WebApplicationBuilder builder) {
            builder.Services.AddDbContext<AppDbContext>(
                x => x.UseNpgsql(Configuration.Database.ConnectionString,
                b => b.MigrationsAssembly("AccessControlSystem.Api")
            ));
        }

        public static void AddJwtAuthentication(this WebApplicationBuilder builder) {
            builder.Services.AddScoped<IActiveSessionChecker, ActiveSessionChecker>();

            builder.Services
                .AddAuthentication(x => {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(x => {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;
                    x.TokenValidationParameters = new TokenValidationParameters {
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration.Secrets.JwtPrivateKey)),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                    x.Events = new JwtBearerEvents {
                        OnTokenValidated = async context => {
                            if (context.Principal == null || !Guid.TryParse(context.Principal.Id(), out var operatorId)) {
                                context.Fail("Token sem identificação válida.");
                                return;
                            }

                            var checker = context.HttpContext.RequestServices.GetRequiredService<IActiveSessionChecker>();

                            if (!await checker.IsActiveAsync(operatorId, context.HttpContext.RequestAborted)) {
                                context.Fail("Conta desativada.");
                            }
                        },
                        OnChallenge = async context => {
                            context.HandleResponse();
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new ErrorResponse("Não autenticado", 401));
                        },
                        OnForbidden = async context => {
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new ErrorResponse("Sem permissão", 403));
                        },
                    };
                });
            builder.Services.AddAuthorization();
        }

        public static void AddControllersConfiguration(this WebApplicationBuilder builder) {
            builder.Services.AddControllers().AddJsonOptions(o =>
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            builder.Services.Configure<ApiBehaviorOptions>(options => {
                options.InvalidModelStateResponseFactory = context => {
                    var notifications = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .SelectMany(e => e.Value!.Errors.Select(x => new Notification(e.Key, x.ErrorMessage)));

                    var response = new ErrorResponse("Requisição inválida", 400, notifications);
                    return new ObjectResult(response) { StatusCode = 400 };
                };
            });
        }

        public static void AddErrorHandling(this WebApplicationBuilder builder) {
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddProblemDetails();
        }

        public static void AddCorsConfiguration(this WebApplicationBuilder builder) {
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            builder.Services.AddCors(options => {
                options.AddPolicy(FrontendCorsPolicy, policy => {
                    if (allowedOrigins.Length == 0) {
                        return;
                    }

                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
        }

        public static void AddRateLimiting(this WebApplicationBuilder builder) {
            builder.Services.AddRateLimiter(options => {
                options.AddPolicy("login", http =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                options.OnRejected = async (context, ct) => {
                    context.HttpContext.Response.StatusCode = 429;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString();

                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new ErrorResponse("Muitas tentativas. Tente novamente em instantes.", 429), ct);
                };
            });
        }

        public static void AddForwardedHeaders(this WebApplicationBuilder builder) {
            builder.Services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        public static void AddSwaggerDocumentation(this WebApplicationBuilder builder) {
            builder.Services.AddSwaggerGen(options => {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Nome do sistema de controle de acesso", Version = "v1" });
                options.CustomSchemaIds(type => type.FullName);
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "AccessControlSystem.Api.xml"));
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "AccessControlSystem.Application.xml"));
            });
        }
    }
}