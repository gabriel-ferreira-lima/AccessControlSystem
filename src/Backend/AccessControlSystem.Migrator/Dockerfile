# Migrator: passo único que atualiza o catálogo e todas as bases de tenant (migrate) e, só em
# desenvolvimento, cria os dados sintéticos (seed-dev). Contexto de build: src/Backend.
#
#   docker build -f AccessControlSystem.Migrator/Dockerfile -t acs-migrator src/Backend
#
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas os .csproj primeiro para aproveitar o cache de camadas no restore. A Api entra porque
# as migrations ficam no assembly dela.
COPY AccessControlSystem.Migrator/AccessControlSystem.Migrator.csproj AccessControlSystem.Migrator/
COPY AccessControlSystem.Api/AccessControlSystem.Api.csproj AccessControlSystem.Api/
COPY AccessControlSystem.Application/AccessControlSystem.Application.csproj AccessControlSystem.Application/
COPY AccessControlSystem.Domain/AccessControlSystem.Domain.csproj AccessControlSystem.Domain/
COPY AccessControlSystem.Infrastructure/AccessControlSystem.Infrastructure.csproj AccessControlSystem.Infrastructure/
RUN dotnet restore AccessControlSystem.Migrator/AccessControlSystem.Migrator.csproj

# Copia o restante do código e publica.
COPY . .
RUN dotnet publish AccessControlSystem.Migrator/AccessControlSystem.Migrator.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

# ---- runtime ----
# O Migrator referencia a Api (migrations), então precisa do runtime do ASP.NET, não só do .NET.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# libgssapi-krb5-2: evita o aviso "Cannot load library libgssapi_krb5.so.2" do Npgsql.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Usuário não-root já definido nas imagens .NET.
USER $APP_UID

# O verbo (migrate ou seed-dev) vem do comando do container; sem verbo, sai com código 2.
ENTRYPOINT ["dotnet", "AccessControlSystem.Migrator.dll"]
