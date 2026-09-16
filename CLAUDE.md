# Bataille Navale — contexte projet

Projet scolaire en binôme. Une bataille navale jouable de bout en bout, avec pouvoirs et mode
en ligne, dans une interface inspirée des consoles portables à deux écrans.

## Stack

- .NET 10 (LTS). La version du SDK est verrouillée par `global.json`.
- ASP.NET Core Minimal APIs + SignalR pour le temps réel.
- Blazor WebAssembly pour le front.
- xUnit + FluentAssertions pour les tests.
- FluentValidation pour la validation des requêtes entrantes.

## Les quatre projets

| Projet | Rôle |
|---|---|
| `src/Naval.Shared` | Bibliothèque de modèles. `Domain/` = règles du jeu, `Contracts/` = DTO. |
| `src/Naval.Api` | API HTTP + hub SignalR. Aucune règle de jeu ici. |
| `src/Naval.App` | Front Blazor WebAssembly. |
| `tests/Naval.Tests` | Tests unitaires et d'intégration. |

La consigne impose exactement quatre projets. N'en crée pas un cinquième.

## Règles de dépendance

- `Naval.Shared/Domain` ne référence jamais `Naval.Shared/Contracts`. Le mapping va dans
  `Contracts/Mapping/`.
- `Naval.Shared` n'a aucune dépendance sur ASP.NET Core : il est compilé pour WebAssembly.
- `Naval.App` ne référence jamais `Naval.Api`.
- Les endpoints et les méthodes du hub appellent le même `GameService`. La logique de jeu n'est
  jamais dupliquée entre REST et SignalR.

## Invariants du jeu

- Le serveur fait autorité. Le client ne reçoit jamais la position des navires adverses non
  découverts — même en mode solo, même dans une réponse intermédiaire. Toute fuite d'information
  dans un DTO est un bug bloquant.
- Le joueur est identifié par un `playerToken` opaque, transmis en en-tête `X-Player-Token` et
  dans la query string SignalR. Ce n'est pas un mécanisme de sécurité, seulement d'identification
  de session.
- Toutes les erreurs sortent en `ProblemDetails` (RFC 9457) avec une extension `code` métier
  stable, du type `NOT_YOUR_TURN` ou `CELL_ALREADY_TARGETED`.
- Un tir hors tour renvoie `409`, pas `400`.
- Chaque mutation d'une partie est sérialisée par un `SemaphoreSlim` propre à cette partie.

## Contrats

`contracts/openapi.yaml` est la source de vérité du contrat HTTP. Si tu modifies un DTO dans
`Naval.Shared/Contracts`, mets à jour le YAML dans le même changement, et inversement. Une
divergence entre les deux bloque le binôme qui travaille sur l'autre moitié.

## Documentation de référence

Lis ces fichiers avant de concevoir quoi que ce soit dans leur domaine :

- `docs/01-fonctionnalites.md` — périmètre, identifiants `S-xx`/`E-xx`/`B-xx`, définition de terminé
- `docs/02-pouvoirs.md` — économie, catalogue, contrat `IPowerHandler`
- `docs/03-architecture.md` — arborescence, transports, travail à deux
- `docs/04-assets.md` — stratégie visuelle et sonore
- `docs/adr/` — décisions déjà prises. Ne les contredis pas sans le signaler.

## Conventions de code

- `Nullable` et `ImplicitUsings` activés, `TreatWarningsAsErrors` activé. Ne désactive pas
  un avertissement pour faire passer une compilation ; corrige la cause.
- Les DTO sont des `record` avec `init`, jamais des classes mutables.
- Les identifiants de domaine sont des types dédiés (`PlayerId`, `GameId`), pas des `Guid` nus.
- Les noms publics, les messages d'erreur et les commentaires sont en français ; les mots-clés
  techniques restent en anglais (`GameState`, `FireRequest`).
- Une méthode d'endpoint qui dépasse 15 lignes signale de la logique mal placée.
- Pas de `async void`. Pas de `.Result` ni de `.Wait()`.

## Tests

- Toute règle de jeu s'accompagne d'un test unitaire dans `tests/Naval.Tests/Domain/`.
- Utilise `GameBuilder` (dans `tests/Naval.Tests/Builders/`) plutôt que de reconstruire un état
  à la main.
- Un pouvoir n'est pas terminé sans trois tests : cas nominal, cas de refus, cas limite en bord
  de grille.
- Lance `dotnet test` avant de déclarer une tâche terminée.

## Commandes

```bash
dotnet build                                   # compile tout
dotnet test                                    # tous les tests
dotnet test --filter FullyQualifiedName~Powers # tests des pouvoirs
dotnet run --project src/Naval.Api             # API sur http://localhost:5119
dotnet run --project src/Naval.App             # front sur http://localhost:5018
dotnet format                                  # mise en forme
```

## Journal des échanges — obligation du projet

Le rendu impose un fichier `PROMPTS.md` documentant les échanges décisifs avec l'IA.

Un hook `UserPromptSubmit` crée automatiquement une entrée horodatée à chaque prompt. Cette
entrée est un **brouillon** : les champs « Décision et justification », « Scénario de
vérification » et « Résultat observé » ne peuvent pas être remplis par un script.

À la fin de toute tâche ayant impliqué un choix de conception — un contrat, un algorithme, un
compromis d'équilibrage, une décision de sécurité — complète la dernière entrée de `PROMPTS.md`
selon le format décrit dans `.claude/skills/naval-journal/SKILL.md`. Ne demande pas la
permission : c'est une exigence du rendu, pas une option.

Les tâches purement mécaniques (renommer une variable, corriger une faute de frappe, relancer
les tests) ne méritent pas d'entrée détaillée. Marque-les `trivial` et passe à la suite.

## Ce qu'il ne faut pas faire

- Ne crée pas de cinquième projet, même « juste pour les contrats ».
- N'ajoute pas de base de données tant que `E-27` n'est pas explicitement demandée. `IGameStore`
  en mémoire suffit.
- N'implémente pas un pouvoir avant que la partie solo ne soit jouable de bout en bout.
- Ne mets pas de règle de jeu dans un composant Razor. Le front affiche et envoie des intentions.
- Ne code pas en dur le catalogue des pouvoirs côté front : il vient de `GET /api/catalog/powers`.
