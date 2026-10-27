---
tags: [ia, conversations]
---
# Claude → reviewer : relecture de Production Automatique

Retour à [[Conversations entre modeles]].

## Demande (Claude, agent codeur)

Merci de relire, **sans rien modifier**, le nouveau script 11 écrit par le codeur le 2026-10-27.

### Contexte

- Ce n'est **pas** du Java/Spring : c'est du C# pour le **Bloc Programmable de Space Engineers** (API `Sandbox.ModAPI.Ingame`). Ton rôle et ton format de rapport (`agents/reviewer.md`) restent valables, adaptés à ce langage.
- Contraintes et règles du projet : lire d'abord `Scripts/Conventions du projet.md` en entier.
  - Compilateur **C# 6** : interdits `out var`, `if (x is T t)`, tuples, fonctions locales, littéral `default`.
  - Le code est collé tel quel : pas de `namespace`, `using` ni `class Program`.
  - Limite d'environ 50 000 instructions par exécution ; 100 000 caractères au plus.
- Demande de l'utilisateur : « si l'utilisateur veut avoir en permanence X munitions pour un maximum de Y (X<=Y), la production de munitions est automatique en dessous de ces X et s'arrête à Y pour ne pas remplir les stockages ou vider les ressources ; éventuellement avec des fabricateurs et/ou raffineries dédiés ».

### Fichiers à relire

| Fichier | Contenu |
|---|---|
| `Scripts/11 - Gestion de Production/ProductionAutomatique.cs` | le script |
| `Scripts/11 - Gestion de Production/Production Automatique.md` | sa note : mode d'emploi, limites, code complet |
| `Scripts/00 - Sommaire.md` | la ligne « Production Automatique » |

Pour comparer seulement : l'ancienne version, qui n'est pas du codeur et ne compilait pas, est sauvegardée dans `IA/Prompteur/Sauvegardes/2026-10-27 14h45 - avant reecriture production/`. Elle n'est pas à relire.

### Points à vérifier, par priorité

1. **Compilation C# 6 dans le Bloc Programmable** : syntaxe interdite, variable non déclarée, type introuvable, champ `readonly` réaffecté hors du constructeur. Vérifier aussi les deux champs tableau `readonly string[]` et la classe imbriquée `Quota`.
2. **API Ingame réelles.** Pour chaque appel ci-dessous, dire « existe », « n'existe pas » ou « non vérifiable », sans deviner :
   - `IMyAssembler.GetQueue(List<MyProductionItem>)`, `AddQueueItem(MyDefinitionId, MyFixedPoint)`, `RemoveQueueItem(int, MyFixedPoint)`, `CanUseBlueprint(MyDefinitionId)`, `Mode` / `MyAssemblerMode.Assembly` ;
   - `MyProductionItem.BlueprintId` et `.Amount` ;
   - `MyDefinitionId.TryParse(string, out MyDefinitionId)` et l'opérateur `==` sur `MyDefinitionId` ;
   - `new MyItemType(string, string)` ;
   - `IMyInventory.GetItemAmount(MyItemType)` ;
   - les conversions `(double)MyFixedPoint` et `(MyFixedPoint)int` ;
   - `IMyEntity.Closed`, `Runtime.CurrentInstructionCount` ;
   - `MyIni.GetSections(List<string>)` et `MyIniParseResult.LineNo` / `.Error` ;
   - `IMyBlockGroup.GetBlocksOfType<T>(List<T>, Func<T, bool>)`.
3. **Logique des quotas** dans `AppliquerQuotas`, `GererFileAssembleurs` et `RetirerDeLaFile` :
   - hystérésis : stock ≥ Maximum → arrêt ; stock ≤ Minimum → production ; entre les deux, la décision précédente est gardée ;
   - pas de surcommande : la quantité déjà en file est déduite ;
   - retrait de la file seulement quand le stock atteint le Maximum ;
   - une raffinerie partagée par plusieurs quotas reste allumée si au moins un quota la demande ;
   - cas limites : `Minimum = 0`, `Minimum = Maximum`, quota en erreur, assembleur détruit, assembleur en mode Désassemblage, configuration modifiée pendant un comptage.
4. **Comptage par lots** dans `CompterLotInventaires` et `Rescanner` : un passage peut-il mélanger deux listes de blocs ? Le budget d'instructions est-il respecté ? Un stock peut-il être compté deux fois, ou pas du tout ?
5. **Persistance** dans `Program()`, `Save()` et `LireConfiguration` : l'état « en production » est-il bien gardé après un rechargement de la partie et après une modification du Custom Data ?
6. **Conventions** (`Scripts/Conventions du projet.md`) :
   - performance §2 : cache, `IsSameConstructAs`, pas d'allocation dans la boucle, écritures conditionnelles, `TimeSinceLastRun`, Echo court ;
   - nommage en français sans accents dans les identifiants ;
   - `/// <summary>` sur chaque méthode ;
   - écran `[Ecran] Role=Production` ;
   - classe `EcransRole` identique à celle de `Scripts/05 - Controle des Avaries/ControleAvaries.cs`.
7. **Note `.md`** :
   - gabarit du §5 des conventions ;
   - bloc de code **identique** au `.cs` ;
   - affirmations de la note conformes au code ;
   - liens Obsidian `[[...]]` qui existent ;
   - section « Limites connues » honnête.

### Format de la réponse

- Pour chaque problème, donner les 7 champs de `agents/reviewer.md` : gravité (bloquant / majeur / mineur / info), fichier, ligne (`fichier.cs:123`), problème, preuve tirée du code, risque, recommandation.
- Séparer les **faits** des **suppositions**. Écrire « non vérifié » quand une API ne peut pas être confirmée.
- Finir par un verdict en une ligne : « prêt à commiter » ou « corrections nécessaires », avec la liste des numéros bloquants et majeurs.
- Mode lecture : ne modifier aucun fichier. La réponse est enregistrée dans le journal par `relais.ps1`. Le codeur fera les corrections.

## Envoi (pour l'orchestrateur)

Depuis la racine du dépôt :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File IA/Prompteur/relais.ps1 -Modele bigpickle -Agent reviewer -Mode lecture -Journal "IA/Journal/2026-10-27 14h30 - Orchestrateur - Production automatique.md" -Prompt "Lis et applique la consigne de IA/Conversations/2026-10-27 - Claude → reviewer - relecture Production Automatique.md (relecture du script 11 Production Automatique, lecture seule)."
```

## Compte rendu (reviewer)

_À remplir après la relecture._
