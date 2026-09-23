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
pour la notification temps réel à l'adversaire. gRPC n'a pas été implémenté : il restait une
option pour un futur replay, jamais une obligation.

## Justification

SignalR est le seul des trois qui offre un client Blazor WebAssembly de première classe et un
repli automatique (WebSocket → SSE → long polling) sans code à écrire. Payer le coût de
gRPC-Web n'a de sens que pour un flux réellement continu comme un replay, pas pour des requêtes
ponctuelles déjà couvertes par REST + OpenAPI. C'est l'argument décisif retenu pour la
soutenance : « REST pour les commandes, SignalR pour le push ; gRPC n'apportait pas de bénéfice
ici. »

## Conséquences

Facile : `contracts/openapi.yaml` reste la source de vérité unique pour tout ce qui n'est pas
événementiel ; les méthodes du hub `GameHub` appellent le même `GameService` que les endpoints
REST, sans logique dupliquée.

Coûteux : le volet gRPC du référentiel du module reste non couvert — à assumer explicitement en
soutenance plutôt qu'à improviser. Si `E-29` (replay) est un jour demandé, le dossier
`Naval.Api/Grpc/` prévu dans l'arborescence de `docs/03-architecture.md` reste entièrement à
créer.
