# 02 — Pouvoirs

## 1. Le modèle économique

Trois ressources, pas une de plus. Au-delà, plus personne ne comprend le jeu.

| Ressource | Gain | Rôle |
|---|---|---|
| **Énergie** | +1 par tour, +2 par touche, +3 par navire coulé | Monnaie des pouvoirs courants |
| **Tours de charge** | Le joueur renonce à tirer pendant N tours | Monnaie des pouvoirs dévastateurs |
| **Charges (`UsesLeft`)** | Fixé au départ | Empêche le spam d'un pouvoir unique |

Un pouvoir peut coûter de l'énergie, des tours de charge, ou les deux.

### Le cycle d'un pouvoir

```
Ready ──(activation, énergie payée)──► Charging(N tours) ──► Armed ──(déclenchement)──► Cooldown ──► Ready
   │                                          │
   └──(N = 0 : effet immédiat)────────────────┘
                                              │
                                  (porteur coulé / sabotage) ──► Aborted
```

`Charging` consomme **le tour de tir**. C'est ça le vrai coût : pendant que vous chargez,
l'adversaire tire librement.

### La règle qui rend tout intéressant : le porteur

Tout pouvoir avec `ChargeTurns ≥ 3` doit être **porté par un navire** désigné à l'activation.
Si ce navire est coulé pendant la charge, la charge est perdue et l'énergie n'est pas remboursée.

Conséquence : charger une arme lourde vous rend lisible et vulnérable. L'adversaire qui devine
le porteur a une raison de concentrer ses tirs. C'est ce qui transforme une mécanique de
« je clique et j'attends » en décision tactique.

---

## 2. Sur votre idée de bombe nucléaire

Vous proposiez : *30 tours sans jouer, puis victoire instantanée*. Faites le calcul avant de
l'implémenter.

Grille 10×10, flotte classique = 17 cases de navire. Une IA de niveau 2 coule une flotte en
**45 à 60 tirs** en moyenne, une bonne IA probabiliste en 40. En renonçant à 30 tours, vous
offrez 30 tirs gratuits à l'adversaire tout en n'avançant pas d'un pouce : vous perdez la
partie avant l'explosion, sauf si l'adversaire joue très mal. Le pouvoir n'est jamais joué —
c'est un piège à débutant, ce qui est le pire résultat possible pour un pouvoir « ultime ».

Trois corrections, à empiler :

1. **Ramenez la charge à 10-12 tours** (≈ 25 % de la durée d'une partie).
2. **Remplacez la victoire instantanée par une dévastation de zone 5×5** : ça gagne souvent
   la partie sans la garantir, et l'adversaire garde une fenêtre de riposte.
3. **Rendez le silo visible** : au démarrage de la charge, l'adversaire reçoit un avertissement
   et, au tour 5, la colonne du porteur lui est révélée. Il a désormais un choix : continuer
   à chercher la flotte, ou se ruer sur le silo.

Et ajoutez **Sabotage** (P-21) en face, sinon il n'y a pas de contre-jeu.

Version corrigée retenue : `P-11 Frappe Orbitale`, ci-dessous.

---

## 3. Le catalogue

`E` = coût en énergie · `C` = tours de charge · `CD` = cooldown · `U` = utilisations par partie

### Reconnaissance

| ID | Nom | E | C | CD | Effet | Pourquoi c'est équilibré |
|---|---|---|---|---|---|
| P-01 | **Sonar** | 3 | 2 | 3 | Renvoie *le nombre* de cases occupées dans un disque de rayon 3 (réduit de 4 à 3), pas leur position | Information floue : réduit l'espace de recherche sans le résoudre |
| P-02 | **Radar tactique** | 5 | 0 | 4 | Révèle le contenu exact d'une zone 3×3 | Cher et immédiat ; 9 cases sur 100, ça reste un pari |
| P-03 | **Drone de ligne** | 4 | 1 | 3 | Renvoie le nombre de cases occupées sur une ligne ou une colonne entière | Complémentaire du sonar : croiser ligne + colonne trilatère une position |
| P-04 | **Interception radio** | 3 | 1 | 5 | Révèle l'orientation (H/V) et la taille d'un navire intact tiré au hasard | Aléatoire : on ne choisit pas la cible |
| P-05 | **Satellite** | 6 | 3 | — (U=1) | Révèle la coordonnée exacte d'une case d'un navire non touché | Une seule fois, très cher, et 3 tours sans tirer |
| P-06 | **Analyse de sillage** | 2 | 0 | 2 | Sur une case déjà manquée, indique si un navire est adjacent | Recycle l'information « ratée », très peu cher |

### Offensif

| ID | Nom | E | C | CD | Effet | Pourquoi c'est équilibré |
|---|---|---|---|---|---|
| P-07 | **Salve triple** | 4 | 0 | 3 | 3 tirs consécutifs alignés | Les résultats sont annoncés groupés en fin de salve : pas d'ajustement en cours de route |
| P-08 | **Frappe en croix** | 5 | 1 | 4 | Tire sur une case + ses 4 voisines orthogonales | 5 cases mais forme imposée : souvent 2-3 tirs gaspillés |
| P-09 | **Tapis de bombes** | 6 | 2 | 5 | Tire sur un carré 2×2 | Idéal après une touche, inutile à l'aveugle |
| P-10 | **Torpille** | 4 | 1 | 3 | Part d'un bord, avance en ligne droite, s'arrête à la première touche et révèle la distance parcourue | Touche garantie **ou** information de « case vide » sur toute la trajectoire |
| P-11 | **Frappe orbitale** | 8 | 10 | — (U=1) | Détruit intégralement une zone 5×5 (toutes les cases de navire dedans sont marquées touchées) | 10 tours offerts à l'adversaire + porteur révélé au tour 5 + annulable par sabotage |
| P-12 | **Missile perforant** | 5 | 1 | 4 | Touche une case ; si c'est une touche, touche automatiquement la case suivante dans l'axe du navire | Ne sert à rien si le premier tir manque |
| P-13 | **Mine navale** | 3 | 0 | 3 | Pose une mine sur **sa propre** grille ; si l'adversaire tire dessus, il perd son prochain tour | L'adversaire peut ne jamais tirer là ; l'énergie est alors gaspillée |
| P-24 | **Bombe lourde** (`HeavyBomb`) | 10 | 0 | 11 | Tire sur un carré 3×3 centré sur la cible (tronqué au bord de la grille) ; remplace le tir du tour | ≈ 8 tours d'économie + 11 tours de cooldown pour 9 tirs ; ses touches ne rapportent **aucune** énergie, sinon elle s'autofinance |
| P-25 | **Tsar Bomba** (`TsarBomba`) | 40 | 0 | — (U=1) | Tire sur un carré 5×5 centré sur la cible (tronqué au bord) ; remplace le tir du tour | 40 d'énergie sans remboursement par les touches : arrive très tard, une seule fois, et rien ne garantit que la zone contienne encore des navires |

### Défensif

| ID | Nom | E | C | CD | Effet | Pourquoi c'est équilibré |
|---|---|---|---|---|---|
| P-14 | **Leurre** | 4 | 0 | — (U=2) | Place un faux navire de 2 cases ; il affiche « touché » puis « coulé » à l'adversaire sans rien coûter | Consomme de la place sur la grille ; le bluff ne marche qu'une fois ou deux |
| P-15 | **Bouclier** | 5 | 1 | 5 | La prochaine touche sur le navire désigné est annulée (affichée « manqué ») | Un seul impact absorbé, et il faut deviner la cible |
| P-16 | **Réparation d'urgence** | 7 | 2 | 6 | Restaure une case touchée d'un navire non coulé | Très cher, inefficace contre le tir en rafale |
| P-17 | **Manœuvre évasive** | 6 | 2 | 5 | Déplace d'une case un navire **intact** | Interdit sur un navire déjà touché : pas de fuite de dernière seconde |
| P-18 | **Brouillage** | 4 | 0 | 4 | Les 2 prochains tirs adverses ne renvoient aucun feedback ; les résultats sont révélés d'un coup au 3ᵉ tour | Retarde l'information, ne la supprime pas |
| P-19 | **Écran de fumée** | 3 | 0 | 4 | Une zone 3×3 devient opaque à toute reconnaissance adverse pendant 5 tours | Ne bloque pas les tirs normaux |

### Utilitaire & contre-jeu

| ID | Nom | E | C | CD | Effet | Pourquoi c'est équilibré |
|---|---|---|---|---|---|
| P-20 | **Double tour** | 7 | 0 | 6 | Rejoue immédiatement un tour complet | Le coût en énergie équivaut à ~5 tours d'accumulation |
| P-21 | **Sabotage** | 5 | 0 | 5 | Annule la charge en cours de l'adversaire | Inutile si l'adversaire ne charge rien : le garder en main coûte un slot |
| P-22 | **Espionnage** | 2 | 0 | 3 | Révèle quel pouvoir l'adversaire charge et le nombre de tours restants | Bon marché, mais c'est de l'information pure |
| P-23 | **Surcharge réacteurs** | 0 | 1 | 2 | Saute le tour, gagne +4 énergie | Convertit du tempo en ressource : le pari des joueurs patients |

---

## 4. Règles d'équilibrage à respecter

1. **3 pouvoirs maximum** dans le loadout, choisis avant le déploiement. Le choix doit faire mal.
2. **Un pouvoir par tour.** Jamais de combo `Double tour` + `Frappe en croix` dans le même tour.
3. **Aucun pouvoir ne révèle une position exacte pour moins de 5 énergie.** L'information exacte
   est la ressource la plus forte du jeu, plus forte qu'un tir.
4. **Tout pouvoir de zone est annoncé à l'adversaire** (« bombardement détecté en secteur D-4 »)
   après résolution. Personne ne doit se demander ce qui vient de se passer.
5. **Pas de pouvoir qui gagne seul.** Si un pouvoir a un taux de victoire > 65 % en le testant
   contre l'IA de niveau 2, augmentez son coût.
6. **Symétrie :** l'IA doit pouvoir utiliser les pouvoirs. Sinon le mode solo devient trivial et
   vous ne testez jamais l'équilibrage.

### Presets suggérés

| Preset | Pouvoirs | Profil |
|---|---|---|
| `Recon` | P-01, P-03, P-12 | Trouve vite, frappe juste |
| `Artillerie` | P-07, P-09, P-23 | Accumule puis écrase |
| `Fourbe` | P-14, P-18, P-13 | Fait perdre du temps à l'adversaire |
| `Apocalypse` | P-11, P-15, P-23 | Tout sur la frappe orbitale |
| `Contre` | P-22, P-21, P-02 | Lit le jeu adverse et le démonte |

`Apocalypse` doit perdre contre `Contre`. Si ce n'est pas le cas, le sabotage est trop faible.

---

## 5. Implémentation

Un pouvoir = une **définition** (données, sérialisable, exposée par l'API) + un **handler**
(comportement, côté Domain uniquement).

```csharp
// Naval.Shared/Domain/Powers/PowerDefinition.cs
public sealed record PowerDefinition(
    PowerId      Id,
    string       Name,
    PowerCategory Category,
    string       Description,
    int          EnergyCost,
    int          ChargeTurns,
    int          Cooldown,
    int          MaxUses,          // -1 = illimité
    TargetKind   TargetKind,       // None, Cell, Ship, Line, Area
    int          Radius);

// Naval.Shared/Domain/Powers/IPowerHandler.cs
public interface IPowerHandler
{
    PowerId Id { get; }

    /// Vérifie les préconditions propres au pouvoir (l'énergie et le cooldown
    /// sont déjà contrôlés par le moteur). Retourne le motif du refus, ou null.
    string? Validate(GameState game, PlayerId caster, PowerTarget target);

    /// Applique l'effet et retourne les événements produits.
    IReadOnlyList<GameEvent> Execute(GameState game, PlayerId caster, PowerTarget target);
}
```

Enregistrement par scan d'assembly ou dictionnaire explicite :

```csharp
services.AddSingleton<IPowerHandler, SonarHandler>();
services.AddSingleton<IPowerHandler, TripleSalvoHandler>();
// …
services.AddSingleton<IPowerRegistry, PowerRegistry>(); // indexe par PowerId
```

### Ajouter un pouvoir en 5 étapes

1. Ajouter la valeur dans l'enum `PowerId`.
2. Ajouter la `PowerDefinition` dans `PowerCatalog.All`.
3. Écrire le `XxxHandler` implémentant `IPowerHandler`.
4. Écrire les tests : un cas nominal, un cas de refus, un cas limite (bord de grille).
5. Ajouter l'icône dans `assets/icons/powers/` — le front lit `PowerId` pour construire le
   chemin, rien à câbler côté UI.

Aucune modification du front n'est nécessaire pour un pouvoir dont l'effet est purement
serveur : c'est le test qui prouve que votre architecture tient.

### Les événements à produire

Le front n'a pas à recalculer quoi que ce soit. Chaque pouvoir émet des événements typés :

| Événement | Charge utile |
|---|---|
| `PowerActivated` | `powerId`, `casterId`, `chargeTurns` |
| `PowerCharging` | `powerId`, `turnsRemaining` |
| `PowerAborted` | `powerId`, `reason` (`CarrierSunk`, `Sabotaged`) |
| `PowerResolved` | `powerId`, `revealedCells[]`, `shots[]`, `message` |
| `EnergyChanged` | `playerId`, `delta`, `total` |

C'est aussi ce flux qui alimente le replay (E-26) : une raison de plus de le faire proprement.
