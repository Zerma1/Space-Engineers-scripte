---
tags: [space-engineers, tests]
---
# Scénarios de test en jeu

> [!summary] À quoi sert cette note ?
> Les scripts du Bloc Programmable (PB) ne peuvent pas être testés par un framework : on les teste **en jeu**, de préférence en **mode Créatif**, en observant ce que le joueur voit (console Echo du PB, écrans, portes, lumières, sons, inventaires). Pour chaque script : 1) les comportements à vérifier, 2) les scénarios, dont les cas limites, 3) le résultat attendu, 4) le banc d'essai et la procédure à cocher.
> Les **écarts** entre ce que promettent les notes et ce que fait le code sont regroupés en fin de note.

Retour au [[00 - Sommaire]] · Règles communes : [[Conventions du projet]]

---

## 0. Préparation commune

### 0.1 Monde de test

- [ ] Nouveau monde **Créatif**, « Vide » ou « Planète » (pour l'O2 du sas, préférer l'espace).
- [ ] Options du monde : **Scripts ingame activés** (Paramètres avancés → « Activer les scripts »).
- [ ] Une grande grille « base » alimentée : 2 grandes batteries + 1 grand réacteur (avec un peu d'uranium) ou des panneaux solaires.
- [ ] Un petit vaisseau équipé d'un **connecteur**, pour les tests « vaisseau amarré » (verrouiller le connecteur sur celui de la base).

### 0.2 Gestes utiles

| Geste | Comment |
|---|---|
| Lire la console Echo | Terminal (K) → sélectionner le PB → panneau de droite |
| Lancer un argument | Terminal → PB → champ **Argument** → **Exécuter** |
| Recompiler | Terminal → PB → **Modifier** → **Vérifier le code** → **OK** (ou **Recompiler**) |
| Modifier un Custom Data | Terminal → bloc → **Données personnalisées** → **Modifier** → valider |
| Abîmer un bloc **sans** le détruire | Tirer dessus avec le fusil de poing (ou une tourelle intérieure en mode test) |
| Détruire un bloc | Le meuler entièrement (en Créatif c'est quasi instantané) ou une ogive armée puis « Détoner » |
| Remplir un inventaire | Inventaire du joueur en Créatif (G) → glisser les objets dans le coffre |

> [!tip] Chronométrer
> Les délais des scripts sont en **temps de jeu** (`Runtime.TimeSinceLastRun`). Pour mesurer, comptez à voix haute ou regardez l'horloge système : la précision visée est de ± 2 s (Update100 ≈ 1,7 s, Update10 ≈ 0,17 s).

### 0.3 Convention des tableaux

- **ID** : `TRI-xx`, `DIC-xx`, `AFF-xx`, `AVA-xx`, `CAR-xx`, `BUS-xx`, `DIA-xx`, `SAS-xx`, `FER-xx`, `INT-xx` (intégration).
- **Résultat attendu** : ce que l'on doit **observer**. Textes Echo / écran entre guillemets = texte exact produit par le code.
- Une case `- [ ]` par vérification : cochez-la en jeu.

---

## 1. Tri Automatique des Inventaires (`TriInventaire.cs`)

Note : [[Tri Automatique des Inventaires]]

### 1.1 Comportements à tester

| # | Comportement | Origine (note / code) |
|---|---|---|
| B1 | Lecture du nom `texte {categorie}[liste]priorite` (catégorie, liste, `*`, `*-…`, alias, casse) | Note §1, code `AnalyserNomDestination` |
| B2 | Priorité : 0 = la plus haute ; absente = 99 ; illisible = 99 + erreur | Note §1, §6 |
| B3 | Débordement : coffre prioritaire plein → coffre suivant ; pile partielle continuée | Note §5.4 |
| B4 | Anti ping-pong : un objet bien rangé ne bouge plus, mais remonte vers un coffre strictement plus prioritaire | Note §5.4.3 |
| B5 | Sources vidées / jamais vidées (réacteur, générateur O2, tourelle, cockpit, entrée de raffinerie, assembleur en désassemblage) | Note §5 tableau |
| B6 | Balise `[NoTri]` : jamais vidé, mais reste destination | Note §1 |
| B7 | Vaisseau amarré : ni vidé, ni utilisé comme destination | `SEULEMENT_CETTE_CONSTRUCTION` |
| B8 | Commandes `rescan`, `stats`, commande inconnue | Note §4 |
| B9 | Rescan périodique 10 s ; bloc disparu → rescan | Note §5.2 |
| B10 | Traitement par lots (20 sources / exécution) | Note §3 |
| B11 | Erreurs de nommage dans Echo (5 max) | Note §8 |

### 1.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| TRI-01 | Grille sans aucun coffre nommé `{…}[…]` | Echo : « Destinations : 0 » et « ! Aucune destination : nommez un coffre {ore}[*] ». Aucun objet ne bouge. |
| TRI-02 | `Coffre A {ore}[*]0`, minerais + lingots dans un coffre `Vrac` | Tous les **minerais** passent dans A en ≤ 2 exécutions ; les lingots restent dans `Vrac`. |
| TRI-03 | `Coffre B {ingot}[fer;nickel]1` (dictionnaire présent) ; lingots fer, nickel, silicium dans `Vrac` | Seuls fer et nickel vont dans B ; silicium reste dans `Vrac`. |
| TRI-04 | `C1 {ore}[*-pierre]0` + `C2 {ore}[pierre]5` ; pierre + fer en vrac | Fer → C1, pierre → C2. |
| TRI-05 | `P0 {ore}[*]0` (petit coffre) + `P1 {ore}[*]1` ; plus de minerai que P0 n'en contient | P0 se remplit, **le reste** va dans P1 (aucun objet bloqué dans `Vrac`). `stats` affiche « PLEIN » derrière P0. |
| TRI-06 | Suite de TRI-05 : vider P0 à la main | Le minerai de P1 **remonte** vers P0 (priorité strictement meilleure). Rien ne redescend de P0 vers P1 (pas de ping-pong). |
| TRI-07 | `Sans prio {ore}[*]` + `Prio 5 {ore}[*]5` | Le minerai va dans « Prio 5 » (99 > 5). `stats` : « P99 {ore} … : Sans prio {ore}[*] ». |
| TRI-08 | Priorité illisible : `X {ore}[*]abc` | Echo : « X {ore}[*]abc : priorité 'abc' illisible -> 99 ». Le coffre fonctionne en priorité 99. |
| TRI-09 | Noms invalides : `[*]{ore}0`, `{}[*]`, `{ore}[]`, `{ore}[*pierre]`, `Coffre {ore` | Erreurs respectives : « il faut [liste] APRÈS {categorie} », « catégorie vide entre { } », « liste [ ] vide : ce coffre n'accepte rien », « après * il faut un tiret : [*-pierre] », « accolades { } incomplètes ou inversées ». |
| TRI-10 | 7 coffres mal nommés | « --- Erreurs de nommage (7) --- » mais **5** lignes listées seulement. |
| TRI-11 | Casse, **sans** dictionnaire : `{ORE}[IRON]0` | Le minerai de fer y est rangé (comparaison insensible à la casse). |
| TRI-12 | `Perso [NoTri]` contenant du minerai, + `Coffre {ore}[*]0` | Le contenu de `Perso` ne bouge pas. Un coffre `[NoTri] Stock {ore}[*]0` **reçoit** quand même du minerai. |
| TRI-13 | Sources interdites : réacteur (uranium), générateur O2 (glace), tourelle (munitions), cockpit (objets), raffinerie (minerai en entrée) + `{ingot}[*]`, `{ore}[*]`, `{ammomagazine}[*]` | Aucun de ces inventaires n'est vidé. Seuls les **lingots en sortie** de la raffinerie partent. |
| TRI-14 | Assembleur en **désassemblage** avec composants en sortie + `{component}[*]` | Sa sortie n'est pas vidée. Repasser en assemblage → la sortie est vidée. |
| TRI-15 | Foreuse / broyeuse / collecteur contenant du minerai | Leur inventaire est vidé vers `{ore}[*]`. |
| TRI-16 | Vaisseau amarré par connecteur : cargo plein de minerai + un coffre `Nav {ore}[*]0` à bord | Le cargo du vaisseau n'est **pas** vidé ; `Nav` n'est **pas** compté dans « Destinations ». |
| TRI-17 | Connecteur de la base nommé `Quai {ore}[glace]0` | La glace y est rangée (connecteur accepté comme destination). |
| TRI-18 | Coffre destination **non relié** par convoyeur (convoyeur meulé) | Pas d'erreur ; la destination suivante est utilisée ; si aucune autre, l'objet reste en place. |
| TRI-19 | Commandes : `rescan`, `stats` (deux fois), `  RESCAN  `, `foo` | « > Rescan effectué. » ; `stats` affiche puis masque la liste « --- Destinations (priorité, catégorie) --- » ; les espaces/majuscules sont tolérés ; « > Commande inconnue : 'foo' (rescan, stats) ». |
| TRI-20 | Renommer un coffre sans `rescan` | Pris en compte en ≤ 10 s (Echo « Prochain rescan : N s » décroît puis repart à 10). |
| TRI-21 | Meuler une destination pendant que le script tourne | Pas d'exception (PB toujours vert) ; le nombre de destinations baisse à l'exécution suivante. |
| TRI-22 | 25 coffres sources ou plus | Echo « Lot : prochaine source x/25 (20 max / exécution) » progresse ; « Dernier passage complet : N déplacement(s) » apparaît après 2 exécutions. |

### 1.3 Banc d'essai minimal

Base alimentée + PB `PB Tri` + 6 petits conteneurs reliés par convoyeurs (`Vrac`, `C1`, `C2`, `P0` (petit), `P1`, `Perso`), 1 raffinerie, 1 assembleur, 1 réacteur, 1 générateur O2, 1 tourelle intérieure, 1 cockpit, 1 foreuse, 1 connecteur de base + le petit vaisseau à connecteur.

### 1.4 Procédure

- [ ] Installer le script (sans `SERVEUR_DICTIONNAIRE` pour l'instant). Echo affiche « === TRI AUTOMATIQUE === ».
- [ ] **TRI-01** : aucun coffre renommé → vérifier les deux lignes Echo.
- [ ] **TRI-11** : renommer `C1` en `C1 {ORE}[IRON]0`, mettre du minerai de fer dans `Vrac` → le fer passe dans C1.
- [ ] Installer ensuite le [[Serveur Dictionnaire]] (voir §2) puis `rescan`.
- [ ] **TRI-02 / 03 / 04** : nommer les coffres, remplir `Vrac`, observer les inventaires.
- [ ] **TRI-05 / 06** : remplir P0 au-delà de sa capacité, vérifier P1 ; vider P0 à la main, vérifier la remontée ; attendre 30 s → aucun aller-retour.
- [ ] **TRI-07 / 08** : vérifier la cible et le texte d'erreur Echo, puis `stats`.
- [ ] **TRI-09 / 10** : créer les noms invalides, lire les erreurs (5 affichées max).
- [ ] **TRI-12** : coffre `Perso [NoTri]`, puis `[NoTri] Stock {ore}[*]0`.
- [ ] **TRI-13 / 14 / 15** : charger les blocs indiqués, attendre 10 s, ouvrir chaque inventaire.
- [ ] **TRI-16** : amarrer le vaisseau (connecteur verrouillé), remplir son cargo, attendre 20 s → rien ne bouge.
- [ ] **TRI-17 / 18** : connecteur destination ; puis meuler un convoyeur d'un coffre destination.
- [ ] **TRI-19 / 20 / 21** : commandes, renommage, meulage en cours d'exécution.
- [ ] **TRI-22** (optionnel) : copier-coller 25 coffres, lire la progression du lot.

---

## 2. Serveur Dictionnaire (`SERVEUR_DICTIONNAIRE.ini`) — utilisé par Tri et Affichage

Note : [[Serveur Dictionnaire]]

### 2.1 Comportements à tester

Lecture des sections `[Categories]`, `[Alias]`, `[Affichage]` (et ancienne `[Dictionnaire]`), rechargement à chaud, absence du serveur, Custom Data invalide, priorité au serveur de la **propre** construction, clés spécifiques `Categorie_SubtypeId`.

### 2.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| DIC-01 | Aucun bloc `SERVEUR_DICTIONNAIRE` | Tri : « Dictionnaire : ABSENT : noms du jeu uniquement (Iron, ore...) ». Affichage : « Dictionnaire : absent (noms du jeu) ». `{minerai}[*]` n'accepte rien (et **aucune** erreur de nommage) ; écrans en anglais (`Iron`). |
| DIC-02 | Serveur avec le `.ini` complet | Tri : « Dictionnaire : OK (N alias objets, M alias catégories) ». Affichage : « Dictionnaire : X noms, Y alias, Z catégories ». Écrans en français (`Fer`, `Gravier`). |
| DIC-03 | **Modifié à chaud** : ajouter `ferro` à `Iron=…` et changer `Iron=Fer` en `Iron=Fer (test)` dans `[Affichage]` | Sans `rescan` : un coffre `{ore}[ferro]0` accepte le fer en ≤ 2 exécutions ; l'écran affiche « Fer (test) » en ≤ 2 exécutions. |
| DIC-04 | Custom Data **invalide** (supprimer le `]` de `[Alias`) | Tri : « Dictionnaire : ERREUR INI : … » et retour aux noms du jeu ; Affichage : « Custom Data du serveur invalide : … ». Recorriger → retour à OK sans recompiler. |
| DIC-05 | Ancien format seul : `[Dictionnaire]` `ferro=Iron` | Tri : `{ore}[ferro]` accepte le fer. Conflit `[Dictionnaire] fer=Nickel` + `[Alias] Iron=fer` → `fer` désigne **Iron** (le nouveau format gagne). |
| DIC-06 | Serveur construit **après** les PB | Détecté en ≤ 10 s (rescan périodique) ou immédiatement avec `rescan`. |
| DIC-07 | Serveur meulé | Tri repasse « ABSENT » dès l'exécution suivante ; Affichage « absent » dès l'exécution suivante ; noms du jeu sur les écrans. |
| DIC-08 | Base **sans** serveur, vaisseau amarré **avec** serveur | Les deux scripts utilisent le serveur du vaisseau (repli autorisé par la note). |
| DIC-09 | Base **et** vaisseau amarré ont chacun un serveur (contenus différents, ex. `Iron=Fer BASE` / `Iron=Fer NAVIRE`) | Attendu selon la note : les deux scripts utilisent celui de la **base**. Voir écart **E1** : l'Affichage peut prendre celui du vaisseau. |
| DIC-10 | Clés spécifiques : minerai de pierre et gravier en stock | Écran : « Pierre : … » (`Ore_Stone`) et « Gravier : … » (`Ingot_Stone`). |
| DIC-11 | Catégorie munitions : coffre `{munitions}[*]0`, munitions Gatling en vrac | Les munitions y sont rangées (`ammomagazine`). |
| DIC-12 | Deux blocs nommés `SERVEUR_DICTIONNAIRE` sur la base | Aucune erreur ; un seul est utilisé (le premier trouvé). Documenter lequel. |

### 2.3 Procédure

- [ ] **DIC-01** avant toute installation du serveur (lire Echo des deux PB).
- [ ] Construire un **panneau de contrôle**, le nommer `SERVEUR_DICTIONNAIRE`, coller le `.ini` → **DIC-02**.
- [ ] **DIC-03** : modifier, valider, **ne pas** taper `rescan`, observer.
- [ ] **DIC-04** : casser puis réparer la section.
- [ ] **DIC-05** : remplacer temporairement tout le Custom Data par l'ancien format, puis le conflit.
- [ ] **DIC-06 / 07** : meuler puis reconstruire le serveur.
- [ ] **DIC-08 / 09** : copier le serveur sur le vaisseau amarré, modifier ses textes, comparer les écrans.
- [ ] **DIC-10 / 11 / 12**.

---

## 3. Affichage Data Center (`AffichageDataCenter.cs`)

Note : [[Affichage Data Center]]

### 3.1 Comportements à tester

| # | Comportement |
|---|---|
| B1 | Sélection des écrans : section `[Ecran]` avec `Role` absent ou `Stocks` (casse indifférente) ; les autres rôles ne sont jamais touchés |
| B2 | Choix de la surface (`Surface=N`) pour cockpit / PB ; surface inexistante signalée |
| B3 | Mode Fixe (`Page`/`TotalPages` bornés) et Rotatif (`LignesParPage`, `SecondesParPage ≥ 2`) |
| B4 | Filtres `Categories` (alias) et `Exclusions` (SubtypeId, alias, nom affiché, clé spécifique) |
| B5 | Gaz (O2/H2) et énergie (batteries, producteurs par famille) |
| B6 | Collecte par lots, tri des lignes, en-têtes de catégorie |
| B7 | Écriture seulement si le texte change ; `ContentType` forcé en texte |
| B8 | `rescan`, rescan 10 s, vaisseau amarré ignoré |

### 3.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| AFF-01 | LCD sans Custom Data | Jamais modifié. Echo « Écrans actifs : 0 / 0 candidats ». |
| AFF-02 | LCD avec `[Ecran]` seul ; minerais + lingots en stock | Titre « === DATA CENTER (Page 1/1) === », en-têtes « --- MINERAIS --- » puis « --- LINGOTS --- », lignes triées par nom affiché, format « Fer : 15,000 ». |
| AFF-03 | `Role=Avaries`, `Role=Structure`, `Role=Carburant` (sans les autres scripts) | Ces LCD gardent leur texte : non touchés. Echo : comptés comme candidats, pas comme actifs. `Role=stocks` (minuscules) → piloté. |
| AFF-04 | Cockpit : `Surface=1` ; puis `Surface=9` | Seul le 2ᵉ écran du cockpit affiche les stocks ; avec 9 : Echo « ! NomCockpit : Surface=9 n'existe pas sur ce bloc ». |
| AFF-05 | Fixe, 2 LCD `TotalPages=2`, `Page=1` / `Page=2` | La liste est partagée en deux moitiés sans doublon. `Page=3` → « Page=3 hors de 1..2, 2 utilisée ». `TotalPages=0` → « TotalPages=0 invalide, 1 utilisé ». `TotalPages=50` avec 5 lignes → page « (rien à afficher sur cette page) ». |
| AFF-06 | Rotatif, `LignesParPage=3`, `SecondesParPage=4`, ~10 lignes | Titre « (1/4) », « (2/4) »… change toutes les ~4 s (± 1,7 s). `SecondesParPage=1` → « SecondesParPage trop petit, 2 utilisé ». `LignesParPage=0` → « … invalide, 10 utilisé ». |
| AFF-07 | `Categories=minerai` ; `Categories=aucune` + `AfficherGaz=true` + `AfficherEnergie=true` ; `Categories=` vide | Minerais seuls ; gaz + énergie seuls, sans aucune ligne de stock ; toutes catégories. |
| AFF-08 | `Exclusions=pierre` / `Gravier` / `Ingot_Stone` / `Stone` | pierre : masque Pierre **et** Gravier ; Gravier : masque le gravier seul ; Ingot_Stone : le gravier seul ; Stone : les deux. |
| AFF-09 | Gaz : grand réservoir **O2** + réservoir **H2** ; puis aucun | « Oxygène : x% (… L) » et « Hydrogène : y% » correctement classés (le grand réservoir O2 n'est **pas** compté comme H2) ; sans réservoir : « Aucun réservoir. ». |
| AFF-10 | Énergie : batteries + solaire + réacteur | « Batteries : …% (…/… MWh) », « Charge : … MW \| Décharge : … MW », une ligne par famille **présente** (Solaire, Réacteurs), « Production : … / … MW ». Sans batterie : « Aucune batterie. ». |
| AFF-11 | Ajouter 1 000 kg de fer dans un coffre | La valeur affichée augmente de 1 000 en ≤ 2 exécutions. Tout vider → « Aucun objet correspondant. ». |
| AFF-12 | Vaisseau amarré : cargo plein + LCD `[Ecran]` à bord | Son contenu n'est pas compté ; son LCD n'est pas écrit. |
| AFF-13 | `Mode=Defilant` | « Mode 'Defilant' inconnu, Fixe utilisé ». |
| AFF-14 | Custom Data d'écran invalide (`[Ecran` sans `]`) | Écran ignoré. Si le pré-filtre ne voit pas `[Ecran]`, aucun message ; avec `[Ecran]` présent mais une autre ligne cassée : « ! Nom : Custom Data invalide (…) ». |
| AFF-15 | Nouvel écran ; `rescan` ; argument `xyz` | Pris en compte en ≤ 10 s, ou tout de suite avec `rescan` (« Rescan demandé. ») ; « Commande inconnue : xyz (attendu : rescan) ». |
| AFF-16 | Écrire un texte à la main sur un LCD piloté, sans changer le stock | Le texte manuel reste tant que les données ne changent pas (preuve de « écrire seulement si ça change »). Modifier son Custom Data (ajouter un espace) → réécrit aussitôt. |
| AFF-17 | LCD réglé sur « Script » ou « Aucun » | Repassé en « Texte et images » à la première écriture. |
| AFF-18 | Meuler un LCD piloté | Pas d'exception ; nombre de candidats mis à jour au rescan suivant. |

### 3.3 Banc d'essai et procédure

Banc : base du §1 + PB `PB_DATA_CENTER` + 3 LCD + 1 cockpit (≥ 2 surfaces) + 1 grand réservoir O2 + 1 réservoir H2 + batteries + 1 panneau solaire + [[Serveur Dictionnaire]].

- [ ] Installer le script ; lire « === AFFICHAGE DATA CENTER === ».
- [ ] **AFF-01 → AFF-03** : Custom Data successifs sur le LCD 1.
- [ ] **AFF-04** : Custom Data du cockpit.
- [ ] **AFF-05** : LCD 1 et 2 en Fixe 2 pages, puis valeurs invalides.
- [ ] **AFF-06** : LCD 3 en Rotatif, chronométrer 3 changements de page.
- [ ] **AFF-07 / 08** : filtres sur le LCD 1.
- [ ] **AFF-09 / 10** : afficher gaz et énergie, puis retirer réservoirs / batteries (et `rescan`).
- [ ] **AFF-11 → AFF-18**.

---

## 4. Contrôle des Avaries (`ControleAvaries.cs`)

Note : [[Controle des Avaries]]

### 4.1 Comportements à tester

| # | Comportement |
|---|---|
| B1 | Classement : endommagé (`CurrentDamage > 0`), critique (`IsFunctional == false`), DÉTRUIT (disparu de la grille) |
| B2 | Tri du rapport (détruits, H.S., intégrité croissante), limite de 15 lignes |
| B3 | Alarme = au moins un critique **non acquitté** ; `ACK` ; réarmement sur nouveau critique ; acquittés réparés oubliés |
| B4 | `rescan` oublie les DÉTRUIT ; le rescan automatique ne les oublie pas |
| B5 | Écrans `Role=Avaries` (LCD, `Surface=N`), écriture si changement |
| B6 | Groupe `ALARMES_CRITIQUES` : `Enabled`, couleur, `Play()/Stop()` sur bloc sonore |
| B7 | Construction : vaisseau amarré ignoré |
| B8 | Echo : blocs, critiques, alarme, écrans, groupe, bus, temps |

### 4.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| AVA-01 | Aucun dégât | Écran : « [ SYSTÈMES NOMINAUX ] … Aucune avarie détectée. ». Echo « Alarme (Avaries) : inactive ». Gyrophare du groupe **éteint**. |
| AVA-02 | Quelques balles sur une porte (reste fonctionnelle) | « --- RÉPARATIONS REQUISES (1) --- » / « [DÉGÂTS] Porte : 87% ». Pas d'alarme. |
| AVA-03 | Tirer jusqu'à la ligne rouge sur un bloc (H.S.) | « !!! PANNES MAJEURES (1) !!! » / « [CRITIQUE] Nom H.S. (x%) ». Gyrophare **allumé rouge**, bloc sonore **joue**. Echo « ACTIVE ». |
| AVA-04 | Meuler entièrement un bloc terminal | « [DÉTRUIT] Nom » en tête du rapport ; alarme active. |
| AVA-05 | Combiner un DÉTRUIT, un H.S., deux endommagés (40 % et 80 %) | Ordre : DÉTRUIT, CRITIQUE, DÉGÂTS 40 %, DÉGÂTS 80 %. |
| AVA-06 | 20 blocs endommagés | 15 lignes puis « ... et 5 autres ». |
| AVA-07 | `ACK` avec un H.S. présent | Gyrophare éteint, son arrêté ; rapport « (Alarme acquittée - ACK) » ; Echo « acquittée ». Les lignes DÉTRUIT disparaissent au passage suivant. |
| AVA-08 | Après `ACK`, rendre H.S. un **autre** bloc | L'alarme se rallume. |
| AVA-09 | Après `ACK`, réparer le bloc acquitté, puis le remettre H.S. | L'alarme se rallume (l'acquittement a été oublié à la réparation). |
| AVA-10 | Bloc DÉTRUIT, attendre 25 s ; puis `rescan` | Après 25 s il est **toujours** listé (rescan auto ne l'efface pas) ; après `rescan` il disparaît. |
| AVA-11 | Pas de groupe `ALARMES_CRITIQUES` | Echo « ATTENTION : groupe ALARMES_CRITIQUES introuvable » ; le rapport fonctionne. |
| AVA-12 | Cockpit `Role=Avaries` `Surface=1` ; puis `Surface=9` | Rapport sur la 2ᵉ surface seulement ; avec 9 : Echo « Écrans Role=Avaries : 0 (1 Surface=N invalide) ». |
| AVA-13 | Vaisseau amarré endommagé ; un de ses gyrophares dans le groupe | Ses dégâts n'apparaissent pas ; son gyrophare n'est pas piloté. |
| AVA-14 | **Meuler** partiellement un bloc sous sa ligne rouge (sans tir) | Non signalé (`CurrentDamage = 0`) — limite documentée « Critère endommagé ». |
| AVA-15 | Abîmer uniquement de l'armure | Non signalé (blocs non terminaux) : utiliser le [[Diagnostic Structurel par Projecteur]]. |
| AVA-16 | `ack` en minuscules ; argument `test` | Accepté ; « Commande inconnue : 'test' (attendu : ACK ou rescan) ». |
| AVA-17 | Pendant une alarme, ajouter un 2ᵉ bloc sonore au groupe | Il se met à jouer au rescan suivant (≤ 10 s) ; le 1ᵉʳ ne redémarre pas. |
| AVA-18 | Sauvegarder avec un bloc H.S. acquitté, recharger | L'alarme se rallume (ACK non sauvegardé, documenté). |
| AVA-19 | Détacher une tête de rotor portant un bloc terminal | Ses blocs passent « [DÉTRUIT] » (documenté) ; `rescan` les efface. |

### 4.3 Banc d'essai et procédure

Banc : base + PB `PB Avaries` + LCD `[Ecran] Role=Avaries` + cockpit + 1 gyrophare + 1 bloc sonore (un son choisi, en boucle) dans le groupe `ALARMES_CRITIQUES` + quelques blocs « cibles » (portes, lumières, panneaux) + fusil de poing.

- [ ] Installer ; Echo « === CONTRÔLE DES AVARIES === », « Bus : autonome (aucun bloc SERVEUR_ALARMES) ».
- [ ] **AVA-11** avant de créer le groupe, puis créer le groupe et `rescan`.
- [ ] **AVA-01 → AVA-06** en abîmant progressivement.
- [ ] **AVA-07 → AVA-10** (ACK / réarmement / rescan).
- [ ] **AVA-12 → AVA-19**.

---

## 5. Surveillance Carburant Uranium (`GestionnaireCarburant.cs`)

Note : [[Surveillance Carburant Uranium]]

### 5.1 Comportements à tester

Mesure de l'uranium (réacteurs / conteneurs), puissance des réacteurs, deux autonomies, alerte à ≤ 1 h, réarmement > 1,5 h (hystérésis), `ACK` avec réarmement, réacteurs « à vérifier », écrans `Role=Carburant`, groupe `ALARMES_URANIUM` (vert), construction.

> [!tip] Maîtriser la consommation
> Charge connue : passer les batteries en mode **Recharger** (le réacteur fournit leur courant de charge). Lire la consommation exacte dans Echo (« Consommation : x MW ») et en déduire l'uranium à mettre : `kg = heures visées × MW`. Pour empêcher le réacteur d'aspirer l'uranium des conteneurs, décocher **« Utiliser le système de convoyeurs »** sur le réacteur.

### 5.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| CAR-01 | Aucun réacteur | Rapport « Aucun réacteur détecté. » ; pas d'alarme. |
| CAR-02 | Réacteur allumé, aucune charge | « Autonomie (réacteurs) : INFINIE (aucune consommation) ». |
| CAR-03 | Charge L MW, U kg dans le réacteur | « Autonomie (réacteurs) » = U / L heures, format « 2 h 05 min ». Vérifier le calcul à la main avec les valeurs Echo. |
| CAR-04 | Ajouter de l'uranium dans un conteneur | « Uranium en stock » augmente ; « Autonomie (totale) » > « Autonomie (réacteurs) ». |
| CAR-05 | Autonomie totale ≤ 1 h | « /!\ ALERTE CARBURANT /!\ » ; gyrophare du groupe allumé **vert** ; Echo « Alarme (Carburant) : ACTIVE ». |
| CAR-06 | En alerte, remonter à ~1 h 15 ; puis à > 1 h 30 | À 1 h 15 l'alerte **reste** ; au-delà de 1 h 30 elle s'éteint. |
| CAR-07 | `ACK` pendant l'alerte ; puis baisser encore ; puis remonter > 1 h 30 et redescendre < 1 h | Après ACK : gyrophare éteint, rapport « (Acquittée) » ; reste éteint en baissant ; après le cycle remontée/descente, l'alarme se rallume. |
| CAR-08 | `ACK` sans alerte | « ACK : aucune alerte en cours. » ; aucun effet. |
| CAR-09 | Réacteur éteint / vide / endommagé ; 10 réacteurs à problème | « Réacteurs à vérifier (n) : - Nom : ÉTEINT / SANS URANIUM / ENDOMMAGÉ » ; au-delà de 8 : « ... et 2 autres ». |
| CAR-10 | **Panne sèche** : vider l'uranium (réacteurs et stock) sous charge, batteries en relais | Attendu métier : l'alarme reste allumée. Attendu **code** : la puissance des réacteurs tombe à 0 → autonomie « INFINIE » → l'alerte s'éteint et l'ACK est oublié. Voir écart **E2**. |
| CAR-11 | Uranium dans un connecteur ou une raffinerie | Non compté (seuls les conteneurs cargo). |
| CAR-12 | Vaisseau amarré avec réacteur et uranium | Ni sa puissance ni son uranium ne sont comptés. |
| CAR-13 | LCD `Role=Carburant` + cockpit `Surface=2` ; puis `Surface=9` | Même rapport sur les deux ; avec 9 : « (1 Surface=N invalide) ». |
| CAR-14 | Pas de groupe ; lumière nommée `[giro-alarme]` hors groupe | « ATTENTION : groupe ALARMES_URANIUM introuvable » ; la lumière `[giro-alarme]` n'est **pas** pilotée (migration). |
| CAR-15 | Nouveau réacteur + `rescan` ; argument `zz` | Compté immédiatement (« Rescan effectué. ») ; « Commande inconnue : 'zz' (attendu : ACK ou rescan) ». |
| CAR-16 | Meuler un réacteur entre deux rescans | Pas d'exception attendue (voir point à confirmer **P2**) ; compte corrigé au rescan suivant. |

### 5.3 Banc d'essai et procédure

Banc : base avec 1 grand réacteur (convoyeurs décochés), 2 grandes batteries, 1 conteneur, PB `PB Carburant`, LCD `Role=Carburant`, 1 gyrophare + 1 bloc sonore dans `ALARMES_URANIUM`.

- [ ] **CAR-01** avant de construire le réacteur.
- [ ] **CAR-02 / 03** : batteries en Recharge, noter la consommation, mettre U kg.
- [ ] **CAR-04 → CAR-08** : ajuster l'uranium (dans le réacteur ou le conteneur) pour viser 0,8 h, 1,25 h, 2 h.
- [ ] **CAR-09 → CAR-16**.

---

## 6. Bus d'Alarmes (`SERVEUR_ALARMES.ini`)

Note : [[Bus d'Alarmes]]. Les scénarios combinant les deux scripts sont en §10.2 ; ici, le bus vu d'**un seul** script.

| ID | Scénario | Résultat attendu |
|---|---|---|
| BUS-01 | Pas de bloc `SERVEUR_ALARMES` | Echo « Bus : autonome (aucun bloc SERVEUR_ALARMES) » ; couleur propre du script. |
| BUS-02 | Serveur avec le `.ini` | Echo « Bus : connecté, alarme globale inactive » ; en alarme : « … ACTIVE (Avaries) ». Le Custom Data montre `Avaries=true` puis `false`. |
| BUS-03 | Custom Data **vide** | Le script ajoute `[Etat]` et `[Couleurs]` (sa source et sa couleur), pas `[Config]`. |
| BUS-04 | Custom Data invalide (`[Etat` sans `]`) | Echo « Bus : Custom Data de SERVEUR_ALARMES illisible (format INI) » ; fonctionnement autonome. |
| BUS-05 | Serveur construit après le PB | Connecté en ≤ 10 s ou avec `rescan`. |
| BUS-06 | Serveur uniquement sur le vaisseau amarré | Ignoré : « autonome ». |
| BUS-07 | `[Couleurs] Avaries=0,0,255` | Gyrophare **bleu** pendant l'alarme Avaries. Composantes hors bornes (`300,-5,0`) → bornées (255,0,0). |

- [ ] BUS-01 → BUS-07 avec le PB Avaries seul (panneau de contrôle nommé `SERVEUR_ALARMES`).

---

## 7. Diagnostic Structurel par Projecteur (`DiagnosticStructurel.cs`)

Note : [[Diagnostic Structurel par Projecteur]]

### 7.1 Comportements à tester

Recherche des projecteurs (`PROJECTEUR_REPARATION` ou balise `[DIAG]`, tri par nom), états (aucun projecteur, inactif, plan vide, intègre, brèches, désaligné), barre et pourcentage, écrans (`LCD_SCANNER`, `Role=Structure`, `Surface`), modes `scan` / `auto` / `manuel` / `stop` / `rescan`, mémorisation du mode (`Storage`), écriture forcée sur action du joueur, construction.

### 7.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| DIA-01 | Aucun projecteur | Écran et Echo : « AUCUN PROJECTEUR TROUVÉ » + aide (`rescan`). |
| DIA-02 | Projecteur éteint | « ÉTAT : PROJECTION INACTIVE » + aide. |
| DIA-03 | Plan chargé, aligné, vaisseau intact | « Intégrité : [####################] 100.0 % » / « ÉTAT : INTÈGRE ». |
| DIA-04 | Meuler 1 bloc d'armure | Barre **non pleine** (19 `#`), « BRÈCHES DÉTECTÉES », « Blocs volatilisés : 1 sur N », « 1 bloc(s) prêt(s) à être soudé(s). ». Ressouder → INTÈGRE. |
| DIA-05 | Décaler la projection d'un bloc | Intégrité basse ou 0 % ; si tout manque : « projection désalignée ? » ; « ATTENTION : aucun bloc manquant n'est rattaché à la coque. ». |
| DIA-06 | Plan vide (si réalisable : plan d'un seul bloc déjà construit puis supprimé du plan) | « ÉTAT : PLAN VIDE ». Sinon marquer « non réalisable ». |
| DIA-07 | 2 projecteurs `B [DIAG]` et `A [DIAG]` ; un 3ᵉ `C [diag]` | Rapports A puis B (ordre alphabétique) ; `[diag]` en minuscules **non** détecté (balise sensible à la casse). |
| DIA-08 | Écrans : LCD `LCD_SCANNER` (sans Custom Data), cockpit `Role=Structure` `Surface=1`, puis `Surface=9`, puis Custom Data invalide | Rapport sur le LCD et la surface 1 ; Echo « ERREUR : Surface=9 inexistante sur Cockpit (0 à 3) » ; « ERREUR : Custom Data INI invalide : Nom ». |
| DIA-09 | `auto` puis écrire un texte à la main sur le LCD | En auto, le texte manuel reste tant que le rapport ne change pas ; `scan` le réécrit aussitôt. Echo « mode AUTO ». |
| DIA-10 | `auto`, sauvegarder, recharger ; idem en `manuel` | Le mode est conservé après rechargement. |
| DIA-11 | `stop` puis meuler un bloc | Plus de mise à jour automatique ; `scan` met à jour. |
| DIA-12 | Argument `xyz` | « Argument inconnu : 'xyz'. Utilisez : scan, auto, manuel, stop, rescan. » |
| DIA-13 | Vaisseau amarré avec `PROJECTEUR_REPARATION` | Ignoré. |
| DIA-14 | Bloc **endommagé** (tir) mais présent | Toujours « INTÈGRE » (complémentarité avec les Avaries). |
| DIA-15 | Meuler le projecteur en mode auto | Jusqu'à 10 s d'ancien rapport possible (limite documentée) ; puis « AUCUN PROJECTEUR TROUVÉ ». |
| DIA-16 | Echo en auto | « Scan précédent : il y a 1 s », « Rapport inchangé depuis : … » qui augmente. |

### 7.3 Banc d'essai et procédure

Banc : petit vaisseau (~30 blocs, dont armure) + projecteur `PROJECTEUR_REPARATION` (plan sauvegardé intact via Ctrl+B, « Garder la projection » coché) + LCD `LCD_SCANNER` + cockpit + PB.

- [ ] **DIA-01** avant de construire le projecteur ; puis **DIA-02 → DIA-06**.
- [ ] **DIA-07 / 08** (écrans et projecteurs multiples).
- [ ] **DIA-09 → DIA-16**.

---

## 8. Sas Automatique (`GestionnaireSas.cs`)

Note : [[Sas Automatique (Airlock)]]

### 8.1 Comportements à tester

| # | Comportement |
|---|---|
| B1 | Reconnaissance des balises `[SAS-Nom-int/ext/vent/lcd]` (dernier tiret, type de bloc vérifié), sas incomplets |
| B2 | « Door bump » : front montant de l'ouverture → cycle si le niveau d'O2 ne convient pas |
| B3 | Cycle : 2 portes fermées, porte cible verrouillée, ouverture en fin de cycle |
| B4 | Changement d'avis (taper la porte de départ) |
| B5 | Délai sans progrès 15 s → ÉCHEC (cause probable), portes déverrouillées, mode manuel 30 s |
| B6 | PANNE (porte endommagée, aucune ventilation `IsWorking`) |
| B7 | Verrouillage croisé ; porte verrouillée seulement une fois fermée ; deux portes ouvertes → intérieure fermée en priorité |
| B8 | Commandes `rescan`, `cycle`, `entrer`, `sortir` (+ nom, casse) |
| B9 | Rescan 10 s sans perdre l'état ; rescan immédiat si bloc disparu |
| B10 | Écran : barre O2, chrono, alerte, état des portes ; écrit si changement |

### 8.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| SAS-01 | Sas complet `Test` | Echo « Sas gérés : 1 », « - Test : PRÊT ». Écran « === SAS : TEST === ». |
| SAS-02 | Ventilation absente ; puis écran nommé `[SAS-Test-int]` à la place de la porte | « Sas incomplets (porte int/ext ou ventilation manquante) : Test » (un écran ne peut pas servir de porte). |
| SAS-03 | Nom avec tirets `[SAS-Pont-B-…]` ; balises de casse différente `[SAS-Test-int]` / `[SAS-test-ext]` | `Pont-B` géré ; « Test » et « test » = deux sas **incomplets**. |
| SAS-04 | Sortie : sas plein, taper la porte **ext** | Les deux portes se ferment ; « ÉTAT : DÉPRESSURISATION », ext « VERROUILLÉE » ; la barre O2 baisse ; à ≤ 1 % les deux portes fermées → ext s'ouvre, « PRÊT », int verrouillée tant que ext est ouverte. |
| SAS-05 | Entrée : sas vide, taper la porte **int** | « PRESSURISATION » ; à ≥ 99 % → int s'ouvre. |
| SAS-06 | Sas déjà vide, taper ext | La porte s'ouvre normalement ; int fermée puis verrouillée. |
| SAS-07 | Pendant une dépressurisation, taper int | Passage en « PRESSURISATION » (changement d'avis). |
| SAS-08 | **O2 plein** : réservoirs pleins (ou éteints), sas plein, taper ext | Pas de progrès → au bout de 15 s : « /!\ ÉCHEC : réservoirs O2 pleins ? » + « Portes déverrouillées (mode manuel). » ; le joueur peut sortir (perte d'air assumée) ; après 30 s et deux portes fermées → « PRÊT ». |
| SAS-09 | Réservoirs O2 vides, générateur coupé, sas vide, taper int | 15 s → « ÉCHEC : plus d'O2 (réservoirs vides ?) ». |
| SAS-10 | Sas non étanche (meuler un bloc de paroi) | Au repos, portes fermées : « /!\ ALERTE : FUITE (sas non étanche) » ; taper int → pressurisation → « ÉCHEC : sas non étanche (fuite) ? ». |
| SAS-11 | Ventilation éteinte ; puis porte int abîmée sous la ligne rouge | « PANNE : aucune ventilation opérationnelle (énergie ? dégâts ? éteinte ?) » / « PANNE : porte intérieure endommagée » ; `sortir Test` → « Sas Test en panne : commande refusée. » ; réparé → « PRÊT ». |
| SAS-12 | Commandes : `sortir Test`, `ENTRER test`, `cycle` (1 sas), `cycle` (2 sas), `sortir Inconnu`, `sortir Test` ext ouverte, `danse` | Cycle lancé ; casse ignorée ; `cycle` : plein → sortie, vide → entrée, en cours → inverse ; avec 2 sas : « Sas introuvable : «  ». Sas gérés : … » ; « Sas introuvable : « Inconnu » … » ; « porte extérieure déjà ouverte. » ; « Commande inconnue : « danse » (rescan, cycle, entrer, sortir). ». |
| SAS-13 | Relancer après ÉCHEC avec `sortir Test` | Nouveau cycle immédiat (sortie du mode manuel). |
| SAS-14 | Ouvrir **les deux** portes par le terminal, puis recompiler | La porte int se ferme en premier, puis est verrouillée seulement une fois fermée ; aucune porte ne reste bloquée entrouverte. |
| SAS-15 | `rescan` au milieu d'une dépressurisation | Le cycle continue (état conservé). |
| SAS-16 | Meuler le LCD du sas ; puis l'unique ventilation | LCD : rescan immédiat, sas toujours géré ; ventilation : sas « incomplet ». |
| SAS-17 | Sas complet sur le vaisseau amarré | Non géré, absent de la console. |
| SAS-18 | Cockpit nommé `[SAS-Test-lcd]` | Affichage sur sa surface 0. |
| SAS-19 | Porte de hangar comme `-ext` | Fonctionne comme une porte simple. |
| SAS-20 | Grand sas, 1 seule ventilation (progrès lent mais régulier) | « Sans progrès : n / 15 s » repart à 0 à chaque baisse de 2 % ; pas d'ÉCHEC tant que ça progresse. |

### 8.3 Banc d'essai et procédure

Banc (dans l'espace) : pièce étanche 1×1×2 en armure lourde, porte `Porte [SAS-Test-int]` vers une base pressurisée, porte `Porte [SAS-Test-ext]` vers l'espace, ventilation `Vent [SAS-Test-vent]` à l'intérieur, LCD `Écran [SAS-Test-lcd]`, réservoir O2 **non plein** + générateur O2 avec glace, alimentation, PB `PB Sas`.

- [ ] Installer ; **SAS-01 / 02 / 03**.
- [ ] **SAS-04 → SAS-07** en jouant le personnage (casque **fermé**).
- [ ] **SAS-08** : remplir le réservoir à 100 % (ou l'éteindre) avant de taper ext.
- [ ] **SAS-09 → SAS-20**.

---

## 9. Fermeture Automatique des Portes (`FermeturePortes.cs`)

Note : [[Fermeture Automatique des Portes]]

### 9.1 Comportements à tester

Fermeture après 2 s (temps de jeu), `[Auto:x]` (point/virgule, bornes, casse), `[NoAuto]`, portes de hangar exclues sauf `[Auto:x]` valide, `[SAS-` ignoré, chrono remis à zéro à la fermeture manuelle, `pause`/`reprise` (+ `Storage`), `rescan`, rescan 10 s, porte disparue, construction.

### 9.2 Scénarios et résultats attendus

| ID | Scénario | Résultat attendu |
|---|---|---|
| FER-01 | Porte sans balise ouverte | Se referme après ~2 s (± 0,2 s). |
| FER-02 | `[Auto:5]`, `[Auto:2,5]`, `[Auto:2.5]`, `[auto:5]` | ~5 s, ~2,5 s, ~2,5 s, ~5 s. |
| FER-03 | `[Auto:abc]`, `[Auto:0.1]`, `[Auto:5` (sans `]`) | Echo « Attention : Nom : délai illisible » / « délai hors bornes (0.5 à 3600 s) » / « balise [Auto: non fermée » ; délai 2 s appliqué. |
| FER-04 | `[NoAuto]` et `[noauto]` | Jamais refermée ; comptée dans « ignorées ». |
| FER-05 | Porte de hangar sans balise ; avec `[Auto:30]` ; avec `[Auto:abc]` | Jamais ; ~30 s ; jamais (balise invalide = pas d'inclusion). |
| FER-06 | Porte `[SAS-X-int]` (même avec `[Auto:5]`) | Jamais refermée. |
| FER-07 | Ouvrir, refermer à la main à 1 s, rouvrir | Le délai repart de zéro (fermeture ~2 s après la 2ᵉ ouverture). |
| FER-08 | `pause` / `off`, puis `reprise` / `on` ; `pause` + sauvegarde + rechargement | « EN PAUSE » : aucune fermeture, la console ne se met plus à jour ; reprise → fermetures et rescan ; après rechargement le script reste en pause. |
| FER-09 | Nouvelle porte ; renommage en `[NoAuto]` | Prise en compte en ≤ 10 s, ou tout de suite avec `rescan` (« Scan forcé terminé. »). |
| FER-10 | Portes du vaisseau amarré | Jamais refermées. |
| FER-11 | Porte **éteinte** restée ouverte | Reste ouverte (le script réessaie sans effet, documenté). |
| FER-12 | Meuler une porte ouverte avant son délai | Pas d'exception ; « Portes gérées » baisse (rescan immédiat ou périodique). |
| FER-13 | Argument `ouvre` | « Commande inconnue : « ouvre » (rescan, pause/off, reprise/on). » |
| FER-14 | Partie solo en pause (Échap) pendant qu'une porte est ouverte | Le délai n'avance pas pendant la pause. |

- [ ] Banc : 4 portes simples, 1 porte de hangar, 1 porte `[SAS-X-int]`, PB `PB Portes`. Dérouler **FER-01 → FER-14**.

---

## 10. Tests d'intégration entre scripts

### 10.1 Tri + Affichage + Dictionnaire

| ID | Scénario | Résultat attendu |
|---|---|---|
| INT-01 | Les 3 installés ; déverser 5 000 kg de minerai dans `Vrac` | Le tri déplace le minerai ; le **total** affiché ne varie pas pendant les transferts (seul l'emplacement change). |
| INT-02 | Raffinerie en marche | Le tri vide la sortie ; l'écran compte entrée + sortie : le total minerai baisse et le total lingots monte, sans disparition. |
| INT-03 | Dictionnaire modifié à chaud (`Iron=…, ferro` et `Iron=Fer (test)`) | Le coffre `{ore}[ferro]` reçoit le fer **et** l'écran affiche « Fer (test) », sans `rescan` ni recompilation. |
| INT-04 | Dictionnaire cassé (INI invalide) | Tri : `{minerai}` n'accepte plus rien ; écran en anglais ; les deux PB affichent l'erreur. Réparé → tout revient. |
| INT-05 | Serveur différent sur un vaisseau amarré (DIC-09) | Les deux scripts doivent utiliser le même serveur (celui de la base). Voir écart **E1**. |
| INT-06 | Coffre `[NoTri]` | Non vidé par le tri, mais **compté** par l'affichage. |

- [ ] Dérouler INT-01 → INT-06.

### 10.2 Contrôle des Avaries + Carburant via le Bus d'alarmes

Banc : les deux PB, un **gyrophare commun** placé dans `ALARMES_CRITIQUES` **et** `ALARMES_URANIUM`, un bloc sonore commun, un panneau de contrôle `SERVEUR_ALARMES` (`.ini` complet).

| ID | Scénario | Résultat attendu |
|---|---|---|
| INT-10 | **Sans** serveur, avarie critique **et** alerte carburant | Les deux Echo « autonome » ; le gyrophare commun alterne rouge/vert (conflit d'origine, attendu sans bus : démontre l'utilité du bus). |
| INT-11 | Avec serveur : avarie seule / carburant seul / les deux | Rouge / vert / **rouge** (priorité `Avaries,Carburant`), stable (pas de clignotement sur 30 s). |
| INT-12 | Les deux actives, `ACK` au PB Avaries | Le gyrophare reste allumé, passe au **vert** ; `ACK` au PB Carburant → tout s'éteint, le son s'arrête. |
| INT-13 | `Priorite=Carburant,Avaries`, les deux actives | Vert. |
| INT-14 | Custom Data vide | Les deux scripts ajoutent leur `[Etat]` et `[Couleurs]` ; sans `[Config]`, la couleur est celle de la première source active **dans l'ordre de `[Etat]`**, identique pour les deux scripts. |
| INT-15 | Custom Data invalide | Les deux repassent « illisible » / autonome ; le conflit INT-10 réapparaît sur le gyrophare commun. |
| INT-16 | `[Couleurs] Avaries=255;0;0` (mal formé), avarie active | Attendu note : couleur ignorée. Observé probable : le PB Avaries applique rouge (sa couleur), le PB Carburant applique **vert** (sa couleur propre) → le gyrophare commun alterne. Voir écart **E3**. |
| INT-17 | Supprimer le PB Carburant pendant que `Carburant=true` | L'alarme reste allumée (documenté) ; passer la clé à `false` à la main → s'éteint. |
| INT-18 | Bloc sonore commun, déclenchement d'une alarme | Le son démarre, éventuellement redémarre **une** fois (< 2 s), puis joue normalement. |
| INT-19 | Serveur sur le vaisseau amarré seulement | Les deux scripts restent « autonome ». |
| INT-20 | Une **porte** placée dans `ALARMES_CRITIQUES` (la note l'autorise) | À vérifier : hors alarme elle est **éteinte** (`Enabled=false`, donc verrouillée), allumée pendant l'alarme. Voir point **P1**. |

- [ ] Dérouler INT-10 → INT-20 (lire Echo des deux PB et le Custom Data du serveur à chaque étape).

### 10.3 Sas + Fermeture des portes

| ID | Scénario | Résultat attendu |
|---|---|---|
| INT-30 | Les deux scripts actifs, cycle complet de sortie puis d'entrée | Aucune porte `[SAS-` n'est refermée par la fermeture auto ; cycles identiques à SAS-04 / 05. |
| INT-31 | Sas en ÉCHEC (mode manuel), porte ext laissée ouverte | La fermeture auto ne la referme **pas** ; seul le verrouillage croisé du sas agit. |
| INT-32 | Porte normale de la base à côté du sas | Refermée après 2 s, sans effet sur le sas. |
| INT-33 | Fermeture en `pause` | Le sas fonctionne normalement. |
| INT-34 | Balise en minuscules `[sas-Test-int]` | Ni le sas ni la fermeture ne la considèrent comme porte de sas : sas incomplet **et** porte refermée après 2 s (comportement cohérent, à connaître). |
| INT-35 | Porte de sas avec `[NoAuto]` ou `[Auto:5]` en plus | Toujours ignorée par la fermeture (la balise `[SAS-` est testée en premier). |

- [ ] Dérouler INT-30 → INT-35.

### 10.4 Rôles d'écrans (un écran = un script)

Banc : les 5 scripts d'affichage installés (Affichage, Avaries, Carburant, Diagnostic, Sas) et 1 LCD par cas.

| ID | Scénario | Résultat attendu |
|---|---|---|
| INT-40 | LCD `[Ecran]` sans Role ; `Role=Stocks` ; `Role=Avaries` ; `Role=Carburant` ; `Role=Structure` | En-têtes respectifs : « === DATA CENTER … », « === DATA CENTER … », « === ÉTAT DES SYSTÈMES === », « === CONTRÔLE RÉACTEURS === », « === DIAGNOSTIC STRUCTUREL === ». Sur 60 s, **aucun** écran ne change de propriétaire. |
| INT-41 | Cockpit `[Ecran] Role=Avaries Surface=1` | Seule la surface 1 change ; l'Affichage ne touche à aucune surface du cockpit. |
| INT-42 | `Role=Avarie` (faute de frappe) | Personne n'écrit ; aucun script ne le signale (à savoir pour le dépannage). |
| INT-43 | LCD nommé `LCD_SCANNER` **avec** `[Ecran]` sans Role | Attendu convention : un seul script. Observé probable : Affichage **et** Diagnostic écrivent. Voir écart **E4**. |
| INT-44 | LCD `[SAS-Test-lcd]` **avec** `[Ecran]` sans Role | Même risque : Sas **et** Affichage écrivent. Voir écart **E4**. |
| INT-45 | Le PB Carburant lui-même : `[Ecran] Role=Carburant` dans **son** Custom Data, `Surface=0` | Le rapport s'affiche sur l'écran du PB (cas « PB à surfaces »). |
| INT-46 | `SERVEUR_DICTIONNAIRE` sur un LCD sans `[Ecran]` | Non piloté par l'Affichage (pas de section `[Ecran]`). |

- [ ] Dérouler INT-40 → INT-46.

---

## 11. Écarts repérés (note ↔ code)

> [!warning] Lecture du code uniquement
> Ces écarts viennent de la lecture du code ; ils sont à **confirmer en jeu** avec le scénario indiqué. Les blocs de code des notes sont identiques aux fichiers `.cs` (vérifié par comparaison), donc les numéros de ligne valent pour les deux.

### E1 — L'Affichage ne privilégie pas le `SERVEUR_DICTIONNAIRE` de sa propre construction

- **Promesse** : [[Serveur Dictionnaire]] §3 : « les scripts prennent le premier trouvé sur **leur** construction. Le serveur d'un vaisseau amarré n'est utilisé que si la base n'en a pas. »
- **Code** : `AffichageDataCenter.cs:350`
  ```csharp
  serveurDictionnaire = GridTerminalSystem.GetBlockWithName(NOM_SERVEUR_DICTIONNAIRE);
  ```
  Aucun test `IsSameConstructAs`. Le Tri le fait correctement (`TriInventaire.cs:297-316`).
- **Conséquence** : base + vaisseau amarré avec chacun un serveur → l'Affichage peut traduire avec le dictionnaire du vaisseau, le Tri avec celui de la base. **Test** : DIC-09 / INT-05.

### E2 — Carburant : l'alarme s'éteint à la panne sèche

- **Promesse** : [[Surveillance Carburant Uranium]] résumé : « déclenche le groupe `ALARMES_URANIUM` sous 1 h d'autonomie ».
- **Code** : `GestionnaireCarburant.cs:284` (`if (consommationMW <= 0.0) return double.PositiveInfinity;`) puis `:306-310` (`else if (enAlerte && autonomieTotaleHeures > SEUIL_REARMEMENT_HEURES) { enAlerte = false; alarmeAcquittee = false; }`).
- **Conséquence** : quand l'uranium est épuisé, `CurrentOutput` des réacteurs tombe à 0 → autonomie « INFINIE » → l'alerte et l'ACK sont annulés au moment le plus critique (idem si le joueur éteint tous les réacteurs). Seule la ligne « SANS URANIUM » reste visible. La note le présente comme « normal à l'arrêt » (§8) mais ne dit pas que l'alarme s'éteint. **Test** : CAR-10.

### E3 — Bus : une couleur mal écrite fait diverger les deux scripts

- **Promesse** : [[Bus d'Alarmes]] §2 : « ils calculent exactement la même chose : un gyrophare présent dans les deux groupes reçoit deux fois le même ordre » ; §6 : « Couleur ignorée ».
- **Code** : `ControleAvaries.cs:771-783` (copie identique dans `GestionnaireCarburant.cs`) : `LireCouleur` renvoie `couleurPropre` si la valeur est mal formée ; or `couleurPropre` est **rouge** dans un script et **vert** dans l'autre.
- **Conséquence** : `[Couleurs] Avaries=255;0;0` → le PB Avaries applique rouge, le PB Carburant applique vert pour la même source dominante : le gyrophare commun alterne (retour du conflit que le bus devait supprimer). Même effet si la source dominante n'a pas de clé `[Couleurs]` (source tierce). **Test** : INT-16.

### E4 — Rôles d'écrans : deux scripts sélectionnent des écrans par leur **nom**, sans regarder `[Ecran]`

- **Promesse** : [[Conventions du projet]] §4.2 : « Un écran ne doit être piloté que par **un seul** script. La clé `Role` décide lequel. »
- **Code** :
  - `DiagnosticStructurel.cs:322` : un LCD nommé `LCD_SCANNER` est pris quel que soit son Custom Data ;
  - `GestionnaireSas.cs:889-901` : tout bloc `[SAS-…-lcd]` est pris quel que soit son Custom Data ;
  - `AffichageDataCenter.cs:850` : tout écran avec `[Ecran]` et `Role` absent est pris.
- **Conséquence** : un LCD `LCD_SCANNER` ou `[SAS-X-lcd]` à qui l'on ajoute `[Ecran]` (sans `Role`) est piloté par deux scripts. Cas peu probable mais non protégé. **Test** : INT-43, INT-44.

### E5 — Message Echo du dictionnaire différent de celui annoncé (mineur)

- **Promesse** : [[Serveur Dictionnaire]] §3.4 : « leur console affiche `Dictionnaire : OK (...)` ».
- **Code** : `AffichageDataCenter.cs:1097-1099` affiche « Dictionnaire : N noms, M alias, K catégories » (seul le Tri affiche « OK (…) », `TriInventaire.cs:365`).
- **Conséquence** : cosmétique ; un utilisateur qui cherche « OK » dans l'Echo de l'Affichage ne le trouvera pas. **Test** : DIC-02.

### Points à confirmer (pas d'écart prouvé)

- **P1 — Portes dans `ALARMES_CRITIQUES`** : la note des Avaries cite des « portes » parmi les blocs d'alarme, mais le bus applique `Enabled = AlarmeGlobale` à tout bloc du groupe (`ControleAvaries.cs:737`) : hors alarme, une porte du groupe est **éteinte**, donc verrouillée. Comportement hérité ? À clarifier dans la note. Test INT-20.
- **P2 — Réacteur détruit entre deux rescans** : `GestionnaireCarburant.cs:236-246` lit l'inventaire et la puissance de chaque réacteur du cache sans vérifier qu'il existe encore (contrairement à l'Affichage qui teste `Closed`). Vérifier qu'aucune exception n'apparaît. Test CAR-16.

---

## 12. Bilan des scénarios

| Script / fichier | Nombre de scénarios |
|---|---|
| Tri Automatique (`TRI`) | 22 |
| Serveur Dictionnaire (`DIC`) | 12 |
| Affichage Data Center (`AFF`) | 18 |
| Contrôle des Avaries (`AVA`) | 19 |
| Carburant Uranium (`CAR`) | 16 |
| Bus d'Alarmes (`BUS`) | 7 |
| Diagnostic Structurel (`DIA`) | 16 |
| Sas Automatique (`SAS`) | 20 |
| Fermeture des Portes (`FER`) | 14 |
| Intégration (`INT`) | 6 + 11 + 6 + 7 = 30 |
| **Total** | **174** |

## Voir aussi

- [[00 - Sommaire]]
- [[Conventions du projet]]
- [[Bus d'Alarmes]] · [[Serveur Dictionnaire]]
