# Revues de propositions IA

Cinq revues argumentées. Chacune montre où le binôme a contredit l'IA, et le débat technique
que le choix a ouvert. Les commits se retrouvent avec les commandes `git log` indiquées (recherche par contenu, reproductible sur le dépôt).

---

## Revue 1 : type de retour de `ForfeitAsync` (contrat front / OpenAPI)

* Proposition et référence dans le dépôt : l'IA avait déclaré `IGameApiClient.ForfeitAsync` en `Task<GameStateDto>` et le `FakeGameApiClient` fabriquait un faux état de partie.
* Hypothèse à vérifier : le front respecte le contrat `contracts/openapi.yaml`, source de vérité.
* Scénario, données ou commande : `curl -X POST .../api/games/{id}/forfeit` avec `X-Player-Token` sur `Naval.Api` démarré ; lecture de l'OpenAPI.
* Résultat attendu avant exécution : une réponse de type `GameState`, comme le déclarait l'interface.
* Erreur que ce contrôle pourrait détecter : désérialisation d'une réponse dans le mauvais DTO, champs silencieusement à leur valeur par défaut.
* Résultat réellement observé : l'API renvoie `{gameId, winnerId, winnerName, reason, totalTurns, stats}`, un `GameOver`, conforme à l'OpenAPI depuis le départ. La proposition initiale de l'IA était fausse.
* Décision et justification : correction de l'interface en `Task<GameOverDto>`, adaptation du faux client, puis `GameStateStore.ForfeitAsync` enchaîne un `GetGameAsync` (même schéma que `FireAsync`). Le binôme conteste la proposition de l'IA : l'erreur vient d'un contrat recopié à la main, et il exige désormais que le faux client soit vérifié contre l'OpenAPI. L'IA propose ensuite de renvoyer l'état complet depuis `/forfeit` côté API pour éviter le second appel ; le binôme s'y oppose pour garder des réponses d'action minimales et cohérentes avec `/shots`, et c'est cette option qui est retenue.
* Preuves reproductibles et liens vers les commits : `git log --oneline -S "GameOverDto" -- src/Naval.App` ; relancer l'appel `curl` ci-dessus.
* Après correction éventuelle : résultat avant / après : avant, type déclaré `GameStateDto` incompatible avec la réponse réelle ; après, réponse correctement désérialisée, 45/45 tests verts.
* Limites et points non vérifiés : aucun bouton Abandonner n'existait alors dans `Battle.razor`, le chemin n'a donc pas été testé par l'interface ; pas de test de contrat automatisé entre OpenAPI et `Naval.Shared`.

---

## Revue 2 : validation de placement en défense en profondeur sur le faux client

* Proposition et référence dans le dépôt : validation bornes + chevauchement dans `Deploy.HandleCellClicked` et dans `FakeGameApiClient.PlaceFleetAsync` (`OutOfBounds`, `OverlappingShips`), avec aperçu local.
* Hypothèse à vérifier : dupliquer la validation côté front et côté faux client est utile avant que `Naval.Api` existe.
* Scénario, données ou commande : tests `PlaceFleetAsync_rejects_overlapping_ships` et `..._cross_the_grid_edge` (destroyer en (4,4) sur grille 5×5) ; `dotnet test`.
* Résultat attendu avant exécution : les deux refus levés avec le bon code, 10/10.
* Erreur que ce contrôle pourrait détecter : flotte incohérente acceptée, navire retiré du tiroir sans être accepté.
* Résultat réellement observé : 10/10 verts, mais la validation faisant autorité vivait dans une simulation destinée à disparaître, et la moitié front n'était couverte par aucun test.
* Décision et justification : proposition contredite par le binôme et annulée intégralement (retour à 8/8), en conservant les correctifs indépendants. Reprise plus tard quand l'API réelle est devenue seule autorité, le front ne gardant qu'une aide visuelle. L'IA défend une validation en profondeur dès maintenant ; le binôme refuse de concevoir une UX sur un faux serveur voué à disparaître. L'annulation a évité de maintenir deux validations divergentes.
* Preuves reproductibles et liens vers les commits : `git log --oneline -S "OverlappingShips" -- src/Naval.App` (ajout puis retrait) ; `git log --oneline -S "OnSelectionChanged"` (reprise).
* Après correction éventuelle : résultat avant / après : avant reprise, chevauchement possible et pose à l'aveugle ; après, cases `grid-cell--preview-invalid`, bannière d'erreur et navire conservé dans le tiroir sur les 5 scénarios DOM.
* Limites et points non vérifiés : la validation dans `Deploy.razor` n'a toujours pas de test automatisé (pas de tests de composants Blazor dans le projet).

---

## Revue 3 : bug `Player2.IsReady` et périmètre d'intervention de l'IA

* Proposition et référence dans le dépôt : lors du branchement front, l'IA signale sans corriger que l'IA solo n'est jamais marquée prête dans `GameService.CreateGameAsync` ; correction d'une ligne dans une tâche dédiée.
* Hypothèse à vérifier : la partie solo reste bloquée en `AwaitingDeployment` à cause du backend, pas du câblage front.
* Scénario, données ou commande : `curl` direct sur l'API (création, flotte, tir) ; tests `CreateGameAsync_marks_the_ai_opponent_ready_in_single_player` et `PlaceFleetAsync_starts_the_battle_once_the_human_player_is_ready_in_single_player`, exécutés avec le correctif en stash puis restauré.
* Résultat attendu avant exécution : tests rouges sans le correctif, verts avec ; statut `InProgress` après placement.
* Erreur que ce contrôle pourrait détecter : régression du démarrage de partie solo ; test écrit après coup qui passerait même sans le correctif.
* Résultat réellement observé : `/shots` en 409 `GAME_NOT_IN_PROGRESS` avant correctif ; 2 échecs attendus puis 2 succès ; parcours Lobby → Deploy → Battle confirmé dans Brave ; 47/47.
* Décision et justification : correctif retenu. Le développeur front demande à l'IA de corriger immédiatement le backend pour ne plus bloquer ses tests ; l'IA refuse de modifier le code de l'autre moitié du binôme et se limite à signaler le bug. Le binôme tranche en faveur de l'IA : la répartition des responsabilités est respectée, au prix d'un parcours de tir non vérifiable temporairement, et le correctif est fait dans une tâche dédiée.
* Preuves reproductibles et liens vers les commits : `git log --oneline -S "p2.IsReady = true"` ; `dotnet test --filter GameServiceTests`.
* Après correction éventuelle : résultat avant / après : avant, partie bloquée et tir refusé ; après, bataille lancée, message de début de bataille dans le journal, grille adverse vide.
* Limites et points non vérifiés : la résolution des tirs n'a pas été vérifiée au-delà du démarrage dans cette correction.

---

## Revue 4 : audio en JavaScript puis en C# pur, et persistance du mute

* Proposition et référence dans le dépôt : première version de lecture audio via un module JS et `IJSRuntime`, remplacée par `AudioService` + `AudioChannel.razor` ; mute gardé en mémoire faute d'accès à `localStorage` sans interop.
* Hypothèse à vérifier : une lecture sans JavaScript personnalisé respecte la politique d'autoplay et joue tous les sons.
* Scénario, données ou commande : vrai clic puis lecture de `ended` sur `ui-select` ; `MutationObserver` sur une partie complète ; `grep` de `naval-audio` et `IJSRuntime` dans les sources.
* Résultat attendu avant exécution : sons joués en entier dans l'ordre, aucun avertissement d'autoplay.
* Erreur que ce contrôle pourrait détecter : sons bloqués par le navigateur, éléments `<audio>` accumulés dans le DOM.
* Résultat réellement observé : `ui-select` terminé à 0,14 s (sa durée exacte) ; 30 sons joués dans l'ordre attendu ; plus aucune référence JS audio ; 47/47.
* Décision et justification : le développeur rejette la version JavaScript et demande que tout soit fait en C# ; l'IA réécrit la lecture audio en conséquence. En revue, le binôme conteste aussi le compromis de l'IA sur le mute : la fonctionnalité multijoueur a ensuite introduit `LocalStorageService` via `IJSRuntime` (appel des fonctions globales, sans fichier .js), donc l'argument selon lequel l'absence d'interop empêche la persistance du mute ne tient plus. La persistance du mute avec ce même service reste à faire.
* Preuves reproductibles et liens vers les commits : `git log --oneline -S "AudioChannel"` ; `git log --oneline -S "LocalStorageService"`.
* Après correction éventuelle : résultat avant / après : avant, dépendance à un module JS ; après, seul le runtime Blazor reste en JavaScript. Mute toujours perdu au rechargement (pas de correction faite).
* Limites et points non vérifiés : volume non réglable (pré-mixé dans les WAV) ; boucle d'ambiance absente ; persistance du mute non implémentée.

---

## Revue 5 : montage des assets par `<Content Link>` dans le csproj

* Proposition et référence dans le dépôt : la branche `assets` publiait les SVG sous `wwwroot/assets/` via des `<Content Link>` dans `Naval.App.csproj`.
* Hypothèse à vérifier : les fichiers sont réellement servis en développement.
* Scénario, données ou commande : `fetch('/assets/...svg')` depuis la page, lecture du `content-type` ; `drawImage` sur un canvas.
* Résultat attendu avant exécution : 200 avec `image/svg+xml`.
* Erreur que ce contrôle pourrait détecter : route présente dans le manifeste mais fichier non servi, donc images cassées sans erreur visible au build.
* Résultat réellement observé : 200 mais corps vide et `content-type: null`, image en état `broken` sur le canvas. Second piège : une `url()` relative passée par `var()` se résolvait contre la feuille CSS (`/css/assets/…` en 404).
* Décision et justification : copie physique dans `wwwroot/assets/` et URL absolue `/assets/…` dans le style de `PowerBar`. Le binôme auteur de la branche défend le lien pour éviter la duplication de fichiers et propose une cible MSBuild de copie ; l'IA maintient la copie physique, retenue comme solution la plus simple et vérifiable.
* Preuves reproductibles et liens vers les commits : `git log --oneline -S "Content Link" -- src/Naval.App` ; refaire le `fetch` dans la console du navigateur.
* Après correction éventuelle : résultat avant / après : avant, corps vide et images cassées ; après, 39 SVG servis en `image/svg+xml`, 0 image cassée, sprites visibles aux trois états.
* Limites et points non vérifiés : comportement du montage d'origine non testé en publication Docker ; duplication de fichiers à surveiller si les sources SVG évoluent.
