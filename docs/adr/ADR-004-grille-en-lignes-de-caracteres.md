# ADR-004 — Grille encodée en lignes de caractères plutôt qu'en tableau d'objets

- **Date :** 2026-09-15
- **Statut :** accepté
- **Décideurs :** Alban, Laurent

## Contexte

Chaque échange réseau (REST ou SignalR) doit transporter l'état visible d'une grille dont la
taille est paramétrable (`S-02`, de 8×8 à 12×12). Il faut un format de DTO à la fois compact,
lisible en JSON, et facile à afficher côté Blazor (`GridView`/`GridCell`).

## Options envisagées

1. **Tableau d'objets par case** (`CellDto[][]` avec `{ x, y, state }`) — explicite, indexable
   par propriété nommée. Mais une grille 10×10 pèse une centaine d'objets JSON alors que
   l'information par case tient sur un seul caractère.
2. **Grille en lignes de caractères** (`BoardViewDto(int Width, int Height, IReadOnlyList<string>
   Rows)`), une case = un caractère : `.` inconnue, `o` manquée, `x` touchée, `#` navire coulé.

## Décision

`BoardViewDto` transporte la grille sous forme de lignes de caractères (`Rows`), jamais de
tableau d'objets par case.

## Justification

Le volume envoyé à chaque `GameStateChanged`/`ShotResolved` est divisé par un facteur proche de
dix par rapport à un tableau d'objets, sans perte d'information puisqu'une case n'a qu'un état
parmi quatre. C'est l'argument décisif : le format reste lisible tel quel en soutenance (une
grille se lit comme du texte), et le mapping caractère → icône reste entièrement côté
`GridCell.razor`, jamais dans le contrat.

## Conséquences

Facile : ajouter un nouvel état visuel de case (par exemple une mine posée) ne demande qu'un
nouveau caractère, sans changer la forme du DTO.

Coûteux : le format ne convient qu'à des grilles rectangulaires à état de case unique — le
backlog `B-02` (grilles non rectangulaires, îles infranchissables) demanderait de revoir ce
contrat s'il était un jour retenu, une case ne pouvant alors plus être décrite par un seul
caractère.
