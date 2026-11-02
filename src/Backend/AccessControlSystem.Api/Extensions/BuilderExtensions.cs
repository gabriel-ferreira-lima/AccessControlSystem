using AccessControlSystem.Api.Filters;
using AccessControlSystem.Api.Handlers;
using AccessControlSystem.Api.Health;
using AccessControlSystem.Api.Options;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Application.SharedContext.UseCases;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Infrastructure.Services;
using AccessControlSystem.Infrastructure.Tenancy;
using Flunt.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace AccessControlSystem.Api.Extensions {
    public static class BuilderExtensions {

        public const string FrontendCorsPolicy = "FrontendCorsPolicy";

        public static void AddConfiguration(this WebApplicationBuilder builder) {

            // Segredo obrigatório: a Api não inicia sem a chave do JWT, nem com chave de menos de 32 bytes (256 bits).
            builder.Services.AddOptions<SecretsOptions>()
                .BindConfiguration(SecretsOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<SecretsOptions>, SecretsOptionsValidator>());

            // Emissor e destinatário do token: obrigatórios e validados na partida (a Api só aceita tokens com eles).
            builder.Services.AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Sem ValidateOnStart de propósito: sem tenant padrão só o login é afetado (responde 401 e loga o motivo).
            builder.Services.AddOptions<LoginOptions>()
                .BindConfiguration(LoginOptions.SectionName);

            // Proxies cujos X-Forwarded-* valem: IP ou CIDR inválido derruba a partida (e a chave vai na mensagem).
            builder.Services.AddOptions<TrustedProxyOptions>()
                .BindConfiguration(TrustedProxyOptions.SectionName)
                .ValidateOnStart();
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<TrustedProxyOptions>, TrustedProxyOptionsValidator>());

            // Limite de login por origem, com piso e teto validados na partida.
            builder.Services.AddOptions<LoginRateLimitOptions>()
                .BindConfiguration(LoginRateLimitOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        public static void AddDatabase(this WebApplicationBuilder builder) {
            // Catálogo, resolução do tenant e AppDbContext por requisição, apontado para a base do tenant.
            // A Api não aplica migrations: quem faz isso é o Migrator.
            builder.Services.AddTenancy();
        }

        public static void AddJwtAuthentication(this WebApplicationBuilder builder) {
            builder.Services.AddScoped<ISessionValidator, SessionValidator>();

            builder.Services
                .AddAuthentication(x => {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(x => {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;
                    // Emissor, destinatário e chave vêm das opções (Configure abaixo). Aqui ficam as regras fixas: só HS256,
                    // validade obrigatória e tolerância de relógio curta (30 s).
                    x.TokenValidationParameters = new TokenValidationParameters {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                    };
                    x.Events = new JwtBearerEvents {
                        OnMessageReceived = context => {
                            var endpoint = context.HttpContext.GetEndpoint();

                            // O login é anônimo e resolve o tenant padrão por conta própria: um token enviado a
                            // ele é ignorado (nem validado), seja qual for o tenant ou o estado dele.
                            // O mesmo vale para as duas rotas de saúde (e SÓ para elas, por metadado do endpoint,
                            // nunca por prefixo de caminho): um token válido faria a validação consultar catálogo e
                            // sessão, e a verificação "viva" deixaria de não depender de nada.
                            if (endpoint?.Metadata.GetMetadata<UseDefaultLoginTenantAttribute>() is not null
                                || endpoint?.Metadata.GetMetadata<HealthEndpointMarker>() is not null) {
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

                            // A versão de segurança também vem da claim assinada. Token sem ela (ou com ela malformada) é
                            // recusado AQUI, antes de qualquer consulta ao catálogo ou à base do tenant.
                            if (context.Principal.SecurityVersion() is not int securityVersion) {
                                context.Fail("Token sem versão de segurança válida.");
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
                            var validator = services.GetRequiredService<ISessionValidator>();

                            // Uma única consulta: operador existente, ativo e com a versão de segurança do token.
                            if (!await validator.IsValidAsync(operatorId, securityVersion, context.HttpContext.RequestAborted)) {
                                context.Fail("Sessão inválida.");
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

            // A chave, o emissor e o destinatário vêm das opções (lidos quando a validação do token é montada, não na
            // partida do builder). Os mesmos bytes da chave (JwtSigningKey) assinam o token e validam a assinatura.
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<SecretsOptions>, IOptions<JwtOptions>>((jwtBearer, secrets, jwt) => {
                    jwtBearer.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(JwtSigningKey.GetBytes(secrets.Value.JwtPrivateKey));
                    jwtBearer.TokenValidationParameters.ValidIssuer = jwt.Value.Issuer;
                    jwtBearer.TokenValidationParameters.ValidAudience = jwt.Value.Audience;
                });

            // Fechado por padrão (Princípio IX): todo endpoint exige usuário autenticado, sem precisar declarar.
            // Rota anônima só com [AllowAnonymous] explícito (hoje: o login). Rota inexistente continua 404.
            builder.Services.AddAuthorization(options => {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
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
                // Por origem: o endereço que sobra depois do UseForwardedHeaders (só confia no proxy conhecido). Origem
                // ausente fica agrupada numa chave estável ("unknown"): o limite nunca é desligado.
                options.AddPolicy("login", http => {
                    var limit = http.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions {
                            PermitLimit = limit.PermitLimit,
                            Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                            QueueLimit = 0
                        });
                });

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

        public static void AddHealth(this WebApplicationBuilder builder) {
            // A sonda é escopada (usa o CatalogDbContext da requisição); o verificador é singleton porque guarda o
            // resultado em cache por instância (e só um booleano e um instante, nunca dado de tenant).
            builder.Services.TryAddSingleton(TimeProvider.System);
            builder.Services.AddScoped<ICatalogProbe, CatalogDbProbe>();
            builder.Services.AddSingleton<CatalogReadinessHealthCheck>();
            builder.Services.AddHealthChecks()
                .AddCheck<CatalogReadinessHealthCheck>(HealthEndpointExtensions.CatalogCheckName);
        }

        public static void AddForwardedHeaders(this WebApplicationBuilder builder) {
            builder.Services.AddOptions<ForwardedHeadersOptions>()
                .Configure<IOptions<TrustedProxyOptions>>((options, trusted) => {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                    // Só o último salto (o que o proxy conhecido acrescentou) conta; endereços forjados à esquerda são ignorados.
                    options.ForwardLimit = 1;

                    // NUNCA Clear(): no ASP.NET Core, KnownProxies e KnownIPNetworks AMBAS vazias significam "confiar em
                    // qualquer origem", e aí qualquer cliente forja X-Forwarded-For e contorna o limite por IP. Sem
                    // configuração valem só os padrões do framework (loopback); os valores configurados são ACRESCENTADOS.
                    // Valor inválido já derrubou a partida (TrustedProxyOptionsValidator); aqui basta ignorá-lo.
                    foreach (var proxy in trusted.Value.KnownProxies) {
                        if (IPAddress.TryParse(proxy, out var address)) {
                            options.KnownProxies.Add(address);
                        }
                    }

                    foreach (var network in trusted.Value.KnownNetworks) {
                        if (System.Net.IPNetwork.TryParse(network, out var parsed)) {
                            options.KnownIPNetworks.Add(parsed);
                        }
                    }
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