# ADR-002 — REST + SignalR ; place de gRPC

- **Date :** 2026-09-15
- **Statut :** accepté
- **Décideurs :** Alban, Laurent

## Contexte

Le module attend une couverture de plusieurs types de transport, tout en imposant qu'aucune
règle de jeu ne soit dupliquée entre REST et le hub temps réel, et que le front (Blazor
WebAssembly) consomme ce transport nativement, sans code de compatibilité maison.

## Options envisagées

1. **Tout en REST, avec polling client** pour détecter les coups de l'adversaire — un seul
   protocole, simple. Mais latence perceptible et charge serveur inutile pour une notification
   qui doit être immédiate (un tir adverse résolu doit apparaître sans délai).
2. **gRPC-Web pour tout**, y compris le cycle de vie (créer, rejoindre, tirer) — protocole
   unique et « moderne ». Mais gRPC pur ne tourne pas dans un navigateur : il faut un proxy
   `UseGrpcWeb()` côté serveur et un `GrpcWebHandler` côté client, pour un bénéfice nul sur des
   requêtes CRUD déjà bien couvertes par REST/OpenAPI.
3. **REST/JSON pour le cycle de vie, SignalR pour le push, gRPC-Web réservé à un usage où il
   apporte un avantage réel** : la relecture d'une partie terminée (streaming serveur), en
   option (`E-29`).

## Décision

REST/JSON pour toutes les actions de jeu (créer, rejoindre, déployer, tirer, pouvoir), SignalR
pour la notification temps réel à l'adversaire, gRPC-Web réservé exactement à l'usage prévu à
l'option 3 : la relecture d'une partie terminée (`E-29`).

## Justification

SignalR est le seul des trois qui offre un client Blazor WebAssembly de première classe et un
repli automatique (WebSocket → SSE → long polling) sans code à écrire. Payer le coût de
gRPC-Web n'a de sens que pour un flux réellement continu comme un replay, pas pour des requêtes
ponctuelles déjà couvertes par REST + OpenAPI. C'est l'argument décisif : « REST pour les
commandes, SignalR pour le push ; gRPC là où un flux serveur est le format naturel, pas
ailleurs. »

## Conséquences

Facile : `contracts/openapi.yaml` reste la source de vérité unique pour tout ce qui n'est pas
événementiel ; les méthodes du hub `GameHub` appellent le même `GameService` que les endpoints
REST, sans logique dupliquée.

**Mise à jour du 2026-09-23 — implémentation.** Le volet gRPC a été construit : contrat
`src/Naval.Api/Protos/naval.proto`, service `NavalReplayService` (`Naval.Api/Grpc/`), monté via
`app.UseGrpcWeb()` / `MapGrpcService<NavalReplayService>().EnableGrpcWeb()`. Un seul RPC en
streaming serveur, `StreamReplay`, rejoue le journal d'événements déjà tenu par `Game.Events` —
aucune règle de jeu n'est dupliquée, le service ne fait que lire `IGameStore` comme le reste de
l'API. Les deux erreurs attendues sont couvertes : `NOT_FOUND` (partie inconnue) et
`FAILED_PRECONDITION` (partie pas encore terminée), toutes deux avec le code métier stable en
trailer gRPC (`code`), sur le même principe que l'extension `code` des `ProblemDetails` REST.
Testé de bout en bout dans `NavalReplayServiceTests` via un vrai `GrpcWebHandler` contre
`WebApplicationFactory` — un échange gRPC-Web réel, pas une simulation.

Coûteux, accepté sciemment : ce n'est pas un client gRPC-Web dans `Naval.App` — la preuve
d'échange repose sur les tests d'intégration et un client externe (`grpcurl`, Postman), pas sur
une UI. Étendre le replay à un vrai lecteur dans le front demanderait de partager le `.proto`
avec `Naval.App` (sans y référencer `Naval.Api`, cf. `docs/03-architecture.md` §2) et d'y ajouter
`Grpc.Net.Client.Web` — non fait, car hors du périmètre demandé (couvrir le volet gRPC du
référentiel, pas construire un lecteur de replay complet).
