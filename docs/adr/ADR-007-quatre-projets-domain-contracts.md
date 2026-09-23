# ADR-007 — Quatre projets, `Domain` et `Contracts` dans la même bibliothèque

- **Date :** 2026-09-15
- **Statut :** accepté
- **Décideurs :** Alban, Laurent

## Contexte

La consigne du module impose exactement quatre projets. Le jeu a pourtant deux besoins qui,
dans un autre contexte, justifieraient chacun leur propre bibliothèque : des règles de jeu
pures et testables sans dépendance, et des DTO sérialisables consommés à la fois par l'API
(serveur) et par le front Blazor WebAssembly (client).

## Options envisagées

1. **Cinq projets** — séparer `Naval.Domain` et `Naval.Contracts` en deux bibliothèques
   distinctes. Respecte au mieux la responsabilité unique, mais dépasse la consigne des quatre
   projets exactement pour un problème de couplage qui peut être résolu autrement.
2. **Domain et Contracts fusionnés sans distinction de dossier** — reste à quatre projets, mais
   rien n'empêche alors une règle de jeu de référencer un DTO (ou l'inverse) par erreur ; la
   frontière n'est plus qu'une convention orale entre les deux membres du binôme.
3. **Un seul projet `Naval.Shared`, avec deux dossiers imposant la même règle qu'une séparation
   en projets aurait imposée** : `Domain/` (aucune dépendance externe, ne référence jamais
   `Contracts/`) et `Contracts/` (DTO sérialisables, mapping vers/depuis `Domain/` dans
   `Contracts/Mapping/`).

## Décision

`Naval.Shared` unique, avec `Domain/` et `Contracts/` comme deux dossiers séparés par une règle
de dépendance à sens unique (`Contracts → Domain`, jamais l'inverse), plutôt que deux projets.

## Justification

La contrainte « exactement quatre projets » n'est pas négociable. Scinder Domain/Contracts en
deux assemblies n'aurait rien apporté que la même règle de dépendance ne peut pas déjà imposer
entre deux dossiers d'un même projet : la limite réelle à préserver n'est pas la frontière
d'assembly, c'est le sens du couplage — le moteur de jeu ne doit jamais devenir l'esclave d'un
format JSON.

## Conséquences

Facile : un seul projet à référencer depuis `Naval.Api` et `Naval.App`, et un seul projet
garanti sans dépendance ASP.NET Core, donc compatible WebAssembly.

Coûteux : rien n'empêche *techniquement* le compilateur de laisser passer un fichier ajouté par
erreur dans `Domain/` qui référencerait `Contracts/` — contrairement à deux assemblies séparées,
cette règle n'est pas vérifiée par le compilateur mais par la relecture de PR obligatoire
(`Naval.Shared/Contracts` est copropriété du binôme, cf. `docs/03-architecture.md` §6).
