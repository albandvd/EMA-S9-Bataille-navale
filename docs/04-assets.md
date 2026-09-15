# 04 — Assets

## Principe : le CSS d'abord

Avant de dessiner quoi que ce soit, sachez que **70 % de l'esthétique DS s'obtient sans un seul
fichier image** : une police pixel, une palette de 8 couleurs, des bordures nettes, et
`image-rendering: pixelated`. Chaque asset binaire est un fichier à produire, à licencier, à
charger et à maintenir.

Ordre de préférence, du moins cher au plus cher :

1. **CSS pur** — cadre de console, boutons, jauges, grille, quadrillage, croix/points des tirs.
2. **SVG inline** — icônes de pouvoirs, silhouettes de navires, viseur. Vectoriel, colorable
   via `currentColor`, aucun souci de résolution.
3. **PNG / spritesheet** — uniquement les animations frame par frame (explosion, gerbe d'eau).
4. **Audio** — indispensable pour le feeling console, mais générable en 20 minutes (voir §6).

Une bataille navale DS convaincante est faisable avec ~12 fichiers. N'en produisez pas 80.

---

## 1. Fondations visuelles

### Palette (à mettre dans `wwwroot/css/tokens.css`)

```css
:root {
  --sea-deep:     #0b2545;   /* eau inconnue */
  --sea:          #13315c;   /* eau explorée */
  --sea-light:    #1c4c7c;   /* survol */
  --hull:         #8d99ae;   /* coque */
  --hull-dark:    #5c677d;   /* ombre de coque */
  --hit:          #ef233c;   /* touché */
  --sunk:         #6a040f;   /* coulé */
  --miss:         #adb5bd;   /* manqué */
  --energy:       #ffd166;   /* énergie */
  --charge:       #06d6a0;   /* charge en cours */
  --shell:        #2b2d42;   /* coque de la console */
  --shell-light:  #3d405b;
  --screen-glow:  #8ecae6;
  --text:         #edf2f4;
}
```

Huit à douze couleurs, pas plus. Une palette restreinte donne l'air « console » même avec des
formes médiocres — c'est le levier le plus rentable du projet.

Pour composer : **Coolors.co**, **Lospec Palette List** (palettes rétro éprouvées, ex. *Endesga 16*,
*Nyx8*, *Sweetie 16*).

### Polices

| Usage | Police | Licence | Où |
|---|---|---|---|
| Titres, HUD | **Press Start 2P** | OFL | Google Fonts |
| Texte courant, journal | **Silkscreen** ou **VT323** | OFL | Google Fonts |
| Chiffres de la grille | **Silkscreen** | OFL | Google Fonts |

Téléchargez les `.woff2` dans `wwwroot/fonts/` plutôt que de dépendre du CDN : la démo doit
fonctionner sans réseau.

> Press Start 2P n'a pas de minuscules lisibles en petit corps. Réservez-la aux titres et aux
> nombres ; sinon votre journal de partie sera illisible.

---

## 2. Coque de console — **100 % CSS**

| Élément | Comment |
|---|---|
| Cadre extérieur | `border-radius`, dégradé linéaire, `box-shadow` intérieure |
| Charnière centrale | Bande de 24 px, `repeating-linear-gradient` pour les stries |
| Écran haut (plateau) | Encart sombre, `inset` shadow, léger `--screen-glow` |
| Écran bas (actions) | Idem, plus petit |
| Reflet d'écran | Pseudo-élément en dégradé blanc à 6 % d'opacité, incliné |
| Boutons A/B/X/Y, croix | `border-radius: 50%`, ombres portées |
| LED d'alimentation | Point de 6 px + `animation: pulse 2s infinite` |
| Effet scanlines *(optionnel)* | `repeating-linear-gradient(transparent 0 2px, rgba(0,0,0,.15) 2px 3px)` |

Aucun fichier. Comptez 150 lignes de CSS pour un rendu très convaincant.

---

## 3. Sprites de navires

| Asset | Format | Taille | Quantité |
|---|---|---|---|
| Navires vue de dessus, 2 à 5 cases | SVG (ou PNG 32 px/case) | 64×32 → 160×32 | 5 |
| Variante endommagée (fumée/trous) | idem | idem | 5 |
| Variante coulée (silhouette immergée) | idem | idem | 5 |
| Leurre (P-14), aspect fantomatique | idem | 64×32 | 1 |

**Où les trouver :**

| Source | Licence | Contenu utile |
|---|---|---|
| **Kenney.nl** — *Pirate Pack*, *Topdown Tanks*, *Game Assets* | CC0 (aucune attribution requise) | Bateaux vue de dessus, tuiles d'eau, explosions, UI |
| **OpenGameArt.org** (filtrer CC0/CC-BY) | variable | Navires, explosions |
| **itch.io**, section *Game assets → Free* | variable | Packs pixel art navals |

Kenney est le bon défaut : CC0, cohérent, et vous n'aurez aucune question de licence en soutenance.

**Les faire vous-même :** **Aseprite** (payant, ~20 €, le standard), **LibreSprite** (fork gratuit),
**Piskel** (gratuit, dans le navigateur, suffisant ici). Un navire vue de dessus en 32×32 par case,
c'est 10 minutes quand on a la palette.

---

## 4. Tuiles de grille — **CSS + un peu de SVG**

| État | Rendu |
|---|---|
| Inconnu | Fond `--sea-deep` + bordure 1 px `rgba(255,255,255,.06)` |
| Manqué | Cercle blanc de 8 px, `opacity: .7` |
| Touché | Croix rouge en SVG ou en pseudo-éléments `::before/::after` rotationnés |
| Coulé | Fond `--sunk` + hachures diagonales en `repeating-linear-gradient` |
| Révélé vide (sonar) | Pointillés cyan animés |
| Révélé occupé | Contour cyan clignotant |
| Survol valide / invalide | `outline` vert / rouge |
| Mine posée | Petit losange SVG |
| Brouillard (P-19) | `backdrop-filter: blur(3px)` + voile gris |

Zéro fichier. Faites-en un composant `GridCell.razor` avec une classe CSS par état.

---

## 5. Icônes de pouvoirs — **SVG, ~23 fichiers**

Un fichier par `PowerId`, 64×64, trait monochrome coloré par `currentColor`, nommés d'après
l'enum pour que le front construise le chemin automatiquement :

```
assets/icons/powers/sonar.svg
assets/icons/powers/orbital-strike.svg
assets/icons/powers/triple-salvo.svg
...
```

**Meilleure source : [game-icons.net](https://game-icons.net)** — plusieurs milliers d'icônes SVG
de jeu, licence **CC BY 3.0** (attribution obligatoire dans `CREDITS.md`, c'est tout). Elles ont
exactement le bon vocabulaire visuel. Correspondances directes :

| Pouvoir | Icône à chercher |
|---|---|
| Sonar | `radar-sweep`, `sonar-wave` |
| Radar tactique | `radar-dish` |
| Drone de ligne | `delivery-drone` |
| Satellite | `satellite-communication` |
| Salve triple | `triple-gun` |
| Frappe en croix | `cross-mark` / `targeting` |
| Tapis de bombes | `bombing-run` |
| Torpille | `torpedo` |
| Frappe orbitale | `mushroom-cloud` |
| Missile perforant | `missile-swarm` |
| Mine navale | `naval-mine` |
| Leurre | `ghost` / `duality-mask` |
| Bouclier | `shield-reflect` |
| Réparation | `mechanical-arm` / `toolbox` |
| Manœuvre évasive | `evasion` |
| Brouillage | `signal-jamming` |
| Écran de fumée | `smoke-bomb` |
| Double tour | `clockwork` / `time-trap` |
| Sabotage | `sabotage` |
| Espionnage | `spy` |
| Surcharge réacteurs | `nuclear-plant` / `battery-pack` |

Alternatives : **Lucide** / **Tabler Icons** (MIT, style plus neutre), **Flaticon** (attention,
attribution requise et beaucoup d'assets payants — à éviter pour un rendu scolaire).

---

## 6. Audio

| Son | Durée | Déclencheur |
|---|---|---|
| `ui-move` | 0,05 s | Déplacement du curseur |
| `ui-select` | 0,1 s | Validation |
| `ui-back` | 0,1 s | Annulation |
| `fire` | 0,4 s | Départ du tir |
| `splash` | 0,6 s | Manqué |
| `explosion` | 0,8 s | Touché |
| `sunk` | 1,5 s | Navire coulé |
| `sonar-ping` | 1,0 s | Reconnaissance |
| `charge-loop` | 2 s bouclé | Pouvoir en charge |
| `alarm` | 1,2 s | Charge adverse détectée |
| `victory` / `defeat` | 3 s | Fin de partie |
| `ambient-sea` | 30 s bouclé | Fond sonore |

**Comment les produire :**

- **jsfxr / Bfxr / sfxr** (gratuits, dans le navigateur) : générateurs de bruitages 8-bit.
  Trois clics sur « Explosion » ou « Blip/Select » donnent exactement le son attendu. **C'est la
  méthode à utiliser** — 20 minutes pour toute la liste, et les sons sont à vous, sans licence.
- **Freesound.org** : filtrez sur CC0. Attention, beaucoup d'enregistrements réalistes qui
  jureront avec l'esthétique pixel.
- **Kenney — *Digital Audio* / *Impact Sounds*** : CC0, cohérents entre eux.
- **BeepBox** (gratuit, navigateur) pour la boucle d'ambiance chiptune. Une boucle de 8 mesures
  suffit largement.

Format : **`.ogg` en priorité** (meilleure compression), avec repli `.mp3` pour Safari.
Normalisez tout entre −14 et −12 LUFS avec **Audacity** (gratuit) : des sons de volumes
disparates ruinent l'impression de finition plus sûrement qu'un mauvais sprite.

> Prévoyez un bouton mute **dès le premier son ajouté**, et mémorisez l'état en `localStorage`.
> Les navigateurs bloquent l'audio avant la première interaction : démarrez l'`AudioContext`
> au premier clic, pas au chargement.

---

## 7. Animations

| Effet | Technique | Coût |
|---|---|---|
| Gerbe d'eau (miss) | CSS keyframes : cercle qui grandit et s'estompe | 10 lignes |
| Explosion (hit) | Spritesheet PNG 5-8 frames, 64×64, `steps()` en CSS | 1 fichier |
| Navire coulé | Rotation + translation Y + fondu, en CSS | 15 lignes |
| Onde de sonar | Cercle SVG, `r` et `opacity` animés | 10 lignes |
| Frappe orbitale | Flash blanc plein écran + secousse + onde de choc | 30 lignes CSS |
| Secousse d'écran | `@keyframes shake` sur le conteneur de l'écran haut | 8 lignes |
| Barre de charge | `width` en transition + rayures animées | 10 lignes |
| Transition d'écran | Fondu au noir style DS, 300 ms | 10 lignes |

Seule l'explosion justifie un fichier : prenez la spritesheet d'explosion de Kenney (CC0) ou
dessinez 6 frames dans Piskel.

**Respectez `prefers-reduced-motion`** : c'est un point d'accessibilité gratuit à mentionner en
soutenance.

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { animation-duration: .01ms !important; transition-duration: .01ms !important; }
}
```

---

## 8. Récapitulatif — ce qu'il faut réellement produire

| # | Asset | Source recommandée | Temps |
|---|---|---|---|
| 1 | Palette + tokens CSS | Lospec → `tokens.css` | 30 min |
| 2 | 2 polices `.woff2` | Google Fonts | 10 min |
| 3 | Coque de console | CSS maison | 2 h |
| 4 | États de cellule | CSS maison | 1 h |
| 5 | 5 sprites de navires (+ variantes) | Kenney *Pirate Pack* (CC0) | 1 h |
| 6 | ~23 icônes de pouvoirs SVG | game-icons.net (CC BY) | 1 h |
| 7 | 1 spritesheet d'explosion | Kenney (CC0) | 15 min |
| 8 | 12 bruitages | jsfxr | 30 min |
| 9 | 1 boucle d'ambiance | BeepBox | 30 min |
| 10 | Favicon + logo titre | Piskel | 30 min |
| 11 | `assets/CREDITS.md` | à la main | 15 min |

**Total ≈ 8 heures.** C'est le budget à tenir. Tout ce qui dépasse est du temps volé au moteur.

---

## 9. `assets/CREDITS.md` — obligatoire

Remplissez-le **au fur et à mesure**, jamais à la fin. Un correcteur qui voit des assets sans
licence traçable a une question toute prête, et vous n'aurez pas la réponse.

```markdown
# Crédits

## Graphismes
- Sprites de navires — Kenney (kenney.nl), CC0 1.0 — modifiés (recoloration)
- Icônes de pouvoirs — game-icons.net, CC BY 3.0 :
  - sonar.svg — Lorc — https://game-icons.net/1x1/lorc/radar-sweep.html
  - orbital-strike.svg — Lorc — https://game-icons.net/1x1/lorc/mushroom-cloud.html
- Spritesheet d'explosion — Kenney, CC0 1.0

## Polices
- Press Start 2P — CodeMan38, SIL Open Font License 1.1
- Silkscreen — Jason Kottke, SIL Open Font License 1.1

## Audio
- Bruitages — générés avec jsfxr, création originale
- Ambiance — composée avec BeepBox, création originale
```

**Trois pièges à éviter :** ne prenez rien sur Google Images ; n'utilisez ni le nom ni les
logos de Nintendo (« style console portable » suffit, ne l'appelez pas « DS » dans l'UI) ;
ne récupérez aucun son ou sprite d'un jeu commercial.
