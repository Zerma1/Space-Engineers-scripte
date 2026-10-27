---
tags: [space-engineers, production, automatise]
---

# Production Automatique

> [!summary] Gestion automatique d'assembleurs et raffineries
> Maintient les quantités d'objets dans une plage définie (min/max).
> - Production démarrée quand le stock tombe sous le seuil minimum
> - Production arrêtée quand le stock atteint le seuil maximum
> - Configuration par défaut dans le Custom Data du bloc Programmable

## Prérequis

- Bloc Programmable (Programmable Block) dans votre construction
- Optionnel : blocs assembleurs et raffineries à gérer
- Optionnel : bloc SERVEUR_DICTIONNAIRE pour la traduction des noms d'objets

## Installation

1. Copiez le fichier `ProductionAutomatique.cs` dans le dossier **Scripts** de votre construction sur le Bloc Programmable.
2. Collez le contenu du fichier `ProductionAutomatique.cs.md` dans la **Note** du script (via l'éditeur de notes du Bloc Programmable ou via l'interface de téléchargement).
3. Le script va détecter automatiquement tous les assembleurs et raffineries de votre construction.
4. Pour configurer, modifiez le **Custom Data** du Bloc Programmable en ajoutant une section `[Production]` (voir § Configuration).

## Configuration

Le script lise ses paramètres dans le **Custom Data** du Bloc Programmable. Ajoutez ou modifiez la section `[Production]` :

```ini
[Production]
; Quantité maximale d'objets en stock avant d'arrêter la production (défaut : 10000)
MaxQuantity=10000
; Quantité minimale qui déclenche le redémarrage de la production (défaut : 10% de MaxQuantity)
MinQuantity=1000
; Type d'objet à surveiller (subtype du jeu, ex: "Ammo", "Iron", "Nickel", "Uranium").
; Laissé vide = surveille TOUS les types de sortie (défaut).
;ItemType=Ammo
; Groupe de blocs optionnel à cibler (nom exact du groupe d'assembleurs/raffineries).
; Si omis, tous les assembleurs/raffineries de la construction sont gérés.
;TargetGroup=MonGroupeAssembleurs
```

**Sans section [Production]**, le script utilise les valeurs par défaut :
- MaxQuantity = 10 000
- MinQuantity = 10 % de MaxQuantity = 1 000
- ItemType = vide (tous types)

### Personnalisation par bloc (via Custom Data du bloc assembleur/raffinerie)

Chaque assembleur ou raffinerie peut avoir son propre Custom Data avec une section `[Production]` pour un contrôle fin. Les valeurs y définies écrasent celles du Bloc Programmable.

## Commandes

| Argument | Effet |
|---|---|
| `rescan` | Relit immédiatement tous les blocs et la configuration |
| `ACK` | Non utilisé par défaut (réservé au bus d'alarmes) |

## Fonctionnement

1. **Scan périodique** : Toutes les ~10 secondes (ou sur commande `rescan`), le script scanne la construction pour trouver tous les assembleurs et raffineries.
2. **Lecture du stock** : Pour chaque bloc de production, le lit l'inventaire de sortie et additionne les quantités par type d'objet.
3. **Décision** :
   - Si la quantité totale du type surveillé est **≥ MaxQuantity** → le bloc est **éteint** (production arrêtée)
   - Si la quantité totale du type surveillé est **< MinQuantity** → le bloc est **allumé** (production démarrée)
   - Si elle est entre les deux → l'état actuel du bloc est conservé (on n'agit pas)
4. **Mise à jour** : L'état (Enabled) du bloc est modifié seulement si cela change, pour éviter les écritures inutiles.

### Logique de seuil

- Par défaut : `MinQuantity = MaxQuantity / 10` (soit 10 % du maximum)
- Exemple avec valeurs par défaut (Max=10000, Min=1000) :
  - Stock ≥ 10 000 → production **arrêtée**
  - Stock < 1 000 → production **démarrée**
  - 1 000 ≤ Stock < 10 000 → aucun changement (état conservé)
- Si `ItemType` est défini, seul ce type d'objet compte dans le calcul ; les autres objets dans la sortie sont ignorés.
- Si `ItemType` n'est pas défini, **tous** les objets dans la sortie sont additionnés et comparés aux seuils.

## Optimisations

- **Cache de blocs** : Les listes d'assembleurs et raffineries ne sont recréées qu'au rescan (~ toutes les 10 s), pas à chaque exécution (~ toutes les 1,6 s).
- **Pas d'allocation inutile** : Les listes d'objets et les dictionnaires sont des champs réutilisés d'une exécution à l'autre.
- **Modification conditionnelle** : L'état `Enabled` du bloc n'est modifié que si la valeur change réellement.
- **Frequence Update100** : Le script s'exécute toutes les ~1,6 secondes (Update100), ce qui est réactif tout en restant dans la limite d'instructions (~50 000 par exécution).

## Limites connues

- Le script ne gère que les assembleurs et raffineries ; les autres blocs de production (foreuses, broyeuses) ne sont pas contrôlés.
- Si le stock contient plusieurs types d'objets et qu'`ItemType` n'est pas défini, tous sont additionnés. Cela peut masquer le fait qu'un type spécifique soit en excès ou en déficit.
- Le script ne tient pas compte des entrées d'inventaire (ce qui est en cours de production), seulement la sortie.
- Se bloc Programmable doit avoir `Runtime.UpdateFrequency = UpdateFrequency.Update100` (déjà défini dans `Program()`).

## Dépannage

### "Aucun assembler ou raffinery trouvé"
- Vérifiez que les blocs sont bien dans la même construction que le Bloc Programmable.
- Assurez-vous qu'ils ont des inventaires de sortie (les assembleurs et raffineries en ont).
- Utilisez la commande `rescan` pour rafraîchir la liste des blocs.

### La production ne s'arrête pas / ne démarre pas
- Vérifiez les valeurs `MaxQuantity` et `MinQuantity` dans le Custom Data du Bloc Programmable.
- Si `ItemType` est défini, vérifiez que le texte correspond bien au subtype d'un objet de sortie (ex. "Ammo" pour les munitions, "Iron" pour le fer).
- Utilisez `rescan` après avoir modifié la configuration.

### Performance : "Budget d'instructions atteint"
- Le script est optimisé pour rester bien sous la limite (~50 000 instructions), mais si vous avez des centaines d'assembleurs, réduisez la fréquence ou utilisez la commande `rescan` manuellement plutôt qu'automatiquement.

## Code complet

Le code complet du script se trouve dans le fichier `ProductionAutomatique.cs` (voir ci-dessus dans la section des fichiers du script).

## Voir aussi

- [[00 - Sommaire]] - Liste de tous les scripts du projet
- [[06 - Gestionnaire de Carburant]] - Script de surveillance énergétique (patterns de cache utilisés)
- [[Conventions du projet]] - Règles d'écriture et de performance pour le Bloc Programmable