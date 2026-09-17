# syntax=docker/dockerfile:1
# Construit et exécute Naval.Api (API HTTP + hub SignalR). Contexte de build attendu :
# la racine du dépôt (docker build -f infra/docker/Naval.Api.Dockerfile .), pour que les
# chemins relatifs de Naval.Api.csproj vers Naval.Shared restent valides.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json verrouille le SDK ; Directory.Build.props porte TargetFramework/Nullable/etc.
# pour tous les projets — les deux doivent être au même niveau relatif qu'en local pour que
# MSBuild les retrouve en remontant l'arborescence depuis le .csproj.
COPY global.json Directory.Build.props ./

# Restore en deux temps : ne copier que les .csproj d'abord pour que la couche de cache Docker
# survive aux changements de code qui ne touchent pas les dépendances.
COPY src/Naval.Shared/Naval.Shared.csproj src/Naval.Shared/
COPY src/Naval.Api/Naval.Api.csproj src/Naval.Api/
RUN dotnet restore src/Naval.Api/Naval.Api.csproj

COPY src/Naval.Shared/ src/Naval.Shared/
COPY src/Naval.Api/ src/Naval.Api/
RUN dotnet publish src/Naval.Api/Naval.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl sert uniquement au HEALTHCHECK ci-dessous.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd --system naval \
    && useradd --system --gid naval naval

COPY --from=build /app/publish .
RUN chown -R naval:naval /app
USER naval

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Naval.Api.dll"]
