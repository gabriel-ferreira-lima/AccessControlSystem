# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas os .csproj primeiro para aproveitar o cache de camadas no restore.
COPY AccessControlSystem.Api/AccessControlSystem.Api.csproj AccessControlSystem.Api/
COPY AccessControlSystem.Application/AccessControlSystem.Application.csproj AccessControlSystem.Application/
COPY AccessControlSystem.Domain/AccessControlSystem.Domain.csproj AccessControlSystem.Domain/
COPY AccessControlSystem.Infrastructure/AccessControlSystem.Infrastructure.csproj AccessControlSystem.Infrastructure/
RUN dotnet restore AccessControlSystem.Api/AccessControlSystem.Api.csproj

# Copia o restante do código e publica.
COPY . .
RUN dotnet publish AccessControlSystem.Api/AccessControlSystem.Api.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# libgssapi-krb5-2: evita o aviso "Cannot load library libgssapi_krb5.so.2"
# do Npgsql no boot (a lib de auth Kerberos não acompanha a imagem slim).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# A API roda em HTTP dentro do container (atrás do nginx / de um proxy).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app ./

# Usuário não-root já definido nas imagens .NET.
USER $APP_UID

ENTRYPOINT ["dotnet", "AccessControlSystem.Api.dll"]
