# ADR-001 — Serveur autoritaire, vue filtrée par joueur

- **Date :** 2026-09-15
- **Statut :** accepté
- **Décideurs :** Alban, Laurent

## Contexte

Le mode solo (contre l'IA) et le mode en ligne partagent la même contrainte : le client ne doit
jamais recevoir la position des navires adverses non découverts, même en solo, même dans une
réponse intermédiaire. Il fallait décider dès le socle si le moteur de jeu vit côté client
(Blazor WebAssembly) ou côté serveur, car repousser ce choix à l'ajout du multijoueur aurait
obligé à réécrire le moteur de résolution des tirs à mi-parcours.

## Options envisagées

1. **Moteur côté client** — le front calcule lui-même les collisions tir/navire. Retours
   instantanés, pas d'aller-retour réseau. Mais la grille adverse complète doit exister quelque
   part dans le client pour que le calcul soit possible : un joueur ouvrant les DevTools voit la
   flotte adverse. Incompatible avec l'invariant de confidentialité.
2. **Serveur autoritaire** — `Naval.Api` (via `GameService`) détient le seul état complet de la
   partie ; chaque joueur ne reçoit qu'une vue filtrée (cases inconnues encodées `.`). Robuste
   contre la triche, au prix d'un aller-retour réseau par action, y compris en solo.

## Décision

Le serveur est seul dépositaire de l'état complet d'une partie ; chaque joueur ne reçoit qu'une
vue filtrée de sa grille et de la grille adverse.

## Justification

Le mode en ligne devait pouvoir remplacer le joueur IA par un second humain sans toucher au
chemin de résolution des tirs — ce qui n'est possible que si le mode solo utilise déjà un
serveur autoritaire dès le départ. C'est l'argument décisif : ce n'est pas une précaution de
sécurité abstraite, c'est ce qui a permis d'implémenter le multijoueur (hub `GameHub`) sans
modifier `GameService` ni `TurnResolver`.

## Conséquences

Facile : activer le multijoueur en ligne revient à brancher un deuxième humain sur le même
chemin de code — confirmé par l'implémentation de la partie en ligne, qui n'a pas touché au
moteur de tir. Toute nouvelle vue partielle (spectateur, futur replay) passe par un mapping
dédié dans `Contracts/Mapping/`, jamais par un accès direct à l'état interne.

Coûteux : chaque interaction, même en solo contre l'IA, implique un aller-retour vers
`Naval.Api` — latence non nulle qu'un moteur client n'aurait pas eue.
