# ADR-006 — Économie des pouvoirs (énergie / tours de charge)

- **Date :** 2026-09-15
- **Statut :** accepté
- **Décideurs :** Alban, Laurent

## Contexte

Le catalogue de pouvoirs (`docs/02-pouvoirs.md`) propose des effets très hétérogènes
(révélation d'information, tirs multiples, destruction de zone). Il fallait une seule monnaie
compréhensible par le joueur, capable de limiter aussi bien un pouvoir bon marché utilisable en
boucle qu'un pouvoir dévastateur utilisable une fois par partie. Le premier jet — une bombe
nucléaire à « 30 tours sans jouer puis victoire instantanée » — s'est révélé injouable au
calcul : contre une IA qui coule une flotte classique en 40 à 60 tirs, sacrifier 30 tours offre
l'équivalent de la moitié de la partie à l'adversaire, sans certitude de victoire à l'arrivée.

## Options envisagées

1. **Une seule ressource (énergie)**, gagnée au fil des tours et des touches, payant tous les
   pouvoirs indistinctement — simple, mais ne distingue pas un pouvoir fréquent bon marché d'un
   pouvoir dévastateur, et n'empêche pas le spam d'un pouvoir unique une fois rechargé.
2. **Victoire instantanée après un nombre fixe de tours de charge** (proposition initiale pour
   la bombe) — spectaculaire sur le papier, mais le calcul (grille 10×10, 17 cases de navires,
   40 à 60 tirs pour couler une flotte) montre que 30 tours de charge perdent la partie avant
   l'explosion : le pouvoir ne serait en pratique jamais joué.
3. **Trois ressources croisées** : **énergie** (+1/tour, +2/touche, +3/navire coulé) pour les
   pouvoirs courants, **tours de charge** (renoncer à tirer pendant N tours, porteur révélé au
   tour 5 pour les charges longues, annulable par Sabotage) pour les pouvoirs dévastateurs, et
   **charges (`UsesLeft`)** fixées au départ pour empêcher le spam d'un pouvoir à usage unique.

## Décision

Modèle à trois ressources (énergie / tours de charge / charges). La Frappe Orbitale est ramenée
à 10 tours de charge pour une zone 5×5 (au lieu de 30 tours et victoire instantanée), avec
porteur obligatoire dès `ChargeTurns ≥ 3`.

## Justification

Le calcul du coût d'opportunité (tours de charge perdus contre tirs adverses gratuits) est
l'argument qui a fait rejeter la proposition initiale — un pouvoir « ultime » jamais joué est le
pire résultat possible pour l'équilibrage. Rendre le porteur vulnérable pendant la charge
transforme un pouvoir « je clique et j'attends » en décision tactique : l'adversaire qui devine
le porteur a une raison de concentrer ses tirs. Sabotage (`P-21`) donne en face un contre-jeu
explicite plutôt qu'une simple attente passive.

## Conséquences

Facile : un nouveau pouvoir s'exprime entièrement par cinq champs (`EnergyCost`, `ChargeTurns`,
`Cooldown`, `MaxUses`, `RequiresCarrier`) dans `PowerCatalog`, sans code dédié pour la ressource
qu'il consomme.

Coûteux : l'équilibrage reste empirique et a déjà bougé une fois en cours de projet (Sonar
ramené de rayon 4 à 3, Tsar Bomba ajouté à 40 d'énergie en 5×5 le 2026-09-23). Toute nouvelle
entrée du catalogue doit être rejouée mentalement contre le calcul de tours ci-dessus, pas
seulement comparée aux pouvoirs voisins.
