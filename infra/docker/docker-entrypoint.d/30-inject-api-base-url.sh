#!/bin/sh
# Régénère wwwroot/appsettings.json à partir du template à chaque démarrage du conteneur,
# pour que l'URL de l'API se configure par variable d'environnement plutôt qu'en rebuild.
# Exécuté automatiquement par l'image nginx officielle (docker-entrypoint.d/*.sh).
set -eu

: "${API_BASE_URL:=http://localhost:5119/}"

envsubst '${API_BASE_URL}' \
    < /usr/share/nginx/html/appsettings.template.json \
    > /usr/share/nginx/html/appsettings.json

echo "naval-app: ApiBaseUrl -> ${API_BASE_URL}"
