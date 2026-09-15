# Bataille Navale

Bataille navale à pouvoirs, jouable en solo contre l'IA ou en ligne à deux, dans une interface
inspirée des consoles portables à deux écrans.

## Démarrage

```bash
git clone <url> && cd bataille-navale
dotnet restore
dotnet build
dotnet test

# Deux terminaux
dotnet run --project src/Naval.Api   # https://localhost:7001
dotnet run --project src/Naval.App   # https://localhost:7002
```

La solution n'existe pas encore : les commandes de création sont dans
[`docs/03-architecture.md` §7](docs/03-architecture.md).

## Où trouver quoi

| Vous cherchez | Fichier |
|---|---|
| Ce qu'il faut construire, et dans quel ordre | `docs/01-fonctionnalites.md` |
| Les pouvoirs, leurs coûts, leur équilibrage | `docs/02-pouvoirs.md` |
| L'organisation du dépôt et du binôme | `docs/03-architecture.md` |
| Les assets et comment les produire | `docs/04-assets.md` |
| Le contrat HTTP | `contracts/openapi.yaml` |
| Les DTO | `src/Naval.Shared/Contracts/` |
| Les décisions déjà prises | `docs/adr/` |
| Le journal des échanges avec l'IA (livrable noté) | `PROMPTS.md` |

## Travailler avec Claude Code

`CLAUDE.md` charge le contexte du projet automatiquement. Cinq skills couvrent le domaine,
l'API, le front, les tests et le journal.

Slash-commands disponibles :

| Commande | Effet |
|---|---|
| `/journal <contexte>` | Complète la dernière entrée de `PROMPTS.md` |
| `/verifie-journal` | Audite `PROMPTS.md` avant le rendu |
| `/contrat <changement>` | Modifie un DTO et l'OpenAPI ensemble, sans divergence |
| `/pouvoir <nom>` | Implémente un pouvoir de bout en bout, tests inclus |

Un hook `UserPromptSubmit` crée automatiquement une entrée brouillon dans `PROMPTS.md` à chaque
prompt. Rendez les scripts exécutables après le clone :

```bash
chmod +x .claude/hooks/*.sh
```

Le hook a besoin de `jq` ou de `python3` (l'un des deux suffit) et échoue silencieusement sans.

## Les quatre premiers jours

1. **Jour 1, ensemble.** Créer la solution, figer `openapi.yaml` et les DTO, merger. Tout le
   reste du projet dépend de ce contrat.
2. **Jour 2.** Dev A : `Board`, `Ship`, placement, tests. Dev B : coque de console et grille
   en CSS, branchées sur `FakeGameApiClient`.
3. **Jour 3.** Dev A : résolution des tirs, tours, fin de partie, IA niveau 1. Dev B :
   écran de déploiement.
4. **Jour 4.** Brancher le front sur la vraie API. **Objectif : une partie solo jouable de
   bout en bout.**

Ensuite seulement : SignalR, puis les pouvoirs un par un, puis le polish.

Ne commencez pas les pouvoirs avant le jour 4. C'est l'erreur qui coule ce genre de projet.
