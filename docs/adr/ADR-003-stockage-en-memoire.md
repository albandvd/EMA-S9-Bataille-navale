# ADR-003 — Stockage en mémoire derrière `IGameStore`

- **Date :** 2026-09-16
- **Statut :** accepté
- **Décideurs :** Alban, Laurent

## Contexte

Le projet interdit d'ajouter une base de données tant que `E-27` n'est pas explicitement
demandée, mais il faut malgré tout persister l'état de chaque partie en cours (créée, rejointe,
en cours, terminée) pendant sa durée de vie.

## Options envisagées

1. **Base de données (SQLite/Postgres) dès le départ** — persistance entre redémarrages, mais
   dépendance d'installation supplémentaire le jour de la démo et migrations à maintenir, pour
   un bénéfice nul : une partie dure environ quinze minutes et personne ne la reprend le
   lendemain.
2. **État en mémoire manipulé ad hoc dans les endpoints** (dictionnaire statique) — zéro
   abstraction, mais impossible à tester isolément et impossible à faire évoluer sans réécrire
   chaque endpoint.
3. **Interface `IGameStore`**, implémentée par `InMemoryGameStore`
   (`ConcurrentDictionary<Guid, Game>`), injectée dans `GameService`.

## Décision

`IGameStore`, implémenté par `InMemoryGameStore` (`ConcurrentDictionary<Guid, Game>`), unique
point d'accès à l'état des parties.

## Justification

La durée de vie d'une partie rend une base de données coûteuse à installer pour un bénéfice nul
le jour de la démo. L'argument décisif est la réversibilité : l'interface encapsule ce choix
pour qu'il reste remplaçable sans toucher `GameService` ni les endpoints le jour où `E-27` est
demandée — seule l'implémentation change.

## Conséquences

Facile : brancher SQLite plus tard ne touchera que `InMemoryGameStore` et son enregistrement DI.

Coûteux : `ConcurrentDictionary` protège seulement les accès à la table des parties (créer,
lister, retirer), pas les mutations *à l'intérieur* d'une partie. C'est pour ça que `Game`
porte son propre `SemaphoreSlim` (`Game.Lock`), acquis par `GameService` autour de chaque
méthode qui mute une partie (tir, placement, pouvoir, forfait, tour IA — neuf points d'entrée
au total) : deux requêtes concurrentes sur la même partie (un tir et un abandon simultanés, par
exemple) s'exécutent en séquence plutôt que de s'entrelacer. C'est l'invariant exigé par
`CLAUDE.md` (« chaque mutation d'une partie est sérialisée par un `SemaphoreSlim` propre à
cette partie »), et il est bien en place. Reste un angle mort : le verrou vit sur l'objet
`Game` en mémoire, donc il ne protégerait plus rien le jour où `IGameStore` cesserait d'être un
simple dictionnaire in-process (plusieurs instances de l'API derrière un load-balancer, par
exemple) — un problème qui ne se pose pas tant qu'`E-27` n'est pas demandée.
