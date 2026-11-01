using AccessControlSystem.Api.Filters;
using AccessControlSystem.Api.Handlers;
using AccessControlSystem.Api.Options;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Application.SharedContext.UseCases;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Infrastructure.Services;
using AccessControlSystem.Infrastructure.Tenancy;
using Flunt.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace AccessControlSystem.Api.Extensions {
    public static class BuilderExtensions {

        public const string FrontendCorsPolicy = "FrontendCorsPolicy";

        public static void AddConfiguration(this WebApplicationBuilder builder) {

            // Segredo obrigatório: a Api não inicia sem a chave do JWT (a exigência de 256 bits é da spec de segurança).
            builder.Services.AddOptions<SecretsOptions>()
                .BindConfiguration(SecretsOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Sem ValidateOnStart de propósito: sem tenant padrão só o login é afetado (responde 401 e loga o motivo).
            builder.Services.AddOptions<LoginOptions>()
                .BindConfiguration(LoginOptions.SectionName);
        }

        public static void AddDatabase(this WebApplicationBuilder builder) {
            // Catálogo, resolução do tenant e AppDbContext por requisição, apontado para a base do tenant.
            // A Api não aplica migrations: quem faz isso é o Migrator.
            builder.Services.AddTenancy();
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
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                    x.Events = new JwtBearerEvents {
                        OnMessageReceived = context => {
                            // O login é anônimo e resolve o tenant padrão por conta própria: um token enviado a
                            // ele é ignorado (nem validado), seja qual for o tenant ou o estado dele.
                            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<UseDefaultLoginTenantAttribute>() is not null) {
                                context.NoResult();
                            }

                            return Task.CompletedTask;
                        },
                        OnTokenValidated = async context => {
                            if (context.Principal == null || !Guid.TryParse(context.Principal.Id(), out var operatorId)) {
                                context.Fail("Token sem identificação válida.");
                                return;
                            }

                            // O tenant vem da claim assinada, nunca de cabeçalho, rota ou corpo.
                            if (!Guid.TryParse(context.Principal.TenantId(), out var tenantId) || tenantId == Guid.Empty) {
                                context.Fail("Token sem tenant válido.");
                                return;
                            }

                            var services = context.HttpContext.RequestServices;

                            // Consulta o catálogo a cada requisição (sem cache): tenant inexistente ou inativo é recusado.
                            // Base sem connection string ou inacessível NÃO é recusa: lança e vira 500.
                            var resolver = services.GetRequiredService<ITenantResolver>();
                            if (!await resolver.ResolveAsync(tenantId, context.HttpContext.RequestAborted)) {
                                context.Fail("Tenant inexistente ou inativo.");
                                return;
                            }

                            // Só depois do tenant resolvido a sessão pode ser conferida (sob o RLS do tenant).
                            var checker = services.GetRequiredService<IActiveSessionChecker>();

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

            // A chave vem das opções (lida quando a validação do token é montada, não na partida do builder).
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<SecretsOptions>>((jwt, secrets) => {
                    jwt.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.Value.JwtPrivateKey));
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