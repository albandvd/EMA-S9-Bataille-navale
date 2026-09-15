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
