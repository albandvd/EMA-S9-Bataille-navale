# Échanges décisifs avec l'IA

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

Ce fichier est un livrable noté. Les entrées sont créées automatiquement par le hook
`.claude/hooks/journal-prompt.sh` à chaque prompt envoyé à Claude Code, puis complétées à la
main ou via la commande `/journal`.

Voir `.claude/skills/naval-journal/SKILL.md` pour le format attendu de chaque champ.

**Contrôle avant rendu :**

```bash
grep -c 'TODO' PROMPTS.md                    # doit renvoyer 0
grep -c 'Statut : `brouillon`' PROMPTS.md    # doit renvoyer 0
```
