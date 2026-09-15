# 01 — Fonctionnalités

Trois niveaux : **S** = socle non négociable (sans ça, pas de démo), **E** = enrichissement
(c'est là que se joue la note), **B** = bonus si le temps le permet.

Chaque ligne a un identifiant stable (`S-01`, `E-07`…). Utilisez-les dans les noms de branches,
les titres de PR et les commits : `feat(E-07): sonar côté serveur`. Ça rend la traçabilité
gratuite le jour de la soutenance.

---

## S — Socle

| ID | Fonctionnalité | Où ça vit | Critère d'acceptation |
|---|---|---|---|
| S-01 | Créer une partie solo (vs IA) | API + Shared | `POST /api/games` renvoie un `gameId` + `playerToken`, état `AwaitingDeployment` |
| S-02 | Grille paramétrable (largeur, hauteur) | Domain | Une partie 8×8 et une partie 12×12 fonctionnent sans changement de code |
| S-03 | Flotte paramétrable (preset) | Domain | Au moins 2 presets : `Classic` (5 navires) et `Skirmish` (3 navires) |
| S-04 | Placement manuel d'un navire | Domain + App | Drag/clic + rotation, refus visuel si invalide |
| S-05 | Validation du placement | Domain | Hors grille, chevauchement, navire en double → rejeté avec motif précis |
| S-06 | Placement aléatoire | Domain | `POST /api/games/{id}/fleet/random` propose une flotte valide, déterministe si seed fourni |
| S-07 | Tir sur une coordonnée | Domain + API | Renvoie `Miss` / `Hit` / `Sunk`, refuse une case déjà tirée |
| S-08 | Alternance des tours | Domain | Un joueur ne peut pas tirer hors de son tour → `409 Conflict` |
| S-09 | Détection de fin de partie | Domain | Tous les navires coulés → statut `Finished` + `winnerId` |
| S-10 | Vue asymétrique | API | La réponse ne contient **jamais** la position des navires adverses non découverts |
| S-11 | IA niveau 1 — aléatoire | Domain | Tire sur une case non encore jouée, uniformément |
| S-12 | IA niveau 2 — chasse/traque | Domain | Après un `Hit`, cible les 4 cases adjacentes ; après 2 hits alignés, suit l'axe |
| S-13 | Affichage double grille | App | Grille adverse (tirs) + grille propre (dégâts reçus) |
| S-14 | Journal des coups | App + API | Liste chronologique lisible, rejouable |
| S-15 | Abandon | API | `POST /api/games/{id}/forfeit` → l'adversaire gagne |
| S-16 | Tests unitaires du moteur | Tests | ≥ 25 tests, couverture du Domain ≥ 80 % |
| S-17 | Gestion d'erreurs homogène | API | Toutes les erreurs en `ProblemDetails` RFC 9457 avec un `code` métier |

---

## E — Enrichissements

### E-A · Multijoueur en ligne

| ID | Fonctionnalité | Détail |
|---|---|---|
| E-01 | Création d'une partie privée avec code | Code à 6 caractères (alphabet sans `0/O/1/I`), partageable à l'oral |
| E-02 | Rejoindre par code | `POST /api/games/join` → attribue le slot 2 |
| E-03 | Salon public / matchmaking | `GET /api/games/open` liste les parties en attente, tri par ancienneté |
| E-04 | Temps réel via SignalR | Hub `/hub/game` : l'adversaire voit le tir sans rafraîchir |
| E-05 | Présence & déconnexion | Statut `IsConnected`, message « adversaire déconnecté », grâce de 60 s |
| E-06 | Reconnexion | Le `playerToken` conservé en `localStorage` permet de reprendre la partie |
| E-07 | Timer de tour | 30 s par tour ; à l'expiration, tir aléatoire automatique ou passe |
| E-08 | Chat / emotes | 8 emotes prédéfinies (pas de texte libre = pas de modération à gérer) |
| E-09 | Mode spectateur | `GET /api/games/{id}/spectate` : les deux grilles, sans possibilité d'agir |

### E-B · Pouvoirs

| ID | Fonctionnalité | Détail |
|---|---|---|
| E-10 | Économie d'énergie | +1/tour, +2 sur touche, +3 sur navire coulé |
| E-11 | Chargement pluri-tours | Un pouvoir « en charge » consomme les tours de tir |
| E-12 | Porteur vulnérable | Une charge longue est portée par un navire ; si coulé → charge perdue |
| E-13 | Loadout | Chaque joueur choisit 3 pouvoirs avant la partie |
| E-14 | Cooldown & charges limitées | `UsesLeft`, `CooldownRemaining` |
| E-15 | Contre-jeu | Au moins un pouvoir capable d'annuler une charge adverse |
| E-16 | Catalogue exposé | `GET /api/catalog/powers` : le front ne code aucune règle en dur |

Détail complet dans [`02-pouvoirs.md`](02-pouvoirs.md).

### E-C · Interface style DS

| ID | Fonctionnalité | Détail |
|---|---|---|
| E-17 | Layout double écran | Écran haut = plateau, écran bas = actions, cadre de console au milieu |
| E-18 | Curseur/viseur | Navigation clavier (flèches + entrée) **et** souris/tactile |
| E-19 | HUD | Jauge d'énergie, barres de charge, compteur de navires restants |
| E-20 | Animations | Gerbe d'eau (miss), explosion (hit), onde de sonar, séquence de tir |
| E-21 | Sons | 8 SFX + 1 boucle d'ambiance, bouton mute persistant |
| E-22 | Transitions d'état | Écrans titre / déploiement / bataille / victoire-défaite |
| E-23 | Responsive | Écrans verticaux : les deux « écrans » empilés ; horizontaux : côte à côte |
| E-24 | Accessibilité | Focus visible, `aria-label` sur chaque case, contraste AA, option « réduire les animations » |

### E-D · Technique

| ID | Fonctionnalité | Détail |
|---|---|---|
| E-25 | IA niveau 3 — probabiliste | Carte de densité par placements possibles restants |
| E-26 | Replay | Rejeu tour par tour depuis le flux d'événements, export JSON |
| E-27 | Persistance | `IGameStore` → implémentation SQLite via EF Core, en plus de l'in-memory |
| E-28 | Validation FluentValidation | Toutes les requêtes entrantes validées, cf. diapo 51 du référentiel |
| E-29 | Service gRPC | `NavalReplay` en streaming serveur — couvre la partie gRPC du référentiel |
| E-30 | Tests d'intégration | `WebApplicationFactory` : scénario complet création → victoire |
| E-31 | CI GitHub Actions | build + test sur chaque PR, blocage du merge si rouge |
| E-32 | Docker Compose | API + front servis en une commande |
| E-33 | Ménage des parties mortes | `BackgroundService` supprimant les parties inactives > 2 h |
| E-34 | Observabilité | Logs structurés + `/health` + compteur de parties actives |

---

## B — Bonus

| ID | Fonctionnalité |
|---|---|
| B-01 | Tournoi à 4 joueurs (arbre à élimination) |
| B-02 | Grilles non rectangulaires (îles infranchissables) |
| B-03 | Brouillard de guerre dégressif (les tirs anciens redeviennent flous) |
| B-04 | Éditeur de flotte personnalisée (budget de points) |
| B-05 | Statistiques persistantes par pseudo (parties, ratio, précision) |
| B-06 | i18n FR/EN |
| B-07 | PWA installable + jouable hors ligne en solo |
| B-08 | Mode « campagne » : 5 combats vs IA de difficulté croissante |
| B-09 | Bot API publique (un étudiant peut brancher son propre bot sur l'API) |

---

## Ordre de construction conseillé

1. **Contrats d'abord.** DTO + OpenAPI figés et mergés avant toute implémentation (½ journée, à deux).
2. **Domain pur + tests.** Aucun ASP.NET dedans. C'est ce qui garantit S-16.
3. **API solo vs IA de niveau 1.** Bout en bout le plus court possible.
4. **Front DS minimal** branché dessus. À ce stade : partie jouable → le socle est acquis.
5. **SignalR + multijoueur.** La couche la plus risquée, à attaquer tôt.
6. **Pouvoirs**, un par un, chacun avec son test.
7. **Polish** : assets, sons, animations, IA niveau 3.

Ne partez pas sur les pouvoirs avant que le point 4 soit démontrable. C'est le piège classique.

---

## Définition de « terminé »

Une fonctionnalité est terminée quand :

- [ ] elle a un test automatisé (unitaire pour le Domain, intégration pour l'API) ;
- [ ] elle est reliée à son ID dans le titre de la PR ;
- [ ] `dotnet build` ne produit aucun avertissement nouveau ;
- [ ] la décision de conception, si elle en a demandé une, est tracée dans `PROMPTS.md` ou dans un ADR ;
- [ ] l'autre binôme a relu et mergé.
