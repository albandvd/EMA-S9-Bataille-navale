# syntax=docker/dockerfile:1
# Construit Naval.App (Blazor WebAssembly) en fichiers statiques et les sert avec nginx.
# Contexte de build attendu : la racine du dépôt (docker build -f infra/docker/Naval.App.Dockerfile .).
#
# Naval.App tourne dans le navigateur : ApiBaseUrl est lu par le client, jamais par ce
# conteneur. Il ne peut donc pas être figé au build (ni pointer vers un nom de service Docker
# comme "http://api:8080", que le navigateur ne sait pas résoudre). Il est réinjecté au
# démarrage du conteneur nginx via la variable d'environnement API_BASE_URL — voir
# docker-entrypoint.d/30-inject-api-base-url.sh.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./

COPY src/Naval.Shared/Naval.Shared.csproj src/Naval.Shared/
COPY src/Naval.App/Naval.App.csproj src/Naval.App/
RUN dotnet restore src/Naval.App/Naval.App.csproj

COPY src/Naval.Shared/ src/Naval.Shared/
COPY src/Naval.App/ src/Naval.App/
RUN dotnet publish src/Naval.App/Naval.App.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM nginx:1.27-alpine AS runtime

COPY infra/docker/nginx/naval-app.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Modèle avec placeholder ${API_BASE_URL} ; le fichier réel est généré au démarrage du
# conteneur, pas au build, pour que la même image serve dev/staging/prod (voir plus haut).
COPY infra/docker/appsettings.template.json /usr/share/nginx/html/appsettings.template.json
COPY infra/docker/docker-entrypoint.d/30-inject-api-base-url.sh /docker-entrypoint.d/30-inject-api-base-url.sh
RUN chmod +x /docker-entrypoint.d/30-inject-api-base-url.sh

ENV API_BASE_URL=http://localhost:5119/
EXPOSE 80

# 127.0.0.1, pas localhost : /etc/hosts résout localhost en ::1 d'abord, et nginx n'écoute
# qu'en IPv4 ici (voir naval-app.conf) — wget échouerait en connection refused sur ::1.
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://127.0.0.1:80/ || exit 1

# L'image nginx officielle exécute automatiquement tout script de /docker-entrypoint.d/
# avant de démarrer nginx — pas besoin de surcharger ENTRYPOINT/CMD.
