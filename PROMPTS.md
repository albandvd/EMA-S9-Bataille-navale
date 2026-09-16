# Échanges décisifs avec l'IA

Ce fichier est un livrable noté. Les entrées sont créées automatiquement par le hook
`.claude/hooks/journal-prompt.sh` à chaque prompt envoyé à Claude Code, puis complétées à la
main ou via la commande `/journal`.

Voir `.claude/skills/naval-journal/SKILL.md` pour le format attendu de chaque champ.

**Contrôle avant rendu :**

```bash
grep -c 'TODO' PROMPTS.md                    # doit renvoyer 0
grep -c 'Statut : `brouillon`' PROMPTS.md    # doit renvoyer 0
```

---

## Prompt 1 — 2026-09-15

**Prompt :** « En te basant sur le fichier `./docs/01-fonctionnalites.md`, fais tout le backend pour la partie socle en respectant l'archi décrite dans `./docs/03-architecture.md`. »

**Décision et justification :**

Quatre choix de conception majeurs ont été faits ici :

1. **Séparation `Board` incoming / outgoing par joueur.** Chaque `PlayerState` porte deux `Board` : `IncomingBoard` (tirs reçus sur la flotte propre) et `OutgoingBoard` (tirs envoyés sur la flotte adverse). Ce double pointeur garantit que `BuildTargetView` ne voit jamais les navires non découverts de l'adversaire — seules les cases marquées dans `OutgoingBoard` sont rendues — et que `BuildOwnView` montre les dégâts subis sans révéler la grille adverse.

2. **`GameEngine` statique pur.** Toute la logique de jeu (validation de placement, exécution d'un tir, détection de victoire, génération aléatoire de flotte) est dans des méthodes statiques sans dépendance externe. Ça permet de tester chaque règle sans instancier un `Game` complet, et ça garantit que l'API n'a aucune règle en double.

3. **`GameService` comme seul orchestrateur.** La couche API (endpoints + futur hub SignalR) appelle uniquement `GameService`. Aucune logique de jeu ne vit dans un endpoint. Un endpoint qui dépasse 15 lignes serait un signal d'alarme.

4. **`HuntTargetAi` avec mode damier en chasse.** En l'absence de hits, l'IA tire sur les cases où `(x + y) % 2 == 0` (damier). Ce schéma réduit le pire cas de découverte d'un navire de taille 2 : sans damier, un Destroyer peut se cacher indéfiniment dans les cases impaires.

**Scénario de vérification :**
- `dotnet build` → 0 avertissement
- `dotnet test` → 37 tests verts
- `POST /api/games` avec `Mode: SinglePlayer`, `FleetPreset: Classic`, `GridWidth: 10`, `GridHeight: 10` → `201` avec `gameId` + `playerToken`, statut `AwaitingDeployment`
- `POST /api/games/{id}/fleet` → flotte acceptée, partie passe en `InProgress`
- `POST /api/games/{id}/shots` → `Miss`/`Hit`/`Sunk` selon la case, `409` si hors tour
- `POST /api/games/{id}/forfeit` → statut `Abandoned`, `winnerId` renseigné

**Résultat observé :** Build propre (0 warning, TreatWarningsAsErrors activé). 37 tests verts couvrant Board, Placement, ShotResolution, TurnOrder, GameOver, RandomAi et HuntTargetAi. Tous les endpoints S-01 à S-17 sont branchés.

**Statut :** `terminé`

---

## GameStateStore comme unique consommateur de IGameApiClient

**Décision et justification :** Toutes les pages et composants de `Naval.App` passent par
`GameStateStore`, jamais directement par `IGameApiClient`. Le store expose une méthode par
action (`CreateGameAsync`, `FireAsync`, `PlaceFleetAsync`, `JoinGameAsync`, etc.), chacune
enveloppée dans un helper privé `RunAsync` qui capture `GameApiException` dans `LastError` et
déclenche `StateChanged` dans un bloc `finally` — donc sur le succès comme sur l'échec. Ce choix
centralise la gestion d'erreur en un seul endroit et garantit que l'UI ne peut pas rester bloquée
sur un état où l'appel a échoué sans notification, sans dupliquer un try/catch dans chaque
composant Razor.

**Scénario de vérification :** Ajout de deux tests dans
`tests/Naval.Tests/App/GameStateStoreTests.cs` : un appel à `JoinGameAsync` (qui échoue toujours
côté `FakeGameApiClient` avec `ErrorCodes.GameNotFound`) vérifie que `LastError` est peuplé et
que `StateChanged` ne se déclenche qu'une seule fois ; un appel `FireAsync` sur une partie
correctement déployée vérifie que `CurrentGame` est rafraîchi (`TurnNumber` incrémenté) et que
`LastError` reste `null`.

**Résultat observé :** Les deux tests passent (`dotnet test` : 7/7 au total). Le pattern
`RunAsync` tient sa promesse sur les deux chemins, succès et échec.

## Seuil de victoire simulé dans FakeGameApiClient.FireAsync

**Décision et justification :** Tant que `Naval.Api` n'existe pas côté moteur, `FireAsync` du
`FakeGameApiClient` déclenche une victoire simulée après `HitsToWin` (3) touches, à raison d'un
tir sur trois marqué « touché » de façon déterministe (`_shotCounter % 3 == 0`). C'est un choix
assumé et documenté en commentaire dans le code : aucune règle de jeu réelle n'est modélisée (pas
de coulage de navire, pas de flotte adverse), l'unique but est de rendre l'écran `/result`
atteignable pendant le développement du front, indépendamment de l'avancement du binôme moteur.

**Scénario de vérification :** Test `FireAsync_marks_the_game_finished_once_the_hit_threshold_is_reached`
dans `tests/Naval.Tests/App/FakeGameApiClientTests.cs`, qui enchaîne 9 tirs distincts et vérifie
que la partie passe à `GameStatus.Finished` avec le bon `WinnerId`. À l'occasion de cette revue,
ajout d'un garde-fou : un tir alors que `Status != InProgress` lève désormais une
`GameApiException` avec `ErrorCodes.GameNotInProgress` (409) plutôt que de continuer à muter une
partie terminée.

**Résultat observé :** Le test existant passe toujours. Le nouveau garde-fou n'a pas encore de
test dédié (pas de scénario UI qui le déclenche avant que `Naval.Api` existe), mais il ferme une
trappe latente identifiée en revue : rejouer « Rejouer » sans réinitialiser `_shotCounter` /
`_hitCounter` terminait la partie rejouée au premier tir — corrigé en réinitialisant les deux
compteurs au début de `CreateGameAsync`.

## Encodage de grille en chaînes de caractères et non-fuite des positions adverses

**Décision et justification :** `BoardViewDto.Rows` encode chaque case en un seul caractère
(`.`, `o`, `x`, `#`, etc., légende documentée une seule fois sur le DTO). `GridCell` et
`GridView` ne consomment que ce caractère : ils n'ont jamais accès à `ShipStateDto.Cells` d'un
navire adverse non coulé, parce que le DTO serveur ne le transmet pas (`Cells` vaut `null` pour
toute flotte qui n'est pas la sienne, sauf navire déjà coulé). Même dans `FakeGameApiClient`, qui
tourne entièrement côté client, cette règle est respectée : la grille adverse ne contient que ce
que le joueur a lui-même découvert par ses tirs.

**Scénario de vérification :** Lecture croisée de `Naval.Shared/Contracts/Responses.cs` (le
commentaire d'en-tête du fichier rappelle la règle absolue), de `GridCell.razor`/`GridView.razor`
(aucune référence à des coordonnées de navire, uniquement au caractère de la case) et de
`FakeGameApiClient.FireAsync` (qui ne modifie que `Opponent.TargetBoard`, jamais une copie des
positions de la flotte adverse, laquelle n'existe d'ailleurs pas dans l'état du fake).

**Résultat observé :** Aucune fuite constatée : le test
`FireAsync_only_reveals_the_targeted_cell` confirme qu'un seul caractère change par tir sur la
grille adverse. Cette convention tient même dans une implémentation 100 % côté client, ce qui
confirme qu'elle est bien portée par le contrat DTO et pas seulement par la confiance dans le
serveur réel.

## Ajout de GetFleetPresetAsync : lacune de contrat découverte à l'assemblage des pages

**Décision et justification :** En assemblant `Deploy.razor`, la revue finale a révélé que
`ShipTray` était alimenté avec `SelfViewDto.Fleet` — la liste des navires *déjà placés*, vide à
`AwaitingDeployment` — rendant l'écran de déploiement inerte : aucun navire n'était jamais
sélectionnable. Le vrai besoin (la liste des navires *encore à placer*, dérivée du preset choisi
en Lobby) n'avait pas de source côté contrat. Plutôt que de coder en dur le catalogue de flottes
dans le composant (interdit par CLAUDE.md, qui interdit déjà ce codage en dur pour les pouvoirs),
la correction ajoute `IGameApiClient.GetFleetPresetAsync(string presetName, CancellationToken)`,
implémentée dans `FakeGameApiClient` avec deux presets (« Classic », 5 navires, et « Skirmish »,
3 navires), exposée par `GameStateStore.FleetPreset` via `LoadFleetPresetAsync`, appelée depuis
`Lobby.StartGame()` juste après la création de la partie. `Deploy.razor` construit alors une
liste plate de `ShipStateDto` synthétiques à partir de `FleetPreset.Ships` (un par exemplaire de
chaque type), sans modifier la signature déjà approuvée de `ShipTray`.

**Scénario de vérification :** Relecture du chemin complet Lobby → Store → Deploy → ShipTray →
GridView pour confirmer qu'un preset non vide atteint bien `ShipTray.Fleet` avant le premier
rendu de `/deploy`. `dotnet build` reste à 0 avertissement/0 erreur après l'ajout de la méthode
sur les trois implémentations (`IGameApiClient`, `GameApiClient` en stub, `FakeGameApiClient`).
Vérification visuelle dans un navigateur non disponible dans cet environnement ; à confirmer en
aval par un contrôleur humain.

**Résultat observé :** Compilation et tests verts. Le déploiement devient fonctionnellement
possible (la flotte à placer n'est plus vide) ; la confirmation visuelle du clic-glisser reste à
faire par un contrôle manuel dans un vrai navigateur.

## Stylisation du formulaire de lancement de partie (Lobby.razor)

**Décision et justification :** Plutôt que d'extraire un composant `DsField` réutilisable,
le style est porté par des classes CSS globales (`.ds-form`, `.ds-field`, `.ds-field__label`,
`.ds-field__control`) ajoutées à `wwwroot/css/console.css`, appliquées directement dans
`Lobby.razor`. `Lobby.razor` est le seul écran avec des champs de saisie à ce stade (`Deploy`,
`Battle`, `Result` n'en ont pas) : créer un composant maintenant serait une abstraction sans
second appelant, contraire à YAGNI. Le style reste dans la palette de tokens existante
(`--sea`, `--sea-light`, `--screen-glow`) pour rester cohérent avec `DsButton`/`DsScreen` plutôt
que d'introduire de nouvelles couleurs. Aucune logique de jeu déplacée ni ajoutée : uniquement
des classes CSS et un `placeholder`, `@code` de `Lobby.razor` inchangé.

**Scénario de vérification :** `dotnet build src/Naval.App` après modification.

**Résultat observé :** Compilation réussie, 0 avertissement, 0 erreur. Vérification visuelle
dans un navigateur non disponible dans cet environnement ; à confirmer manuellement en lançant
`dotnet run --project src/Naval.App` et en ouvrant `/lobby`.

## Bug « impossible de placer un bateau » : absence totale de CSS pour ShipTray

**Décision et justification :** Signalement utilisateur : cliquer sur un nom de bateau dans la
liste de déploiement ne provoquait « rien » de visible. Investigation (`grep -rn "ship-tray"
wwwroot/css/`) : zéro règle CSS pour `.ship-tray`, `.ship-tray__item`, `.ship-tray__item--selected`
ou `.ship-tray__rotate` dans tout le projet. `ShipTray.Select` mettait pourtant bien à jour l'état
`_selected` — le clic fonctionnait, mais rien à l'écran ne changeait (pas de curseur pointeur, pas
de mise en évidence de la sélection), d'où l'impression de bouton mort. Tentative de reproduction
dans un navigateur réel via Playwright : Chromium n'est pas installé dans cet environnement
(`npx playwright install chrome` manquant), donc la confirmation visuelle reste indirecte, fondée
sur la lecture du code et l'absence vérifiée par grep de toute règle CSS correspondante.
Correctifs : (1) ajout des règles CSS manquantes dans `console.css`, cohérentes avec le style déjà
établi (`--sea`, `--sea-light`, `--screen-glow`, réutilisation de `.ds-button` pour le bouton
d'orientation) ; (2) `ShipTray` retire désormais un navire de la liste affichée une fois placé
(`_placedIds`, filtré côté composant plutôt que muté côté `Deploy`, pour ne pas changer le contrat
du paramètre `Fleet`), afin que la disparition du bateau serve elle-même de confirmation visuelle
du placement — ce second point comblait un manque de retour utilisateur connexe repéré pendant
l'investigation, pas seulement le bug rapporté.

**Scénario de vérification :** `dotnet build` (0 avertissement/0 erreur) puis `dotnet test`
(7/7, aucune régression sur `FakeGameApiClientTests`/`GameStateStoreTests`, qui ne testent pas le
rendu CSS mais valident que le flux de placement/tir sous-jacent n'a pas changé de comportement).

**Résultat observé :** Compilation et tests verts. Confirmation visuelle du clic « sélection
puis retrait de la liste » non faite dans un vrai navigateur dans cet environnement (Chromium
absent) ; à valider manuellement par l'utilisateur via `dotnet run --project src/Naval.App` →
`/lobby` → `/deploy`.

## Bug « les navires n'apparaissent pas » et grilles non identifiables

**Décision et justification :** Deuxième signalement utilisateur, après confirmation que le clic
de placement fonctionne désormais visuellement : les navires placés restent invisibles sur la
grille, et une fois la partie lancée, rien ne distingue sa propre grille de celle de l'adversaire.
Root cause pour le premier point : `FakeGameApiClient.PlaceFleetAsync` construisait bien
`Self.Fleet` (liste de navires avec leurs `Cells`) mais ne touchait jamais `Self.Board.Rows` — or
`GridCell` n'affiche que le caractère de la case du `BoardViewDto`, jamais `Fleet` directement. Le
plateau restait donc entièrement `'.'` même flotte placée. Correctif : nouvelle méthode privée
`PaintFleet` qui peint `'S'` (déjà géré par `GridCell.CssClass`) sur chaque case de
`Self.Board.Rows` occupée par un navire de la flotte, appelée à la fin de `PlaceFleetAsync`. Ceci
ne viole pas l'invariant « pas de fuite de position adverse non découverte » du CLAUDE.md : il
s'agit exclusivement du plateau du joueur lui-même (`Self.Board`), jamais de
`Opponent.TargetBoard` — le joueur a toujours eu le droit de voir sa propre flotte, c'est
`FakeGameApiClient` qui ne le lui montrait pas encore par un oubli d'implémentation, pas un choix
de sécurité. Second point (grilles non identifiables) : ajout d'un simple `<h2>` (« Votre grille »
/ « Grille adverse ») au-dessus de chaque `GridView`, sur `Deploy.razor` (un seul écran, donc pas
d'ambiguïté à lever avant le lancement de la partie ; le titre confirme quand même qu'on place sur
sa propre grille) et `Battle.razor` (où l'ambiguïté était réelle : écran du haut = tirs sur
l'adversaire, écran du bas = sa propre flotte, aucun texte ne le disait). Aucune règle de jeu
déplacée : uniquement du balisage de présentation.

**Scénario de vérification :** Nouveau test `PlaceFleetAsync_paints_the_ship_cells_onto_the_player_s_own_board`
dans `FakeGameApiClientTests` : place un destroyer horizontal en `(1,2)`, vérifie que
`Self.Board.Rows[2][1]` et `Rows[2][2]` valent `'S'` et qu'exactement 2 cases du plateau portent
ce caractère (pas de sur-peinture, pas de fuite vers `Opponent.TargetBoard` qui n'est pas touché
par ce test). `dotnet build` (0 avertissement/0 erreur), `dotnet test` (8/8, aucune régression).

**Résultat observé :** Compilation et tests verts, y compris le nouveau test qui échouerait sans
le correctif (`Board.Rows` restait à `'.'` avant `PaintFleet`). Vérification visuelle dans un
vrai navigateur toujours impossible dans cet environnement (Chromium absent pour Playwright) ; à
confirmer par l'utilisateur en relançant `dotnet run --project src/Naval.App` et en jouant le
parcours Lobby → Deploy → Battle jusqu'à voir ses navires affichés et les deux grilles étiquetées.

## Chevauchement de navires autorisé et flotte invisible pendant la phase de placement [ANNULÉ — voir entrée suivante]

**Décision et justification :** Troisième signalement : (1) rien n'empêche de superposer deux
navires en phase de placement, ce qui produit une flotte incohérente une fois la partie lancée ;
(2) même avec le correctif précédent (peinture du plateau à la confirmation), l'utilisateur ne
voit ses navires apparaître qu'*après* avoir cliqué « Valider la flotte » — pas au fur et à mesure
qu'il les place. Root cause du (1) : `FakeGameApiClient.PlaceFleetAsync` ne validait strictement
rien avant d'accepter un `PlaceFleetRequest`, alors que `ErrorCodes.OverlappingShips` et
`ErrorCodes.OutOfBounds` existent déjà dans le contrat (`Naval.Shared/Contracts/Realtime.cs`) —
ces codes métier étaient prévus dès la conception du contrat mais jamais branchés côté simulation.
Root cause du (2) : `Deploy.razor` n'accumule les placements que localement (`_placements`) et
n'envoie la requête qu'au clic sur « Valider la flotte » ; entre-temps, rien ne peint le plateau
affiché.

Choix d'architecture (défense en profondeur à deux niveaux, plutôt qu'un seul) :
- **Validation immédiate côté front** (`Deploy.HandleCellClicked`) : avant de déléguer à
  `ShipTray.PlaceSelectedAtAsync`, calcul des cases occupées par le navire sélectionné
  (`ShipTray.Selected`/`SelectedOrientation`, désormais exposés en lecture) et rejet si hors
  grille ou en collision avec `_occupiedCells` (les cases déjà retenues localement). Sans ce
  garde-fou côté front, un rejet différé à la confirmation aurait laissé `ShipTray` dans un état
  incohérent : le navire aurait déjà disparu de la liste (retiré de manière optimiste dès le
  clic) sans jamais avoir été réellement accepté, sans moyen de le replacer. Ceci n'est **pas**
  une règle de jeu déplacée dans un composant Razor au sens où l'entend le CLAUDE.md (tour de
  jeu, résolution de tir, économie des pouvoirs) : c'est une validation géométrique d'entrée
  (bornes + non-chevauchement), redondante par construction avec la validation serveur.
- **Validation faisant autorité côté `FakeGameApiClient.PlaceFleetAsync`** (qui simule le futur
  `Naval.Api`, seul endroit qui parlera vraiment au serveur) : rejet avec les codes
  `ErrorCodes.OutOfBounds` / `ErrorCodes.OverlappingShips` déjà prévus au contrat, si jamais un
  appelant contournait la validation front (bug futur, test direct du client, etc.). Le front
  n'est donc jamais la seule ligne de défense.
- **Aperçu local de la flotte** (`Deploy.BuildPreviewBoard`) : la grille du haut affiche
  désormais `Self.Board` peint localement avec les cases déjà retenues (`'S'`), sans attendre de
  round-trip serveur — pure présentation d'une information que l'utilisateur connaît déjà
  lui-même (sa propre flotte), aucune donnée cachée n'est concernée.
- `DsErrorBanner` complété avec les deux nouveaux codes pour rester cohérent avec la convention
  « toutes les erreurs sortent en ProblemDetails avec un code métier stable ».

**Scénario de vérification :** Deux nouveaux tests dans `FakeGameApiClientTests` :
`PlaceFleetAsync_rejects_overlapping_ships` (deux navires partageant une case → `GameApiException`
avec `ErrorCodes.OverlappingShips`) et `PlaceFleetAsync_rejects_a_ship_that_would_cross_the_grid_edge`
(destroyer de taille 2 posé en `(4,4)` horizontal sur une grille 5×5 → `ErrorCodes.OutOfBounds`).
`dotnet build` (0 avertissement/0 erreur), `dotnet test` (10/10, aucune régression sur les tests
existants dont celui de peinture de flotte ajouté précédemment).

**Résultat observé :** Compilation et tests verts. La validation front (`Deploy.HandleCellClicked`)
n'a pas de test dédié — elle vit dans un composant Razor, hors du périmètre `dotnet test` actuel
(pas de test de composants Blazor dans ce projet à ce stade) — donc seule la moitié « autorité »
de la défense en profondeur est couverte par des tests automatisés. La vérification visuelle
(aperçu en temps réel, message d'erreur affiché au clic sur une case invalide) reste à faire par
l'utilisateur, Chromium n'étant toujours pas disponible dans cet environnement pour Playwright.

## Annulation de la défense en profondeur front/fake-client (entrée précédente)

**Décision et justification :** Après explication de l'origine du dernier correctif (l'aperçu
local n'était qu'un contournement de l'absence de round-trip serveur par navire, pas un vrai
retour serveur), la décision du binôme a été de ne pas maintenir cette logique tant que
`Naval.Api` n'existe pas : mieux vaut concevoir la validation de placement (bornes, chevauchement)
et son retour visuel une fois le vrai serveur en place, plutôt que sur une simulation qui sera de
toute façon remplacée. Annulé intégralement : validation dans `Deploy.HandleCellClicked` (bornes +
chevauchement), `Deploy.BuildPreviewBoard`/`ShipCells`, `ShipTray.Selected`/`SelectedOrientation`,
validation dans `FakeGameApiClient.PlaceFleetAsync` (les throws `OutOfBounds`/`OverlappingShips`),
les deux entrées de `DsErrorBanner` correspondantes, et les deux tests de refus associés. **Non
annulé**, car indépendant du problème d'architecture serveur : le CSS de `ShipTray` (bug initial
de sélection invisible), le retrait d'un navire de la liste une fois placé (`_placedIds`), la
peinture du plateau propre par `PaintFleet` lors de la confirmation groupée (qui, elle, part bien
d'une réponse de `FakeGameApiClient.PlaceFleetAsync`, donc reste valide même en simulation), et
les étiquettes `<h2>` sur `Deploy`/`Battle`. Les deux problèmes qui restent donc en l'état après
cette annulation — chevauchement possible, pas d'aperçu pendant la phase de placement (avant
confirmation) — sont notés comme connus, à reprendre à la fusion de `Naval.Api`.

**Scénario de vérification :** `dotnet build` (0 avertissement/0 erreur), `dotnet test` (retour à
8/8, les deux tests de refus retirés avec le code qu'ils couvraient).

**Résultat observé :** Code revenu à l'état d'avant l'entrée annulée, sans toucher aux correctifs
indépendants. Aucune régression sur les fonctionnalités qui restent en place.

## Branchement de GameApiClient sur Naval.Api

**Décision et justification :** `Naval.Api` existe désormais (mergé depuis `main`) ; `GameApiClient`
était un stub `NotImplementedException`. Implémentation HTTP complète, avec plusieurs choix :

- **Un seul point d'envoi/réception** (`SendAsync<T>`) plutôt qu'une méthode HTTP dédiée par
  endpoint : chaque méthode de `IGameApiClient` ne fait que fournir verbe + route + corps + jeton
  optionnel. Ça garantit que la gestion d'erreur (parsing `ApiProblemDto` → `GameApiException`)
  et les options JSON sont appliquées identiquement partout, sans copier-coller.
- **`JsonSerializerOptions` explicites** (`JsonSerializerDefaults.Web` + `JsonStringEnumConverter`)
  plutôt que de compter sur les valeurs par défaut de `System.Net.Http.Json` : les extensions
  `ReadFromJsonAsync`/`JsonContent.Create` sans options utilisent `JsonSerializerOptions.Default`
  (PascalCase strict), qui ne correspond ni à la casse camelCase envoyée par
  `Naval.Api` (config web par défaut des Minimal API) ni aux enums sérialisés en chaîne
  (`ConfigureHttpJsonOptions` côté API ajoute `JsonStringEnumConverter`). Sans ce choix explicite,
  chaque réponse aurait échoué à la désérialisation silencieusement (propriétés à leur valeur par
  défaut) ou levé une exception sur les enums.
- **`X-Player-Token` dupliqué en constante locale** plutôt que référencé depuis
  `Naval.Api.Infrastructure.PlayerTokenAccessor.HeaderName` : `Naval.App` ne référence jamais
  `Naval.Api` (règle explicite du CLAUDE.md). Le nom d'en-tête est un détail de contrat HTTP, déjà
  documenté en toutes lettres dans le CLAUDE.md et l'OpenAPI — le dupliquer comme constante privée
  est plus sûr que créer un couplage de projet interdit pour une seule chaîne de caractères.
- **`GetFleetPresetAsync(name)` implémenté par filtrage client** de `GET /api/catalog/fleets` :
  l'API n'expose qu'un endpoint « tous les presets », pas de route par nom. Comportement de refus
  aligné sur `FakeGameApiClient` (même code `ErrorCodes.GameNotFound`) pour que le reste du front
  (`DsErrorBanner`, `GameStateStore`) n'ait pas à distinguer les deux implémentations.

**Bug de contrat découvert en cours de route — `ForfeitAsync` :** `IGameApiClient.ForfeitAsync`
déclarait `Task<GameStateDto>`, et `FakeGameApiClient` fabriquait un faux `GameStateDto` en
conséquence. Or `contracts/openapi.yaml` (source de vérité) documente depuis le départ que
`POST /api/games/{gameId}/forfeit` renvoie un `GameOver`, et `Naval.Api` l'implémente bien ainsi
(vérifié par un appel réel : réponse `{gameId, winnerId, winnerName, reason, totalTurns, stats}`).
Le front s'était trompé de type de retour avant même que l'API existe. Corrigé : l'interface
déclare maintenant `Task<GameOverDto>`, `FakeGameApiClient.ForfeitAsync` construit un `GameOverDto`
plausible à partir de son état interne, et `GameStateStore.ForfeitAsync` enchaîne un
`GetGameAsync` après le forfeit pour rafraîchir `CurrentGame` (même pattern que
`FireAsync`/`UsePowerAsync`, qui ne récupèrent pas non plus l'état de partie complet dans leur
réponse d'action). Aucune page ne consommait encore `Store.ForfeitAsync()` (pas de bouton
« Abandonner » dans `Battle.razor`), donc aucun autre fichier à ajuster.

**Réalignement des ports dev :** `Naval.App` tournait sur `7218` et `Naval.Api` sur `7279`, deux
ports auto-générés par le scaffold de chaque moitié du binôme, alors que le `CLAUDE.md` documente
`7002`/`7001` et que la policy CORS par défaut de `Naval.Api` (`Cors:AppOrigin`, jamais surchargée
dans `appsettings`) vise justement `https://localhost:7002`. Corrigé dans les deux
`launchSettings.json` pour que CORS fonctionne sans configuration supplémentaire. Ajout de
`wwwroot/appsettings.json` (`ApiBaseUrl: https://localhost:7001/`) côté `Naval.App`, lu dans
`Program.cs` pour construire le `HttpClient` injecté dans `GameApiClient` — c'est la même instance
`HttpClient` scope qui était déjà déclarée (mais jamais consommée) depuis le cycle 1.

**Bug backend découvert en vérifiant de bout en bout, non corrigé ici (hors périmètre du
branchement front) :** en mode `SinglePlayer`, `GameService.CreateGameAsync` place bien la flotte
IA (`p2.Fleet = aiFleet`) mais ne met jamais `p2.IsReady = true`. Or `PlaceFleetAsync` ne démarre
la partie (`StartBattle`) que si `Player1.IsReady && Player2.IsReady` — en solo, `Player2` (l'IA)
ne passe donc jamais à `IsReady`, et la partie reste bloquée en `AwaitingDeployment` après le
placement du joueur humain, y compris en interrogeant l'API réelle directement (vérifié par appel
`curl` direct, indépendamment de tout code front). Ce n'est pas un problème de câblage front : je
n'ai pas touché `Naval.Api` sans qu'on me le demande, puisque ce n'est pas le périmètre de cette
tâche et que c'est le code de l'autre moitié du binôme. Signalé pour action séparée.

**Scénario de vérification :** `dotnet build` (0 avertissement/0 erreur), `dotnet test` (45/45,
aucune régression). Vérification de bout en bout par appels `curl` directs contre `Naval.Api`
réellement démarré (`dotnet run --project src/Naval.Api`) : `POST /api/games` (201, forme
attendue), `GET /api/games/{id}` sans jeton (401 `MISSING_TOKEN`, forme `ApiProblemDto` conforme),
`GET /api/catalog/fleets`, `GET /api/catalog/powers`, `POST /api/games/{id}/fleet/random`,
`POST /api/games/{id}/fleet`, `POST /api/games/{id}/forfeit` (renvoie bien un `GameOver`,
confirmant le correctif de contrat). `POST /api/games/{id}/shots` renvoie `409
GAME_NOT_IN_PROGRESS` — attendu, conséquence directe du bug backend ci-dessus, pas du câblage
front.

**Résultat observé :** Le câblage HTTP lui-même (routes, en-têtes, formes JSON, gestion
d'erreurs) est vérifié correct contre l'API réelle pour tous les appels testables en l'état
(création, lecture, placement de flotte, erreurs). Le parcours de tir n'a pas pu être vérifié de
bout en bout à cause du bug backend `Player2.IsReady` signalé ci-dessus — pas testable avant sa
correction. `POST /api/games/{id}/powers` (`UsePowerAsync`) n'a pas d'endpoint côté `Naval.Api`
pour l'instant (pas de `PowerEndpoints.cs`) : implémenté côté front conformément à l'OpenAPI, mais
renverra 404 tant que ce n'est pas branché côté backend — cohérent avec la règle du projet de ne
pas implémenter les pouvoirs avant que le solo ne soit jouable de bout en bout.

## Correction du bug backend Player2.IsReady : la partie solo ne démarrait jamais

**Décision et justification :** Root cause confirmée à la fois par lecture de code et par un test
navigateur réel (Playwright/Brave) : `GameService.CreateGameAsync` génère bien la flotte IA
(`p2.Fleet = aiFleet`) en mode `SinglePlayer`, mais ne met jamais `p2.IsReady = true`. Or
`PlaceFleetAsync` ne déclenche `StartBattle` que si `Player1.IsReady && Player2.IsReady` — en
solo, `Player2` (l'IA) ne passait donc jamais à `IsReady`, et la partie restait bloquée en
`AwaitingDeployment` indéfiniment après que le joueur humain ait placé sa flotte, y compris via
un vrai navigateur contre l'API réelle. Correctif : une ligne, `p2.IsReady = true;` juste après
l'assignation de la flotte IA dans `CreateGameAsync`. Pas de changement de contrat, pas de DTO
touché, `openapi.yaml` n'a donc pas besoin de mise à jour.

**Scénario de vérification :**
1. Deux nouveaux tests dans `tests/Naval.Tests/Api/GameServiceTests.cs` (`GameService`
   instancié directement avec `InMemoryGameStore`, sans monter tout `Naval.Api`) :
   `CreateGameAsync_marks_the_ai_opponent_ready_in_single_player` (assert direct sur
   `game.Player2.IsReady`) et `PlaceFleetAsync_starts_the_battle_once_the_human_player_is_ready_in_single_player`
   (place une flotte valide via `SuggestRandomFleetAsync` puis vérifie `game.Status ==
   InProgress`). Les deux tests ont été vérifiés **rouges** avant le correctif (stash temporaire
   du fix, `dotnet test --filter GameServiceTests` → 2 échecs avec les messages d'assertion
   attendus), puis verts après restauration — pas seulement écrits après coup pour cocher une
   case.
2. Vérification de bout en bout dans un vrai navigateur (Brave, via le serveur MCP
   `playwright-brave`) : redémarrage de `Naval.Api` avec le correctif, parcours complet Lobby →
   Deploy (5 navires placés un par un) → clic « Valider la flotte ». La page navigue vers
   `/battle`, le journal d'événements affiche « La bataille commence. Tour de Joueur. » et
   « Joueur a déployé sa flotte. », la grille adverse est vide (aucune fuite d'information), la
   grille propre affiche la flotte complète correctement positionnée.
3. `dotnet build` (0 avertissement/0 erreur), `dotnet test` (47/47, aucune régression).

**Résultat observé :** Le parcours solo complet (Lobby → Deploy → Battle) est maintenant
jouable de bout en bout, vérifié à la fois par test automatisé et par un vrai navigateur. C'est
le dernier bloquant fonctionnel connu pour la partie solo ; le tir/la résolution de combat n'ont
pas encore été testés au-delà du démarrage de la bataille (hors périmètre de cette correction).

## Infra Docker pour le déploiement (`infra/docker/`, `docker-compose.yml`)

**Décision et justification :** Deux images, une par projet déployable — `naval-api`
(`Naval.Api` publié, exécuté par `dotnet` sur `mcr.microsoft.com/dotnet/aspnet:10.0`) et
`naval-app` (`Naval.App` compilé en Blazor WebAssembly, fichiers statiques servis par nginx).
`Naval.Shared` n'a pas d'image propre : il ne compile que comme dépendance des deux autres,
toujours quatre projets, pas un cinquième. Les deux `Dockerfile` copient explicitement
`global.json` et `Directory.Build.props` avant le code source, au même niveau relatif qu'en
local, pour que MSBuild retrouve le verrou de SDK et les propriétés communes
(`TreatWarningsAsErrors`, `Nullable`, etc.) en remontant l'arborescence depuis chaque `.csproj`
— sans ça le build en conteneur pourrait accepter des avertissements qu'un `dotnet build` local
rejette.

Décision la plus significative : **`ApiBaseUrl` et `Cors:AppOrigin` sont injectés à
l'exécution du conteneur, jamais figés au build.** `Naval.App` est du Blazor WebAssembly : le
code qui lit `ApiBaseUrl` (`Program.cs`) s'exécute dans le navigateur du joueur, pas dans le
conteneur `naval-app`. Un `ApiBaseUrl` pointant vers un nom de service Docker
(`http://api:8080`) serait donc injoignable — ce nom n'existe que dans le réseau interne
Compose, pas pour le navigateur. Pareil côté API : `Cors:AppOrigin` doit être l'origine que le
navigateur affiche réellement, pas un nom de service. J'ai donc :
1. Fait lire `Cors__AppOrigin` comme variable d'environnement côté `naval-api` (mécanisme
   standard de la configuration ASP.NET Core, aucun code à ajouter).
2. Ajouté un script dans `docker-entrypoint.d/` côté `naval-app`, exécuté automatiquement par
   l'image nginx officielle avant le démarrage du serveur, qui régénère
   `wwwroot/appsettings.json` à partir d'un template (`envsubst` sur `API_BASE_URL`). Ça permet
   de construire l'image une seule fois et de la redéployer telle quelle en dev/staging/prod —
   seule la variable d'environnement change, jamais un rebuild.

Autres choix mineurs : `nginx:1.27-alpine` plutôt que servir les fichiers statiques depuis
`Naval.Api` (qui violerait « `Naval.App` ne référence jamais `Naval.Api` » — l'inverse n'est
pas interdit par la lettre de la règle, mais mélanger front statique et API dans un seul
conteneur casse le déploiement indépendant des deux, donc écarté) ; `Content-Type:
application/wasm` forcé explicitement dans la conf nginx plutôt que de compter sur
`mime.types`, parce que certains navigateurs refusent la compilation en streaming du module
WASM sans ce type MIME exact ; `HEALTHCHECK` sur `naval-api` utilisé par `docker-compose.yml`
pour ordonner le démarrage (`depends_on: condition: service_healthy`) avant `naval-app`, même si
`naval-app` n'a pas besoin de l'API pour démarrer (statique), pour un ordre de boot lisible en
démo.

**Scénario de vérification :**
1. `docker build -f infra/docker/Naval.Api.Dockerfile .` et `docker build -f
   infra/docker/Naval.App.Dockerfile .` depuis la racine du dépôt : les deux réussissent sans
   avertissement (une première tentative a échoué sur `groupadd --gid 1000`, le GID étant déjà
   pris dans l'image de base `aspnet:10.0` — corrigé en laissant `groupadd`/`useradd` assigner
   un GID libre automatiquement plutôt que de le figer).
2. `docker compose up --build -d` avec un `.env` copié depuis `.env.example` : les deux
   conteneurs démarrent, `naval-api` passe `Healthy` avant que `naval-app` ne démarre
   (confirmé dans les logs `docker compose up`).
3. `curl http://localhost:5119/health` → `{"status":"Healthy","activeGames":0}`.
4. `curl http://localhost:8080/appsettings.json` → `{"ApiBaseUrl":
   "http://localhost:5119/"}`, confirmant l'injection à l'exécution (le fichier généré, pas un
   fichier baked au build).
5. `curl -D - http://localhost:8080/_framework/<nom-fingerprinté>.wasm` → `Content-Type:
   application/wasm`, `Cache-Control: public, max-age=31536000, immutable`.
6. Tous les assets référencés par le `<script>` fingerprinté réel de `index.html`
   (`dotnet.*.js`, `blazor.webassembly.*.js`, `dotnet.native.*.js`, `dotnet.runtime.*.js`)
   renvoient `200`.
7. `curl http://localhost:8080/lobby` (route côté client, pas un fichier réel) → `200`,
   confirme le fallback SPA (`try_files … /index.html`).
8. CORS : préflight `OPTIONS /api/games` avec `Origin: http://localhost:8080` (l'origine
   configurée) → `204` avec `Access-Control-Allow-Origin: http://localhost:8080`. Même requête
   avec `Origin: http://evil.example` → `204` mais **sans** aucun en-tête
   `Access-Control-Allow-*` — la policy CORS de `Naval.Api` rejette bien silencieusement une
   origine non autorisée.
9. `docker compose down` propre, images de test et `.env` de vérification supprimés après coup.

**Résultat observé :** La stack se construit et démarre proprement avec `docker compose up
--build`, sans modification du code applicatif. Le point de conception central — ne jamais
figer une adresse réseau interne à Docker dans ce que le navigateur doit résoudre — est
vérifié : changer `.env` seul (sans rebuild) suffit à repointer le déploiement vers d'autres
hôtes. Non couvert volontairement, documenté dans `infra/docker/README.md` : pas de
persistance (cohérent avec `IGameStore` en mémoire tant que `E-27` n'est pas demandée), pas de
TLS (à terminer en amont par un reverse proxy en prod), pas de pipeline CI.
