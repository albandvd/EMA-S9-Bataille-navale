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
## Fusion de la branche assets, puis retour visuel de placement (S-04/S-05)

**Décision et justification :** Reprise explicite du point laissé en suspens par l'entrée
« Annulation de la défense en profondeur front/fake-client » : le binôme avait alors choisi de ne
*pas* garder la validation de placement (bornes + chevauchement) et son aperçu temps réel tant que
`Naval.Api` n'existait pas, pour ne pas concevoir cette UX sur une simulation vouée à disparaître.
`Naval.Api` est désormais mergé et le parcours solo est jouable de bout en bout (entrée
précédente) : la condition posée pour reprendre ce travail est remplie.

Avant cela, fusion (fast-forward, sans push) de la branche `assets` dans `main` : 23 icônes de
pouvoirs + 5 sprites de navires (normal/endommagé/coulé) + leurre, en SVG originaux, avec
`Naval.App.csproj` déjà modifié par cette branche pour les publier sous `wwwroot/assets/` via des
`<Content Link>`. Rien à ajuster côté build : aucun composant ne les consomme encore (prochaine
étape de polish visuel), donc pas de risque de régression en les intégrant maintenant.

Contrairement à la tentative annulée, la validation n'est pas dupliquée dans un faux client HTTP :
`GameApiClient` parle au vrai `Naval.Api`, qui reste la seule autorité à la soumission de
`PlaceFleetRequest`. Le nouveau code côté front est une **pure aide visuelle**, jamais une source
de vérité :
- `ShipTray` expose son navire sélectionné et son orientation via deux événements
  (`OnSelectionChanged`, `OnOrientationChanged`) au lieu de placer le navire lui-même
  (`PlaceSelectedAtAsync` est supprimé) ; la page appelante reste seule responsable de la
  validation géométrique, `ShipTray` ne connaît toujours pas la grille.
- `GridView`/`GridCell` gagnent un paramètre `Preview` (`bool?` : aucun / valide / refusé) piloté
  par un nouvel événement de survol (`OnCellHovered`), sans toucher à l'affichage des états de
  combat existants (miss/hit/sunk…).
- `Deploy.razor` calcule les cases occupées par le navire survolé (taille lue dans
  `Store.FleetPreset`), les compare aux bornes de la grille et aux navires déjà retenus localement
  (`_placements`), et n'appelle `ShipTray.ConfirmPlacement()` qu'après validation — sinon un
  message d'erreur s'affiche (réutilise la classe `.ds-error-banner` existante) et rien n'est
  ajouté à `_placements`. Le placement définitif reste soumis d'un bloc au clic sur « Valider la
  flotte », inchangé.

**Scénario de vérification :** Le SDK .NET 10.0.100 verrouillé par `global.json` manquait sur
cette machine (seul le 9.0.302 était installé) ; installé via
`winget install Microsoft.DotNet.SDK.10` (résout en 10.0.401, compatible grâce au
`rollForward: latestFeature` du `global.json`). Ensuite :
1. `dotnet build` → 0 avertissement, 0 erreur.
2. `dotnet test` → 47/47, aucune régression.
3. `Naval.Api` et `Naval.App` démarrés réellement (`dotnet run`, profils `http`, ports 5119/5018).
   Parcours Lobby → Deploy vérifié par pilotage direct du DOM (le pane navigateur ne compositait
   pas d'image dans cet environnement, donc pas de captures d'écran ; vérification par lecture du
   DOM et dispatch d'événements souris réels plutôt que par coordonnées à l'aveugle) :
   - Sélection du Carrier (5) → survol de la case (2,2) horizontal → les 5 cases (22 à 26 dans
     l'ordre de rendu) portent `grid-cell--preview-valid`.
   - Survol d'une case proche du bord droit (8,0) avec le Carrier → les deux cases encore dans la
     grille (8 et 9) portent `grid-cell--preview-invalid` ; les cases 10 à 12, hors grille,
     n'existent simplement pas à afficher (comportement attendu, pas un bug).
   - Clic sur cette position invalide → bannière d'erreur affichée (« Placement invalide : hors
     grille ou chevauchement d'un autre navire. ») et le Carrier reste sélectionné dans le tiroir
     (pas de retrait fantôme).
   - Clic sur une position valide (0,0 horizontal) → Carrier retiré du tiroir, aucune erreur.
   - Sélection du Battleship (4), survol d'une case chevauchant le Carrier déjà posé → les 4
     cases de l'aperçu portent `grid-cell--preview-invalid` (le chevauchement partiel rejette le
     placement entier, pas seulement les cases en conflit) ; clic → bannière d'erreur, Battleship
     toujours dans le tiroir.
4. Serveurs arrêtés proprement après vérification (`Stop-Process` sur les PID liés aux ports
   5119/5018, confirmé par `Get-NetTCPConnection` ne renvoyant plus rien).

**Résultat observé :** Build et tests verts, comportement conforme à la conception dans les cinq
scénarios ci-dessus (aperçu valide, aperçu invalide hors grille, refus au clic avec message et
navire conservé, placement valide accepté, refus par chevauchement). Aucune régression détectée.

## Refonte visuelle « Navcom » : la console deux écrans en CSS pur

**Prompt :** « Montre-moi l'étendue de tes capacités en matière d'UI. Ta mission est de
repousser les limites du design d'interface. » (Session exploratoire sur la branche `ui` :
l'utilisateur avait demandé de ne rien commiter ni documenter pendant le test ; le travail a
été conservé, commité par lui, et ces entrées formalisent a posteriori les décisions prises.)

**Décision et justification :** Application stricte du principe « CSS d'abord » de
`docs/04-assets.md` §2 : toute la refonte de ce commit se fait **sans un seul fichier image
ajouté**. La coque (métal brossé, vis, charnière crantée, LED qui respire, haut-parleurs,
croix directionnelle, boutons A/B/X/Y, START/SELECT) est en dégradés, ombres et pseudo-éléments ;
les écrans ont scanlines (`repeating-linear-gradient`), reflet de dalle et vignettage en calques
`aria-hidden` ; l'océan des grilles scintille par animation de `background-position` déphasée
selon `nth-child`, et le balayage radar est un `conic-gradient` en rotation sur `::after`.
Choix typographique en trio (Black Ops One pour le logo, Silkscreen pour l'interface, VT323 pour
les données), via Google Fonts — l'auto-hébergement en `.woff2` recommandé par la doc reste à
faire. Les libellés visibles disent « Navcom », jamais « DS » (`docs/04-assets.md` §9, marque
Nintendo). `prefers-reduced-motion` neutralise toutes les animations. Les commandes latérales
décoratives disparaissent sous 760 px. Aucune règle de jeu déplacée dans le front : les
composants Razor ne gagnent que du balisage de présentation (labels de coordonnées A–J/1–10
calculés dans `GridView`, jauge d'énergie à segments plafonnée à l'affichage).

**Scénario de vérification :** `dotnet build` → 0 avertissement. Parcours complet piloté par le
DOM (le pane navigateur ne compositait pas de captures dans cet environnement) : titre → lobby →
déploiement → bataille avec 8 tirs réels (2 touchés, 6 manqués, journal et jauge mis à jour).
Contrôles ciblés : `water-shimmer` et `radar-sweep` actifs dans les styles calculés, les trois
polices dans `document.fonts` à l'état `loaded`, labels A–J/1–10 rendus, aucune erreur console
hormis le 404 préexistant de `Naval.App.styles.css` (bundle scoped vide, bénin).

**Résultat observé :** Rendu conforme sur les cinq écrans, animations et états visuels de
cellule (touché, manqué, coulé, aperçu, mine, bouclier, brouillard) tous stylés, zéro fichier
binaire ajouté à ce stade.

## Les navires posés restaient invisibles pendant le déploiement

**Prompt :** « Lors de la phase de planification j'arrive bien à voir où je pose mon bateau,
en revanche une fois posé je ne le vois plus sur l'écran. »

**Décision et justification :** La grille de déploiement affichait `game.Self.Board` tel que
renvoyé par le serveur — or celui-ci ne connaît pas les placements avant la soumission de
`PlaceFleetRequest` : ils ne vivaient que dans la liste locale `_placements` de la page.
Correction par `BuildDisplayBoard` dans `Deploy.razor` : superposition des cases occupées par
les placements locaux (marquées `'S'`) sur la grille serveur, juste avant le passage à
`GridView`. C'est de l'affichage pur : la soumission d'un bloc au « Valider la flotte » et
l'autorité du serveur sont inchangées, et la validation locale continue de s'appuyer sur
`OccupiedCells`. Effet secondaire souhaitable : survoler une case occupée montre l'aperçu
rouge « refusé » par-dessus le navire posé.

**Scénario de vérification :** Après chaque pose successive (Carrier, Battleship, Cruiser),
comptage des cases `grid-cell--ship` dans le DOM : 5, puis 9, puis 12 — la flotte s'accumule
visuellement. `dotnet build` → 0 avertissement.

**Résultat observé :** Les navires posés restent visibles pendant toute la phase de
déploiement ; plus aucun placement à l'aveugle.

## Branchement des sprites de navires et des icônes de pouvoirs

**Prompt :** « Arrives-tu maintenant à intégrer les assets à tout ça ? »

**Décision et justification :** Quatre choix, et deux bugs réels découverts :

1. **Overlay de sprites par grille jumelle.** Plutôt que découper chaque sprite en tranches par
   cellule, `GridView` gagne un paramètre `ShipSprites` et rend une seconde grille CSS
   absolument positionnée, au même template et au même gap que les cases : chaque navire est un
   élément `grid-column: X / span taille`, donc aligné au pixel sans calcul de coordonnées. Les
   navires verticaux tournent le sprite horizontal de 90° avec une largeur pré-rotation de
   `calc(taille × 100 % + gaps)`. L'overlay est `pointer-events: none` et `aria-hidden`.
2. **Invariant de non-fuite respecté par construction.** Les sprites ne sont construits que
   depuis `Self.Fleet` (dont le serveur renseigne `Cells` uniquement pour sa propre flotte) et
   `Opponent.SunkShips` (positions déjà révélées). La variante (`-damaged`/`-sunk`) découle de
   `Hits`/`IsSunk` fournis par le serveur. Aucune position non révélée ne transite par le front.
3. **Icônes en masque CSS.** Les icônes sont tracées en `currentColor` : appliquées via
   `mask-image` (variable `--icon-url` posée par `PowerBar`), elles prennent la couleur d'état
   du pouvoir (prêt/armé/en charge/épuisé) définie en CSS. Le nom de fichier est dérivé du
   `PowerId` en kebab-case — aucune donnée du catalogue codée en dur côté front.
4. **Bug découvert : le montage `<Content Link>` du csproj ne servait rien en dev.** Les routes
   existaient dans le manifest des static web assets (200 au lieu de 404) mais les réponses
   étaient **vides, sans Content-Type** — les images étaient en état « broken » (diagnostic par
   `drawImage` sur canvas). Remplacé par une copie physique dans `wwwroot/assets/`, servie
   normalement. Point signalé au binôme auteur de la branche `assets` : son montage n'a
   probablement jamais servi un fichier en dev.
5. **Piège Chromium : une `url()` relative passée via `var()` se résout contre la feuille CSS**
   (`/css/assets/…` → 404), pas contre le document. Corrigé en URL absolue `/assets/…` dans le
   style inline généré par `PowerBar`.

**Scénario de vérification :** Placements mixtes (3 horizontaux, 2 verticaux) : spans et classe
`--vertical` corrects dans le DOM, sprite vertical mesuré à 28×113 px (bien tourné). Après un
coup encaissé de l'IA, le sprite du croiseur passe à `cruiser-damaged.svg`. Avant correctif :
`fetch` → 200, corps vide, `content-type: null`, canvas en erreur « broken state » ; après :
`image/svg+xml`, 39 SVG servis, 0 image cassée. Masque d'icône vérifié par style calculé et
`fetch` de l'URL résolue → 200.

**Résultat observé :** Sprites visibles aux trois états sur les deux grilles, épaves adverses
comprises, icônes colorées par état ; `dotnet build` → 0 avertissement.

## Bruitages 8-bit générés et joués en C# pur, sans JavaScript

**Prompt :** « Et le son ? Tu peux générer des bruitages 8-bit ? » puis « Je vois que tu as
utilisé du JS pour le son, peux-tu rester uniquement en C# ? »

**Décision et justification :**

1. **Synthèse maison plutôt qu'assets externes.** Les 12 bruitages de `docs/04-assets.md` §6
   (interface, refus, tir, manqué, touché, coulé, sonar, alarme, victoire, défaite) sont
   synthétisés à la manière de jsfxr — ondes carrées avec balayage de fréquence, bruit blanc
   filtré passe-bas, enveloppes exponentielles, bitcrush pour le grain chiptune — par un
   générateur C# jetable exécuté **hors solution** (répertoire temporaire : la consigne des
   quatre projets reste respectée). Création originale : zéro question de licence en soutenance.
   Sortie en WAV PCM 16 bits mono 22 050 Hz. La boucle d'ambiance `ambient-sea` est volontairement
   exclue (composition musicale, mieux servie par BeepBox).
2. **Lecture 100 % C#, à la demande explicite de l'utilisateur.** Une première version passait
   par un module JS et `IJSRuntime` ; remplacée par une solution sans interop : `AudioService`
   est un conteneur d'état pur (liste de sons actifs, identifiants uniques, événement `Changed`)
   et `AudioChannel.razor` la matérialise en éléments `<audio autoplay>` que le navigateur joue
   à l'insertion dans le DOM. `@key` sur l'identifiant force un élément neuf par déclenchement
   (les sons se chevauchent correctement) ; plafond de 6 éléments simultanés et purge des sons
   de plus de 4 s pour borner le DOM. La politique d'autoplay est satisfaite par construction :
   tous les déclencheurs suivent un clic. Les volumes sont pré-mixés dans les WAV, l'attribut
   HTML ne permettant pas de les régler.
3. **Le choix du son du tir n'est pas une règle de jeu.** `Battle` compare l'état avant/après le
   tir (`SunkShips.Count`, comptage des `x`/`#` du `TargetBoard`) pour choisir entre `sunk`,
   `explosion` et `splash` : simple lecture de ce que le serveur a déjà tranché. `deny` sonne
   sur `LastError`, l'alarme sur la transition `IsCharging` (une seule fois), les jingles au
   premier rendu de l'écran de fin.
4. **Compromis assumé : l'état muet n'est pas persisté.** La doc demande `localStorage`, mais
   y accéder exigerait précisément de l'interop JS. Le mute (bouton 🔊/🔇 encastré dans la
   charnière) vit en mémoire du service scoped : il survit à la navigation, pas au F5. À
   retrancher avec le binôme si la persistance devient un vrai besoin.

**Scénario de vérification :** Décodage des 12 WAV vérifié via `AudioContext.decodeAudioData`
(durées conformes). Lecture réelle prouvée en deux temps : un vrai clic (activation utilisateur
authentique, requise par la politique d'autoplay) → l'élément `ui-select` atteint `ended: true`
à `currentTime` 0,14 s, sa durée exacte ; puis un `MutationObserver` écoutant `ended` sur une
partie complète → 30 sons joués jusqu'au bout dans l'ordre attendu (boutons, sélection,
rotation, refus de chevauchement, `fire` puis `splash`/`explosion` par tir). Aucun avertissement
d'autoplay en console. `grep` : plus aucune référence à `naval-audio`/`IJSRuntime` dans les
sources de l'app.

**Résultat observé :** `dotnet build` → 0 avertissement, `dotnet test` → 47/47. Le seul
JavaScript restant dans l'application est le runtime Blazor lui-même.

## Les écrans de la console gardent une taille fixe quel que soit le contenu

**Prompt :** « A de nombreux moment la taille de l'écran de la ds change en fonction de ce qui
est affiché dedans. J'aimerai que l'écran et la ds garde tout le temps la meme taille quelque
soit le contenu qui est affiché dedans. »

**Décision et justification :**

1. **Dalle à ratio fixe, contenu absolu.** `.ds-screen` n'avait qu'un `min-height: 180px` : sa
   hauteur suivait le contenu (titre seul au lobby, grille + journal en bataille). Elle est
   désormais dimensionnée par `aspect-ratio: 16 / 10` — sa hauteur ne dépend plus que de la
   largeur de la console — et `container-type: size` coupe toute remontée de taille depuis
   les enfants. Le `.ds-screen__content` passe en `position: absolute; inset: 0` avec
   `overflow-y: auto` : ce qui dépasse défile *dans* la dalle, jamais en dehors. Le 4:3 d'une
   vraie DS a été essayé puis écarté : deux dalles 4:3 à 920 px de large donnaient une console de
   1 310 px de haut, injouable sans défiler la page.
2. **Deux dalles identiques.** Le capot avait des colonnes latérales de 34 px et le socle de
   92 px : les écrans faisaient 824 px et 704 px de large. Les deux moitiés partagent maintenant
   la même grille `92px 1fr 92px`, d'où deux dalles strictement égales (651 × 409 px à 920 px).
3. **Largeur de la console indépendante du contenu.** Avec le confinement de taille, la console
   perdait sa largeur intrinsèque et s'effondrait à 328 px (le `#app` de Blazor, item d'une
   grille `place-items: center`, se rétractait sur son contenu). `#app` prend désormais
   `width: 100%` et centre la console ; `.ds-console` passe en `box-sizing: border-box`, ce qui
   supprime au passage un débordement horizontal de 15 px sur mobile.
4. **La grille se borne à la hauteur de la dalle.** `.grid-frame` prend
   `min(100%, 380px, calc(100cqh - 115px))` : les 115 px couvrent le titre, les marges et la
   ligne des lettres. À 920 px la grille tient sans défilement dans les deux écrans.
5. **Écran bas de bataille en deux colonnes.** Grille + énergie + charge + pouvoirs + journal
   empilés ne tiennent pas dans une dalle 16:10 : `Battle.razor` enveloppe grille et
   instruments dans `.battle-panel` (grille à gauche, colonne d'instruments à droite, le journal
   s'étirant sur la hauteur de la grille). Pure disposition : aucune règle de jeu déplacée.
   Sous 760 px, retour à une colonne.
6. **Compromis mobile assumé.** À 400 px de large la dalle fait 323 × 204 px : une grille 10 × 10
   ne peut pas descendre sous la largeur des lettres de colonnes, donc le contenu défile
   légèrement à l'intérieur de l'écran. C'est le comportement voulu (la console ne bouge pas),
   la cible principale restant le bureau.

**Scénario de vérification :** Application lancée (API + front), parcours accueil → lobby →
déploiement (5 navires posés par script) → bataille, en mesurant `.ds-screen` et `.ds-console`
via `getBoundingClientRect` à chaque page. Avant correctif : dalles de 179 × 135 / 59 × 45 px
(effondrement) puis 824 × 619 / 704 × 529 px. Après : 651 × 409 px pour les deux dalles sur les
quatre pages, console à 920 px, `scrollHeight == clientHeight` (aucun défilement interne) sur
accueil, déploiement et bataille ; `scrollWidth` du document égal à la largeur de la fenêtre
à 1 280 px comme à 400 px. Captures dans `.playwright-mcp/` (index, deploy, battle, mobile).

**Résultat observé :** La console et ses deux écrans ont la même taille sur toutes les pages ;
seul le contenu change. `dotnet build` → 0 avertissement, `dotnet test` → 47/47.

---

## E-31 — CI GitHub Actions

**Prompt :** « mets toi sur une branche ci qui part de main et occupe toi de faire la ci »
(précédé d'un état des lieux du projet par rapport aux consignes, qui a identifié E-31 comme
absent : `.github/` n'existait pas alors que `03-architecture.md` documente déjà le workflow
attendu).

**Décision et justification :**

1. **`Naval.slnx` explicite dans les commandes, pas d'auto-détection.** Le dépôt utilise le
   nouveau format de solution `.slnx` (pas de `.sln`). `dotnet build`/`test` sans argument
   fonctionnent en local parce qu'il n'y a qu'un seul fichier de solution dans le répertoire,
   mais un CI qui dépend d'une auto-détection implicite casse silencieusement au premier fichier
   ambigu ajouté à la racine. Les trois étapes (`restore`, `build`, `test`) ciblent donc
   explicitement `Naval.slnx`.
2. **Version du SDK lue depuis `global.json` (`global-json-file`) plutôt que dupliquée dans le
   workflow.** `03-architecture.md` propose `dotnet-version: '10.0.x'` en dur dans le YAML ; ça
   crée une deuxième source de vérité qui peut diverger du `global.json` que les deux postes du
   binôme utilisent déjà. `actions/setup-dotnet@v4` sait lire `global.json` directement — un seul
   endroit à mettre à jour si la version du SDK change.
3. **Pas de flag `-warnaserror` redondant.** `Directory.Build.props` a déjà
   `TreatWarningsAsErrors=true` pour tous les projets ; l'ajouter aussi en CLI n'aurait changé
   rien de plus, mais aurait suggéré à tort que l'échec sur warning est une politique de CI et
   non une propriété du build lui-même — donc omis pour ne pas dupliquer la source de vérité.
4. **Build et test en configuration `Release`.** Les warnings et le comportement de certaines
   API (`Nullable`, optimisations JIT) peuvent différer de `Debug` ; c'est la configuration qui
   sera réellement publiée (voir `docker-compose.yml`, `ASPNETCORE_ENVIRONMENT: Production`), donc
   c'est elle qui doit être vérifiée en CI.
5. **Résultats de tests publiés en artefact `.trx`**, comme documenté, pour pouvoir inspecter un
   échec sur GitHub sans reproduire en local.

**Scénario de vérification :** Les trois commandes du workflow rejouées en local à l'identique :
`dotnet restore Naval.slnx`, `dotnet build Naval.slnx --no-restore --configuration Release`,
`dotnet test Naval.slnx --no-build --configuration Release --logger "trx;LogFileName=results.trx"`.

**Résultat observé :** Build Release → 0 avertissement, 0 erreur. Tests → 47/47,
`results.trx` généré dans `tests/Naval.Tests/TestResults/`. Le workflow n'a pas pu être exécuté
sur GitHub Actions depuis cette session (pas d'accès réseau à l'exécuteur) ; à confirmer par un
push/PR réel.

**Statut :** `terminé` (sous réserve de la première exécution réelle sur GitHub Actions)

---

## Premier pouvoir de bout en bout — Sonar (P-01)

**Prompt :** Enchaînement de 7 tâches issues de
`docs/superpowers/plans/2026-09-22-power-sonar-plan.md` (spec approuvée dans
`docs/superpowers/specs/2026-09-22-power-sonar-design.md`) : primitives `IPowerHandler`/
`PowerRegistry`/`PowerSlot`, loadout sur `PlayerState`, `SonarHandler` +
`GameEngine.ActivatePower`/`TickPowerCooldowns`, événements + `GameMapper`,
`GameService.UsePowerAsync` + énergie/cooldown par tour, endpoint REST, câblage front
(`Battle.razor`). Objectif : faire fonctionner un pouvoir complet, du clic à la grille jusqu'à
la réponse serveur, pour valider le pipeline générique avant les 22 autres pouvoirs du
catalogue.

**Décision et justification :**

1. **Distance euclidienne, pas Chebyshev, pour le disque de Sonar.** `docs/02-pouvoirs.md`
   décrit la zone de Sonar comme un « disque de rayon 4 » : `SonarHandler.Execute` compte donc
   les cases occupées adverses dont `dx² + dy² ≤ 16` (rayon au carré), plutôt qu'un simple
   `max(|dx|, |dy|) ≤ 4` (Chebyshev, qui donnerait un carré, pas un disque). La différence n'est
   pas cosmétique : aux quatre coins de la zone (`|dx| = |dy| = 4`), Chebyshev inclurait des
   cases à distance réelle 4√2 ≈ 5,7, hors de portée du sonar tel que décrit. Documenté en
   commentaire XML sur `SonarHandler` (et repris dans le plan lui-même) pour que le prochain
   pouvoir de zone à venir — la Frappe orbitale, qui est un carré 5×5 et doit donc rester en
   Chebyshev ou en bornes explicites — ne récupère pas `DistanceSquared` par erreur. Les deux
   métriques coexisteront dans le catalogue ; le nom de la méthode (`DistanceSquared`, privée à
   `SonarHandler`) évite qu'un futur handler la réutilise par simple copier-coller.
2. **`GrantTurnStartBenefits` accorde l'énergie ET décrémente les cooldowns au même instant :
   quand le joueur redevient actif.** E-10 (+1 énergie/tour) n'existait pas avant cette tâche ;
   E-14 (cooldowns) est nouveau aussi. Les regrouper dans une seule méthode privée de
   `GameService`, appelée depuis `StartBattle` (tour 1) et `AdvanceTurn` (tours suivants), évite
   deux pièges : (a) un ordre implicite entre deux appels séparés qu'un futur refactor pourrait
   inverser sans test qui casse immédiatement ; (b) un décalage d'un tour si l'un des deux était
   accroché à un autre point du cycle (ex. fin du tour du tireur précédent plutôt que début du
   tour du joueur qui redevient actif) — un cooldown pris à `AdvanceTurn` au lieu de son propre
   `GrantTurnStartBenefits` retarderait la disponibilité d'un pouvoir d'un tour complet pour le
   joueur qui vient de jouer. Le choix explicite est : les bénéfices de tour sont une notion
   unique, appliquée au joueur qui *devient* actif, jamais à celui qui vient de jouer. Un
   `EnergyChangedEvent` est émis à cette occasion, cohérent avec les événements déjà existants
   pour les gains d'énergie sur touche/coulé (`GameEngine.ExecuteShot`).
3. **Sonar équipé par défaut si `CreateGameRequest.Powers` est vide, plutôt que de bloquer la
   création de partie.** Le front (`Lobby.razor`, `PowerBar.razor`) est déjà câblé pour un
   loadout mais la sélection UI (E-13) n'est pas encore implémentée — `req.Powers` arrive donc
   vide dans la plupart des parties créées aujourd'hui. Deux autres options écartées : refuser la
   création (`400`) tant qu'aucun pouvoir n'est choisi — bloquerait toute partie solo existante
   pour une fonctionnalité UI hors scope de cette tâche — ou équiper silencieusement une liste
   vide — rendrait le pipeline de pouvoirs invisible et intestable de bout en bout tant que E-13
   n'est pas fait. Équiper `[PowerId.Sonar]` par défaut (les deux joueurs, IA comprise) garde la
   partie solo jouable immédiatement et sert de filet pour vérifier le pipeline en conditions
   réelles ; le code du choix (`req.Powers.Count > 0 ? req.Powers : [PowerId.Sonar]`) est une
   ligne à retirer quand E-13 fournira un vrai loadout choisi par le joueur.

Point mineur, non structurant : `IPowerHandler.Validate`/`Execute` prennent `PowerTargetDto`
(un DTO de `Contracts`) directement en paramètre plutôt qu'un type Domain intermédiaire — accepté
en cours d'implémentation car `GameEngine.ValidateAndBuildFleet`/`GenerateRandomPlacement`
suivent déjà ce même patron pour les requêtes de placement ; ce n'est pas une nouvelle dérogation
à la règle Domain/Contracts, juste la continuation d'un précédent déjà en place.

**Scénario de vérification :**
- 3 tests dédiés à `SonarHandler`/`GameEngine.ActivatePower` (nominal : 3 cases détectées, coût
  et cooldown corrects, `RevealedCells` vide ; refus : énergie insuffisante, état inchangé ;
  bord de grille : cible en coin `(0,0)`, cases hors bornes silencieusement exclues du comptage,
  pas d'exception).
- `GameMapperTests` : vue self expose le vrai statut/coût du slot (`CanAffordNow`), vue adverse
  n'expose que la liste des `PowerId` équipés, jamais leur statut.
- `GameServiceTests` : activation de bout en bout via `UsePowerAsync` (création → placement →
  activation Sonar), et refus `PowerNotEquipped` sur un pouvoir non équipé.
- Vérification manuelle de l'endpoint réel : `dotnet run --project src/Naval.Api`, séquence
  `POST /api/games` (Powers=["Sonar"]) → déploiement → `POST /api/games/{id}/powers` avec
  `{"powerId":"Sonar","target":{"cell":{"x":5,"y":5}}}` → `200` avec `revealedCount` renseigné,
  `revealedCells` vide.
- Vérification manuelle front : partie solo jusqu'à `Battle.razor`, clic sur le bouton Sonar
  (message « Ciblez une case… » affiché), clic sur une case de la grille adverse → énergie
  décrémentée de 3, slot en `OnCooldown`, entrée « Sonar : N case(s) détectée(s)… » dans
  `EventLog`.
- Suite complète : `dotnet build && dotnet test`, puis `dotnet format && dotnet build &&
  dotnet test` pour confirmer l'absence de régression après mise en forme.

**Résultat observé :** `dotnet build` → 0 avertissement sur les deux passes. `dotnet test` →
58/58 (47 préexistants + 2 `PowerRegistryTests` + 2 `PlayerStateTests` + 3 `SonarTests` + 2
`GameMapperTests` + 2 `GameServiceTests`), avant et après `dotnet format`. `dotnet format` n'a
touché aucun fichier créé par cette fonctionnalité ; il a en revanche réaligné des paramètres de
`record` multi-lignes dans onze fichiers préexistants (espaces d'alignement colonne supprimés,
aucun changement sémantique) — commité séparément (`style: dotnet format — alignement des
paramètres de record`).

**Statut :** `terminé`

---

## Revue finale de branche — 5 findings sur le pouvoir Sonar

**Prompt :** Revue finale « whole-branch » de `feat/E-10-16-power-sonar` (8 tâches déjà
committées et individuellement relues clean) ayant fait remonter 5 findings « Important »
transversaux, invisibles tâche par tâche : divergence 409/400 avec `contracts/openapi.yaml`,
`Target` nullable non gardé côté `UsePowerAsync`, `PowersEnabled` contredisant le loadout par
défaut, `JoinGameRequest.Powers` silencieusement ignoré, commentaire obsolète dans
`Battle.razor`. Consigne : corriger exactement ces 5 points en une seule passe, rien d'autre.

**Décision et justification :**

1. **`INSUFFICIENT_ENERGY` classé en conflit (409), pas en requête invalide (400).**
   `contracts/openapi.yaml` est la source de vérité du contrat HTTP (règle explicite de
   `CLAUDE.md`) et documente déjà cet exemple en 409. `IsPowerConflictCode` ne listait que
   `PowerAlreadyCharging`/`PowerOnCooldown`/`PowerExhausted` : ajout de
   `ErrorCodes.InsufficientEnergy` à cette liste. `PowerNotEquipped` et `InvalidTarget` restent
   en 400 — ce sont des erreurs de requête (pouvoir non équipé, cible malformée), pas des
   conflits d'état de partie, distinction que le YAML respecte aussi.
2. **Garde explicite sur `Target` null plutôt que dépendre de la validation FluentValidation en
   amont.** `UsePowerRequest.Target` est déclaré non-nullable côté C#, mais `System.Text.Json`
   accepte silencieusement un corps sans `target` ou `"target": null` — rien ne l'empêchait
   d'atteindre `SonarHandler.Validate`, qui déréférence `Cell` sans garde et lève une
   `NullReferenceException` non catchée par le pipeline `GameException` → `ProblemDetails`.
   Plutôt que de modifier `SonarHandler` (chaque futur handler répéterait la même faille) ou
   d'ajouter une règle FluentValidation séparée du reste de la logique d'erreurs pouvoir, un
   garde-fou est posé au même endroit que les autres vérifications de `UsePowerAsync` (tour,
   statut de partie) : `throw new GameException(ErrorCodes.InvalidTarget, …)` sans
   `isConflict`, donc 400 — c'est une requête malformée, pas un état de jeu en conflit.
3. **`PowersEnabled` recalculé à partir d'`equippedPowers`, pas de `req.Powers` brut.** Depuis la
   tâche précédente, une liste `Powers` vide déclenche déjà un loadout par défaut `[Sonar]` pour
   les deux joueurs (`equippedPowers`), mais `Game.PowersEnabled` (exposé en lobby via
   `GameSummary`/`OpenGameDto`) restait calculé sur `req.Powers.Count > 0` — un vestige d'avant
   cette décision, qui annonçait « pouvoirs désactivés » alors que Sonar était activement
   équipé et jouable. Corrigé pour réutiliser la variable déjà calculée
   (`equippedPowers.Count > 0`) : en pratique toujours vrai aujourd'hui (le fallback garantit au
   moins Sonar), ce qui est l'état honnête du système tant qu'E-13 n'introduit pas de vraie
   désactivation volontaire des pouvoirs. Doc XML de `CreateGameRequest.Powers` mise à jour en
   conséquence (ne prétend plus qu'une liste vide « désactive la mécanique »).
4. **Pas de transfert du loadout du joueur qui rejoint (`JoinGameRequest.Powers`) — juste un
   commentaire explicite du manque.** `PlayerState.EquippedPowers` est get-only, fixé une seule
   fois à la construction ; le rendre modifiable après coup pour un flux qui n'a aujourd'hui
   aucun chemin d'exécution possible (pas de mode en ligne réel encore branché) aurait été de la
   sur-ingénierie hors du périmètre de cette revue. Le gap est documenté en commentaire au point
   exact où `req.Powers` est disponible mais non utilisé, pour qu'un futur lecteur — ou l'auteur
   d'E-13 — voie explicitement ce qui est fixé et ce qui ne l'est délibérément pas encore.
5. **Commentaire de `Battle.razor::HandlePower` corrigé en TODO honnête plutôt que supprimé.**
   Le commentaire prétendait qu'un pouvoir `TargetKind.None` serait « activé immédiatement »,
   alors que le corps de la méthode ne fait que `_pendingPower = id;` pour tous les pouvoirs,
   sans branche. Un futur pouvoir sans cible (ex. `RadioIntercept`, déjà au catalogue) resterait
   donc bloqué en attente d'un clic qu'il n'utilise pas. Plutôt que d'implémenter la logique
   manquante (hors scope, pas de pouvoir `TargetKind.None` existant à ce jour pour la tester),
   le commentaire est corrigé pour décrire fidèlement le comportement actuel et signaler le gap
   comme TODO explicite.

**Scénario de vérification :**
- 3 tests ajoutés dans `tests/Naval.Tests/Api/GameServiceTests.cs` :
  `UsePowerAsync_rejects_insufficient_energy_as_a_conflict` (vérifie `Code ==
  InsufficientEnergy` et `IsConflict == true`), `UsePowerAsync_rejects_a_missing_target_as_invalid_target`
  (`Target: null!`, vérifie `Code == InvalidTarget` et `IsConflict == false`),
  `CreateGameAsync_reports_powers_enabled_even_when_powers_defaults_to_sonar` (`Powers: []`,
  vérifie `game.PowersEnabled == true`).
- `dotnet build` : 0 avertissement, 0 erreur.
- `dotnet test` : suite complète.
- Relecture de `contracts/openapi.yaml` (recherche « INSUFFICIENT_ENERGY ») pour confirmer
  l'alignement du code sur le statut 409 documenté avant de modifier `IsPowerConflictCode`.
- `git status --short` après les modifications : seuls les 4 fichiers autorisés par la tâche
  (`GameService.cs`, `Requests.cs`, `Battle.razor`, `GameServiceTests.cs`) apparaissent modifiés.

**Résultat observé :** `dotnet build` → 0 avertissement. `dotnet test` → 61/61 verts (58
préexistants + 3 nouveaux). Aucun fichier hors du périmètre autorisé n'a été touché.

**Statut :** `terminé`

## Deuxième pouvoir — Bombe lourde (P-24, `HeavyBomb`)

**Prompt :** « En créant une nouvelle branche j'aimerais que tu fasses un nouveau pouvoir. Comme
nous avons déjà un pouvoir de reconnaissance, j'aimerais avoir un pouvoir de destruction cette
fois. Ce pouvoir aura un coût et un cooldown significativement plus longs (10-12). Je te laisse
libre de décider de la puissance de la bombe et de sa manière de taper (en x, en +, une simple
explosion autour du centre…) »

**Décision et justification :**

1. **Forme : carré 3×3 centré (Chebyshev ≤ 1), pas un X ni un +.** Le + existe déjà au
   catalogue (`CrossStrike`, 5 cases pour 6 d'énergie) ; le X éparpille les tirs en diagonale,
   où deux cases touchées n'appartiennent presque jamais au même navire. Le carré garantit
   qu'un navire qui traverse le centre prend 3 touches alignées, ce qui correspond à l'idée
   d'un pouvoir « de destruction » plutôt que de recherche. Rayon 1 et pas 2 (5×5 = 25 cases)
   pour ne pas empiéter sur la future Frappe orbitale (P-11, 5×5 après 10 tours de charge).
2. **Coût 10, cooldown 11, pas de charge.** Avec +1 énergie par tour, 10 d'énergie ≈ 6 à 8
   tours d'économie (selon les touches) : la bombe arrive tard et une fois par tranche de ~11
   tours, soit 2 à 3 utilisations sur une partie de 45-60 tirs. Pas de tours de charge : le
   coût en tempo est déjà payé par l'économie d'énergie, et la charge + porteur est réservée
   aux pouvoirs « dévastateurs » (≥ 3 tours) selon docs/02-pouvoirs.md §1.
3. **Les touches de la bombe ne rapportent pas d'énergie.** Sans ça, une bombe qui touche 3
   cases et coule un navire rembourserait 2+2+3 = 7 des 10 points dépensés. Implémenté par un
   paramètre `grantEnergy` (défaut `true`) sur `GameEngine.ExecuteShot`, pour réutiliser la
   résolution de tir existante plutôt que la dupliquer dans le handler.
4. **La bombe remplace le tir du tour.** Sans ça, bombe + tir normal le même tour = 10 tirs.
   Ajout de `Shots` et `ConsumesTurn` sur `PowerEffectResult` ; `GameService.UsePowerAsync`
   émet un `ShotFiredEvent` par case (flux complet pour le replay E-26), puis clôt le tour via
   un nouveau helper `EndTurn` — extrait de `FireAsync`/`PlayAiTurnAsync` où la logique fin de
   partie/passage de main était dupliquée. En solo, l'endpoint déclenche le tour de l'IA comme
   après un tir. `PowerResultDto` portait déjà `Shots`, `NextPlayerId`, `GameOver`,
   `WinnerId` : aucun champ de contrat ajouté, seulement la valeur d'enum `HeavyBomb`.
5. **Bord de grille et cases déjà visées.** La zone est tronquée à la grille (4 cases en coin) ;
   les cases déjà visées sont sautées, pas refusées. Seule une zone intégralement déjà visée est
   refusée (`INVALID_TARGET`, 400) pour ne pas faire payer 10 d'énergie pour rien. Aucune fuite
   d'information : seules les cases frappées sont révélées, avec le même résultat qu'un tir.
6. **Loadout par défaut `[Sonar, HeavyBomb]`.** Le front n'a pas encore de sélection de loadout
   (E-13) : sans ce changement, la bombe serait inaccessible en jeu. Doc de
   `CreateGameRequest.Powers` et `contracts/openapi.yaml` alignés.
7. **Front : aucune logique propre à la bombe.** `Battle.razor` compare seulement la grille
   adverse avant/après le pouvoir : si des cases ont été frappées, même retour sonore qu'un tir,
   sinon ping ; redirection vers `/result` si la partie est finie. Icône `heavy-bomb.svg`.

**Hors périmètre, signalé :** l'IA a la bombe dans son loadout mais n'utilise toujours aucun
pouvoir (règle de symétrie §4.6 non encore traitée) ; l'équilibrage > 65 % de victoire n'a pas
été mesuré.

**Scénario de vérification :**
- 6 tests dans `tests/Naval.Tests/Domain/Powers/HeavyBombTests.cs` : nominal (9 tirs, destroyer
  coulé, énergie 10 → 0, cooldown 11), refus énergie insuffisante, refus zone entièrement
  visée, centre hors grille, coin (4 cases exactement), cases déjà visées sautées.
- 2 tests dans `GameServiceTests.cs` : la bombe passe la main à l'adversaire (`TurnNumber` 1 → 2),
  et une bombe qui coule le dernier navire termine la partie (`GameOver`, `WinnerId`).
- Run réel API + front : partie solo via l'API, 7 tirs pour atteindre 10 d'énergie, bombe en E5.
  Puis dans le navigateur : déploiement manuel et affichage de l'écran de bataille.

**Résultat observé :** `dotnet build` → 0 avertissement ; `dotnet format --verify-no-changes` →
propre ; `dotnet test` → 69/69 verts (61 + 8). En réel : 9 tirs D4-F6 à `energyGained: 0`, énergie
10 → 0, `nextPlayerId` = IA, l'IA a joué avant la réponse, cooldown affiché 10 au retour du
joueur, second usage → 409 `POWER_ON_COOLDOWN`. Côté front, le bouton HeavyBomb et son icône
s'affichent dans la barre de pouvoirs (grisé tant que l'énergie < 10).

**Statut :** `terminé`
