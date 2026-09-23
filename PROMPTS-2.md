# Échanges décisifs avec l'IA

Sélection des 18 échanges structurants, synthétisés depuis le journal automatique
(`.claude/hooks/journal-prompt.sh`). Quand le prompt n'a pas été consigné mot pour mot, la
demande est reformulée et signalée comme telle.

---

## 2026-09-15 — Backend du socle (S-01 à S-17)

* Outil / modèle si connu : Claude Code (modèle exact non consigné)
* Contexte : aucun code serveur ; specs dans `docs/01-fonctionnalites.md`, archi dans `docs/03-architecture.md`.
* Prompt réellement utilisé : « En te basant sur le fichier `./docs/01-fonctionnalites.md`, fais tout le backend pour la partie socle en respectant l'archi décrite dans `./docs/03-architecture.md`. »
* Réponse et hypothèses résumées : deux `Board` par joueur (Incoming/Outgoing) ; `GameEngine` statique pur ; `GameService` seul orchestrateur ; IA `HuntTargetAi` en damier `(x + y) % 2 == 0` en phase de chasse.
* Décision et justification : retenu. Le double plateau empêche `BuildTargetView` de voir les navires adverses non découverts ; le moteur pur se teste sans instancier de `Game` ; aucune règle dans les endpoints (> 15 lignes = alerte).
* Scénario ou commande de vérification : `dotnet build`, `dotnet test`, puis `POST /api/games`, `/fleet`, `/shots`, `/forfeit`.
* Résultat attendu, puis résultat observé : attendu 0 warning et endpoints conformes → observé 0 warning (TreatWarningsAsErrors), 37 tests verts, `201` à la création, `409` hors tour, `Abandoned` + `winnerId` au forfait.
* Erreur que ce contrôle pourrait détecter : tir accepté hors tour, fuite de la flotte adverse, règle dupliquée dans l'API.
* Preuves reproductibles et limites : `dotnet test` ; limite : l'IA n'est testée que sur ses règles, pas sur sa force de jeu.

---

## Non horodaté — `GameStateStore`, seul consommateur de `IGameApiClient`

* Outil / modèle si connu : Claude Code
* Contexte : front `Naval.App` développé en parallèle du moteur, sur un faux client.
* Prompt réellement utilisé : non consigné mot pour mot (demande : centraliser les appels API du front).
* Réponse et hypothèses résumées : une méthode par action dans le store, enveloppée par `RunAsync` qui capture `GameApiException` dans `LastError` et déclenche `StateChanged` dans un `finally`.
* Décision et justification : retenu ; gestion d'erreur en un seul point, l'UI ne reste jamais figée après un échec, pas de try/catch dans chaque composant Razor.
* Scénario ou commande de vérification : 2 tests dans `GameStateStoreTests.cs` (`JoinGameAsync` en échec, `FireAsync` en succès).
* Résultat attendu, puis résultat observé : `LastError` rempli et un seul `StateChanged` en échec, `TurnNumber` incrémenté en succès → observé, 7/7 verts.
* Erreur que ce contrôle pourrait détecter : écran bloqué sans notification après une erreur API.
* Preuves reproductibles et limites : `dotnet test --filter GameStateStoreTests` ; ne couvre pas le rendu des composants.

---

## Non horodaté — Victoire simulée dans `FakeGameApiClient`

* Outil / modèle si connu : Claude Code
* Contexte : `Naval.Api` pas encore disponible, écran `/result` inatteignable.
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : 1 tir sur 3 touché (`_shotCounter % 3 == 0`), victoire après 3 touches ; aucune règle réelle modélisée, choix commenté dans le code.
* Décision et justification : retenu comme échafaudage temporaire ; en revue, ajout d'un refus `GameNotInProgress` (409) sur partie terminée et remise à zéro des compteurs dans `CreateGameAsync`.
* Scénario ou commande de vérification : test `FireAsync_marks_the_game_finished_once_the_hit_threshold_is_reached` (9 tirs).
* Résultat attendu, puis résultat observé : statut `Finished` et bon `WinnerId` → observé.
* Erreur que ce contrôle pourrait détecter : « Rejouer » qui termine la nouvelle partie dès le premier tir (compteurs non réinitialisés).
* Preuves reproductibles et limites : le garde-fou 409 n'a pas de test dédié.

---

## Non horodaté — Encodage des grilles et non-fuite des positions adverses

* Outil / modèle si connu : Claude Code
* Contexte : règle absolue du projet : aucune position adverse non découverte côté client.
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : `BoardViewDto.Rows` encode chaque case en un caractère ; `GridCell`/`GridView` ne lisent que ce caractère ; `ShipStateDto.Cells` vaut `null` pour la flotte adverse sauf navire coulé.
* Décision et justification : retenu ; la règle est portée par le contrat DTO, pas par la confiance dans le serveur.
* Scénario ou commande de vérification : relecture de `Responses.cs`, `GridCell.razor`, `GridView.razor` ; test `FireAsync_only_reveals_the_targeted_cell`.
* Résultat attendu, puis résultat observé : un seul caractère modifié par tir → observé.
* Erreur que ce contrôle pourrait détecter : révélation de cases non ciblées sur la grille adverse.
* Preuves reproductibles et limites : vérifié sur le faux client ; le serveur réel est couvert plus tard (vue spectateur).

---

## Non horodaté — Lacune de contrat : `GetFleetPresetAsync`

* Outil / modèle si connu : Claude Code
* Contexte : `Deploy.razor` inerte, aucun navire sélectionnable.
* Prompt réellement utilisé : non consigné mot pour mot (revue finale d'assemblage des pages).
* Réponse et hypothèses résumées : `ShipTray` recevait `SelfViewDto.Fleet` (navires déjà placés, vide en `AwaitingDeployment`) ; ajout de `IGameApiClient.GetFleetPresetAsync`, exposé via `GameStateStore.FleetPreset`, appelé depuis `Lobby.StartGame()`.
* Décision et justification : retenu plutôt qu'un catalogue codé en dur dans le composant (interdit par `CLAUDE.md`).
* Scénario ou commande de vérification : relecture Lobby → Store → Deploy → ShipTray ; `dotnet build`.
* Résultat attendu, puis résultat observé : preset non vide dans `ShipTray.Fleet` → observé, build vert.
* Erreur que ce contrôle pourrait détecter : écran de déploiement vide.
* Preuves reproductibles et limites : pas de vérification navigateur à ce stade.

---

## Non horodaté — Bug « impossible de placer un bateau » (CSS de `ShipTray`)

* Outil / modèle si connu : Claude Code ; Playwright indisponible (Chromium absent)
* Contexte : signalement utilisateur, clic sans effet visible.
* Prompt réellement utilisé : non consigné mot pour mot (signalement du bug).
* Réponse et hypothèses résumées : `grep -rn "ship-tray" wwwroot/css/` → aucune règle CSS ; le clic fonctionnait mais rien ne s'affichait.
* Décision et justification : ajout des règles CSS manquantes ; retrait du navire de la liste une fois placé (`_placedIds`) comme retour visuel.
* Scénario ou commande de vérification : `grep`, `dotnet build`, `dotnet test`.
* Résultat attendu, puis résultat observé : build vert, aucune régression → observé, 7/7.
* Erreur que ce contrôle pourrait détecter : composant fonctionnel mais visuellement mort.
* Preuves reproductibles et limites : validation visuelle laissée à l'utilisateur.

---

## Non horodaté — Navires placés invisibles et grilles non identifiables

* Outil / modèle si connu : Claude Code
* Contexte : second signalement utilisateur.
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : `PlaceFleetAsync` remplissait `Self.Fleet` mais jamais `Self.Board.Rows` ; ajout de `PaintFleet` (`'S'` sur le plateau propre) et de titres `<h2>` « Votre grille » / « Grille adverse ».
* Décision et justification : retenu ; ne touche que `Self.Board`, jamais `Opponent.TargetBoard`, donc l'invariant de non-fuite tient.
* Scénario ou commande de vérification : test `PlaceFleetAsync_paints_the_ship_cells_onto_the_player_s_own_board`.
* Résultat attendu, puis résultat observé : exactement 2 cases `'S'` pour un destroyer en (1,2) → observé, 8/8.
* Erreur que ce contrôle pourrait détecter : sur-peinture ou peinture de la grille adverse.
* Preuves reproductibles et limites : le test échoue sans le correctif ; pas de validation navigateur.

---

## Non horodaté — Défense en profondeur du placement, puis annulation

* Outil / modèle si connu : Claude Code
* Contexte : chevauchement possible, aucun aperçu pendant la pose.
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : validation bornes + chevauchement côté `Deploy` et côté faux client (`OutOfBounds`, `OverlappingShips`), aperçu local de la flotte.
* Décision et justification : annulé par le binôme ; concevoir cette UX sur un faux client voué à disparaître était prématuré. Conservés : CSS, `_placedIds`, `PaintFleet`, titres `<h2>`.
* Scénario ou commande de vérification : `dotnet build`, `dotnet test`.
* Résultat attendu, puis résultat observé : 10/10 avec la validation, retour à 8/8 après annulation → observé.
* Erreur que ce contrôle pourrait détecter : régression sur les correctifs indépendants conservés.
* Preuves reproductibles et limites : chevauchement et absence d'aperçu laissés comme problèmes connus jusqu'à l'arrivée de `Naval.Api`.

---

## Non horodaté — Branchement de `GameApiClient` sur `Naval.Api`

* Outil / modèle si connu : Claude Code
* Contexte : `Naval.Api` mergé, `GameApiClient` encore en `NotImplementedException`.
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : point unique `SendAsync<T>` ; `JsonSerializerOptions` explicites (Web + `JsonStringEnumConverter`) ; constante locale `X-Player-Token` ; `GetFleetPresetAsync` par filtrage de `GET /api/catalog/fleets` ; ports réalignés sur 7001/7002.
* Décision et justification : retenu ; bug de contrat corrigé (`ForfeitAsync` renvoie `GameOverDto`, pas `GameStateDto`, conformément à `openapi.yaml`) ; bug backend `Player2.IsReady` signalé sans toucher au code de l'autre moitié du binôme.
* Scénario ou commande de vérification : `dotnet test` ; appels `curl` sur l'API démarrée (création, 401 `MISSING_TOKEN`, catalogues, flotte, forfait).
* Résultat attendu, puis résultat observé : 45/45 ; formes JSON conformes ; `/shots` en 409 `GAME_NOT_IN_PROGRESS` (conséquence du bug backend).
* Erreur que ce contrôle pourrait détecter : désérialisation silencieuse en valeurs par défaut, CORS bloqué, type de retour faux.
* Preuves reproductibles et limites : tir non vérifiable de bout en bout avant correction backend ; `/powers` en 404 (pas d'endpoint).

---

## Non horodaté — Correction backend : la partie solo ne démarrait jamais

* Outil / modèle si connu : Claude Code + serveur MCP `playwright-brave`
* Contexte : partie solo bloquée en `AwaitingDeployment`.
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : `StartBattle` exige `Player1.IsReady && Player2.IsReady` ; l'IA n'était jamais marquée prête ; correctif d'une ligne `p2.IsReady = true;`.
* Décision et justification : retenu ; aucun contrat modifié.
* Scénario ou commande de vérification : 2 tests `GameServiceTests` vérifiés rouges (fix en stash) puis verts ; parcours Lobby → Deploy → Battle dans Brave.
* Résultat attendu, puis résultat observé : statut `InProgress`, navigation vers `/battle` → observé, 47/47.
* Erreur que ce contrôle pourrait détecter : régression du démarrage solo.
* Preuves reproductibles et limites : `dotnet test --filter GameServiceTests` ; tirs non testés au-delà du démarrage.

---

## Non horodaté — Infra Docker (`infra/docker/`, `docker-compose.yml`)

* Outil / modèle si connu : Claude Code
* Contexte : besoin d'un déploiement conteneurisé.
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : image `naval-api` (aspnet:10.0) et `naval-app` (nginx 1.27-alpine) ; `ApiBaseUrl` et `Cors__AppOrigin` injectés à l'exécution (`envsubst`), jamais au build.
* Décision et justification : retenu ; le code Blazor WASM tourne dans le navigateur, un nom de service Docker y serait injoignable ; une image, plusieurs environnements.
* Scénario ou commande de vérification : `docker compose up --build -d`, `curl /health`, `curl /appsettings.json`, en-têtes `.wasm`, fallback `/lobby`, préflight CORS.
* Résultat attendu, puis résultat observé : tout conforme ; origine `http://evil.example` → 204 sans en-tête `Access-Control-Allow-*`. Premier build en échec sur `groupadd --gid 1000`, corrigé.
* Erreur que ce contrôle pourrait détecter : URL interne figée côté navigateur, MIME WASM incorrect, CORS trop permissif.
* Preuves reproductibles et limites : pas de persistance, pas de TLS, pas de CI (documenté dans `infra/docker/README.md`).

---

## Non horodaté — Retour visuel de placement (S-04/S-05) et fusion `assets`

* Outil / modèle si connu : Claude Code ; SDK .NET 10 installé via `winget`
* Contexte : condition de reprise remplie (API réelle mergée, solo jouable).
* Prompt réellement utilisé : non consigné mot pour mot.
* Réponse et hypothèses résumées : `ShipTray` émet `OnSelectionChanged`/`OnOrientationChanged` ; `GridCell` gagne un `Preview` (valide/refusé) ; `Deploy.razor` valide avant `ConfirmPlacement()` ; le serveur reste seule autorité.
* Décision et justification : retenu ; aide visuelle pure, aucune validation dupliquée dans un faux client.
* Scénario ou commande de vérification : pilotage du DOM sur 5 scénarios (valide, hors grille, refus au clic, placement accepté, chevauchement).
* Résultat attendu, puis résultat observé : classes `grid-cell--preview-valid/invalid` correctes, bannière d'erreur, navire conservé dans le tiroir → observé, 47/47.
* Erreur que ce contrôle pourrait détecter : « retrait fantôme » d'un navire refusé.
* Preuves reproductibles et limites : pas de captures (pane non composité) ; validation Razor sans test automatisé.

---

## Non horodaté — Refonte visuelle « Navcom » en CSS pur

* Outil / modèle si connu : Claude Code, branche `ui`
* Contexte : session exploratoire, entrée rédigée a posteriori.
* Prompt réellement utilisé : « Montre-moi l'étendue de tes capacités en matière d'UI. Ta mission est de repousser les limites du design d'interface. »
* Réponse et hypothèses résumées : console deux écrans sans aucune image (dégradés, pseudo-éléments, scanlines, radar en `conic-gradient`) ; trio de polices Google Fonts ; libellé « Navcom », jamais « DS ».
* Décision et justification : retenu ; applique « CSS d'abord » (`docs/04-assets.md` §2) et évite la marque Nintendo (§9) ; `prefers-reduced-motion` respecté.
* Scénario ou commande de vérification : parcours DOM titre → bataille avec 8 tirs ; styles calculés ; `document.fonts`.
* Résultat attendu, puis résultat observé : animations actives, polices chargées → observé ; seule erreur console : 404 bénin de `Naval.App.styles.css`.
* Erreur que ce contrôle pourrait détecter : animation inactive, police non chargée.
* Preuves reproductibles et limites : polices non auto-hébergées en `.woff2`.

---

## Non horodaté — Navires posés invisibles pendant le déploiement

* Outil / modèle si connu : Claude Code
* Contexte : aperçu visible au survol, mais navire disparu une fois posé.
* Prompt réellement utilisé : « Lors de la phase de planification j'arrive bien à voir où je pose mon bateau, en revanche une fois posé je ne le vois plus sur l'écran. »
* Réponse et hypothèses résumées : le serveur ignore les placements avant `PlaceFleetRequest` ; `BuildDisplayBoard` superpose les placements locaux (`'S'`).
* Décision et justification : retenu ; affichage pur, autorité serveur inchangée.
* Scénario ou commande de vérification : comptage des `grid-cell--ship` après chaque pose.
* Résultat attendu, puis résultat observé : 5, puis 9, puis 12 cases → observé.
* Erreur que ce contrôle pourrait détecter : placement à l'aveugle.
* Preuves reproductibles et limites : vérifié par DOM, pas par test unitaire.

---

## Non horodaté — Sprites de navires et icônes de pouvoirs

* Outil / modèle si connu : Claude Code
* Contexte : assets SVG mergés mais non consommés.
* Prompt réellement utilisé : « Arrives-tu maintenant à intégrer les assets à tout ça ? »
* Réponse et hypothèses résumées : overlay en grille CSS jumelle ; sprites construits depuis `Self.Fleet` et `Opponent.SunkShips` uniquement ; icônes en `mask-image` colorées par état.
* Décision et justification : retenu ; deux bugs corrigés : `<Content Link>` du csproj servait des réponses vides (remplacé par une copie dans `wwwroot/assets/`) et `url()` relative via `var()` résolue contre la feuille CSS (passage en `/assets/…`).
* Scénario ou commande de vérification : `fetch` des SVG, `drawImage` sur canvas, mesure du sprite vertical.
* Résultat attendu, puis résultat observé : avant corps vide et `content-type: null` ; après `image/svg+xml`, 39 SVG servis, 0 image cassée, sprite vertical 28×113 px.
* Erreur que ce contrôle pourrait détecter : images cassées silencieuses, fuite d'une position adverse via un sprite.
* Preuves reproductibles et limites : défaut du montage signalé au binôme auteur de la branche `assets`.

---

## Non horodaté — Bruitages 8-bit en C# pur

* Outil / modèle si connu : Claude Code
* Contexte : aucun son ; première version via module JS.
* Prompt réellement utilisé : « Et le son ? Tu peux générer des bruitages 8-bit ? » puis « Je vois que tu as utilisé du JS pour le son, peux-tu rester uniquement en C# ? »
* Réponse et hypothèses résumées : 12 WAV synthétisés par un générateur C# hors solution ; lecture via `AudioService` + `AudioChannel.razor` (`<audio autoplay>`, `@key`, plafond de 6 sons).
* Décision et justification : retenu ; créations originales (pas de licence), plus d'interop JS ; compromis : mute non persisté.
* Scénario ou commande de vérification : `decodeAudioData` ; `MutationObserver` sur `ended` pendant une partie ; `grep IJSRuntime`.
* Résultat attendu, puis résultat observé : 30 sons joués dans l'ordre, aucun avertissement d'autoplay → observé, 47/47.
* Erreur que ce contrôle pourrait détecter : son bloqué par la politique d'autoplay, DOM saturé d'éléments audio.
* Preuves reproductibles et limites : boucle `ambient-sea` exclue ; mute perdu au F5.

---

## Non horodaté — Écrans de la console à taille fixe

* Outil / modèle si connu : Claude Code + Playwright MCP
* Contexte : la console change de taille selon le contenu.
* Prompt réellement utilisé : « A de nombreux moment la taille de l'écran de la ds change en fonction de ce qui est affiché dedans. J'aimerai que l'écran et la ds garde tout le temps la meme taille quelque soit le contenu qui est affiché dedans. »
* Réponse et hypothèses résumées : `aspect-ratio: 16 / 10` + `container-type: size` ; contenu absolu avec défilement interne ; grilles latérales identiques `92px 1fr 92px` ; bataille en deux colonnes.
* Décision et justification : retenu ; 4:3 testé puis écarté (console de 1 310 px de haut).
* Scénario ou commande de vérification : `getBoundingClientRect` sur `.ds-screen`/`.ds-console` à chaque page, à 1 280 et 400 px.
* Résultat attendu, puis résultat observé : avant 824×619 / 704×529 px ; après 651×409 px pour les deux dalles sur les quatre pages.
* Erreur que ce contrôle pourrait détecter : effondrement de la console (328 px), débordement horizontal sur mobile.
* Preuves reproductibles et limites : captures dans `.playwright-mcp/` ; léger défilement interne à 400 px, assumé.

---

## Non horodaté — Multijoueur en ligne (E-A, E-01 à E-09)

* Outil / modèle si connu : Claude Code
* Contexte : E-01 à E-03 existaient en REST, sans temps réel ni front.
* Prompt réellement utilisé : « en te basant sur le fichier docs/01-fonctionnalites.md met en place le E-A Multijoueur en ligne »
* Réponse et hypothèses résumées : `GameNotifier` seul point de diffusion SignalR ; groupes `game:{id}` et `player:{id}` ; `PresenceService` (grâce 60 s) et `TurnTimeoutService` via `IServiceScopeFactory` ; timeout réutilisant `RandomAi` ; spectateur en polling REST 2,5 s.
* Décision et justification : retenu ; `GameService` reste ignorant de SignalR ; 3 bugs corrigés (`FleetPreset` absent de `GameStateDto`, `Player2.IsConnected` à `true` par défaut, `GameStateChanged` non poussé après abandon).
* Scénario ou commande de vérification : `dotnet test` (dont `GameHubTests` avec `WebApplicationFactory` + `HubConnection`) ; serveur réel avec timer à 5 s ; `GET /spectate`.
* Résultat attendu, puis résultat observé : 69/69 verts ; deux tirs `(temps écoulé)` enchaînés sans action ; grilles spectateur à `.` avant déploiement.
* Erreur que ce contrôle pourrait détecter : fuite vers un spectateur, service scoped capturé par un singleton, fin de partie non propagée.
* Preuves reproductibles et limites : aucune validation visuelle humaine du nouveau front (à faire à deux onglets).
