---
tags: [suivi, ia]
---
# Suivi de l'optimisation (reprise IA)

> [!info] À quoi sert ce fichier ?
> Si une session IA s'arrête (manque de tokens), la suivante lit ce fichier pour reprendre exactement où elle en était.

## Demande d'origine
Voir `.opencode/prompte/initial.md` : analyser les scripts et les `.md` existants **sans les modifier**, produire des versions optimisées dans `./optimisation_Claude` (C# du Bloc Programmable de Space Engineers) et une note Obsidian par script et par dictionnaire.

## État (2026-10-13)

| Étape | État |
|---|---|
| 00 - Conventions et Playbook IA | ✅ fait |
| 01 - Serveur Dictionnaire (.ini + .md) | ✅ fait |
| 02 - Tri Automatique (.cs + .md) | ✅ fait |
| 03 - Affichage Data Center (.cs + .md) | ✅ fait |
| 04 - Bus d'Alarmes (.ini + .md) | ✅ fait |
| 05 - Contrôle des Avaries (.cs + .md) | ✅ fait |
| 06 - Gestionnaire de Carburant (.cs + .md) | ✅ fait |
| 07 - Diagnostic Structurel (.cs + .md) | ✅ fait |
| 08 - Gestionnaire de Sas (.cs + .md) | ✅ fait |
| 09 - Fermeture Automatique des Portes (.cs + .md) | ✅ fait |
| Vérif : code des `.md` identique aux `.cs` | ✅ vérifié |
| 00 - Sommaire (référencé partout, manquant) | ✅ fait (session de reprise) |

## Passe de vérification par agents (demandée le 2026-10-13)

Les agents de `.opencode/agents` (écrits pour Java/Spring) sont utilisés avec leur rôle et leurs règles, adaptés au C# du Bloc Programmable. Rapports dans `optimisation_Claude/Revue/`.

| Agent | Périmètre | Rapport | État |
|---|---|---|---|
| reviewer A | 02 Tri, 03 Affichage, 01 Dictionnaire | `Revue/Revue - reviewer A.md` | ✅ fini : 0 bloquant, 2 majeurs (03 écrans reconstruits à chaque exécution ; 02 dictionnaire en erreur → coffres à alias vidés), 9 mineurs, 7 info |
| reviewer B | 05 Avaries, 06 Carburant, 04 Bus, 07 Diagnostic, 08 Sas, 09 Portes | `Revue/Revue - reviewer B.md` | ✅ fini : 0 bloquant, 3 majeurs (08 sas incomplet → porte verrouillée à vie ; 06 autonomie INFINIE à sec ; 05 rescan non budgété), 6 mineurs, 9 info |
| tester | les 7 scripts | `Revue/Scenarios de test.md` | ✅ fini : 174 scénarios, écarts E1-E5 |
| explainer | cohérence notes `.md` ↔ code | `Revue/Coherence documentation.md` | ✅ fini : 1 majeur (06 autonomie « INFINIE » à la panne sèche), 2 moyens (03 dictionnaire non filtré par construction), 9 mineurs, 0 lien cassé |
| Correction des problèmes confirmés (agent coder) | — | — | ⏸ en attente de l'accord de l'utilisateur |

### Liste consolidée des corrections (par priorité)

1. **06 Carburant** : réacteurs à sec → autonomie INFINIE, alarme coupée (`GestionnaireCarburant.cs:284`, 300-313). Vérifié dans le code, trouvé par 3 agents.
2. **08 Sas** : un sas devenu incomplet est retiré de `ListeDesSas` → porte verrouillée à vie (`GestionnaireSas.cs:905-914`, 494-502). Vérifié.
3. **02 Tri** : dictionnaire en erreur ou absent → alias vidés, le tri continue → coffres `{minerai}` vidés (`TriInventaire.cs:345-356`). Vérifié. Correction : mettre le tri en pause si le dictionnaire est invalide.
4. **05 Avaries** : rescan et tri final hors budget d'instructions (`ControleAvaries.cs:194-229`, 324-379).
5. **03 Affichage** : écrans reconstruits à chaque exécution à cause de l'énergie (`AffichageDataCenter.cs:308-311`, 805-809) ; `SERVEUR_DICTIONNAIRE` non filtré par construction (l. 350).
6. Mineurs : couleur mal formée dans le bus (rouge/vert qui alternent), `LCD_SCANNER` / `[SAS-lcd]` ignorent `Role`, fenêtre où les 2 portes du sas sont ouvertes (08:444-451), faux DÉTRUIT quand une sous-grille se détache, `CultureInfo` (09:290) à vérifier.
7. Documentation : appliquer les corrections de texte de `Revue/Coherence documentation.md`, puis recopier le code dans chaque note `.md` (doit rester identique au `.cs`).

Si la session s'arrête pendant les corrections : reprendre la liste ci-dessus au premier point non fait.
