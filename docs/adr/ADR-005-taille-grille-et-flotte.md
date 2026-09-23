# ADR-005 — Taille de grille et composition de flotte retenues

- **Date :** 2026-09-16
- **Statut :** accepté
- **Décideurs :** Alban, Laurent

## Contexte

`S-02` exige une grille paramétrable et `S-03` une flotte paramétrable par preset. Il fallait
malgré tout un choix par défaut jouable dès le premier écran (Lobby), et un jeu de presets qui
couvre à la fois une partie de démonstration standard et une partie rapide.

## Options envisagées

1. **Une seule taille et une seule flotte fixes** (10×10, 5 navires classiques) — le plus simple
   à équilibrer, mais contredit `S-02`/`S-03` qui exigent explicitement la paramétrabilité.
2. **Grille et flotte totalement libres**, saisies à la main — flexibilité maximale, mais aucune
   garantie qu'une combinaison saisie soit jouable (flotte qui ne rentre pas dans la grille), et
   une validation disproportionnée pour un projet scolaire.
3. **Grille paramétrable avec une valeur par défaut (10×10), combinée à des presets de flotte
   validés à l'avance** : `Classic` (5 navires, 17 cases, grille minimale 10) et `Skirmish`
   (3 navires, 9 cases, grille minimale 8), chaque preset portant sa propre contrainte
   `MinGridSize`.

## Décision

Grille par défaut 10×10 (modifiable), flotte choisie parmi les presets `Classic` et `Skirmish`
définis dans `FleetPresets`, chacun avec sa taille de grille minimale.

## Justification

Coupler chaque preset à un `MinGridSize` déplace la validation « la flotte rentre-t-elle dans la
grille » du domaine métier (à chaque partie) vers une donnée statique du preset (une fois, à sa
définition). C'est l'argument décisif : ça évite une flotte invalide sans sacrifier la
paramétrabilité imposée par `S-02`/`S-03`, et deux presets suffisent à couvrir une partie
standard et une partie rapide sans complexifier l'écran de lobby.

## Conséquences

Facile : ajouter un troisième preset ne touche que `FleetPresets.All`, jamais `GameService` ni
les endpoints de déploiement.

Coûteux : le catalogue de presets est codé en dur côté serveur — pas d'éditeur de flotte
personnalisée (backlog `B-04`). Modifier la composition d'un preset existant change le contrat
implicite d'une partie déjà en cours au moment du déploiement.
