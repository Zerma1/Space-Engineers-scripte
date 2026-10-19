---
tags: [suivi, ia]
---
# Suivi de l'optimisation (reprise IA)

> [!info] À quoi sert ce fichier ?
> Si une session IA s'arrête (manque de tokens), la suivante lit ce fichier pour reprendre exactement où elle en était.

## Demande d'origine
Voir `.opencode/prompte/initial.md` : analyser les scripts et les `.md` existants **sans les modifier**, produire des versions optimisées dans `./optimisation_Claude` (renommé `Scripts` le 2026-10-16) (C# du Bloc Programmable de Space Engineers) et une note Obsidian par script et par dictionnaire.

## État (2026-10-13)

| Étape | État |
|---|---|
| Conventions et Playbook IA (séparés le 2026-10-16 : `Scripts/Conventions du projet.md` et `IA/Playbook IA.md`) | ✅ fait |
| 01 - Serveur Dictionnaire (.ini + .md) | ✅ fait |
| 02 - Tri Automatique (.cs + .md) | ✅ fait |
| 03 - Affichage Data Center (.cs + .md) | ✅ fait |
| 04 - Bus d'Alarmes (.ini + .md) | ✅ fait |
| 05 - Contrôle des Avaries (.cs + .md) | ✅ fait |
| 06 - Gestionnaire de Carburant (.cs + .md) | ✅ fait |
| 07 - Diagnostic Structurel (.cs + .md) | ✅ fait |
| 08 - Gestionnaire de Sas (.cs + .md) | ✅ fait |
| 09 - Fermeture Automatique des Portes (.cs + .md) | ✅ fait |
| 10 - Centrale d'Alarmes (.cs + .md) + criticité dans le bus (2026-10-15) | ✅ fait, non testé en jeu |
| 06 - Surveillance énergie : H2, uranium, glace, batteries + alarme Energie à escalade ; BusAlarmes multi-alarmes avec [Detail] ; correction « réacteurs à sec = autonomie INFINIE » (2026-10-15) | ✅ fait, non testé en jeu |
| Idées pour la suite (Centrale) : un écran par niveau `[alarme-line-Critique]`… ; défilement de `[alarme-line]` | ⏸ à faire |
| Vérif : code des `.md` identique aux `.cs` | ✅ vérifié |
| 00 - Sommaire (référencé partout, manquant) | ✅ fait (session de reprise) |
| Dossier `IA/` : playbook, suivi, revues et conversations entre modèles sortis de `optimisation_Claude` ; `CLAUDE.md` et `AGENTS.md` à la racine (2026-10-16) | ✅ fait |
| Conventions séparées du playbook (`Scripts/Conventions du projet.md` pour joueur, développeur et IA ; `IA/Playbook IA.md`) ; `optimisation_Claude` renommé `Scripts` (2026-10-16) | ✅ fait |
| Prompteur (essai `qwen3-prompteur` abandonné) : `IA/Prompteur/` (consignes, `relais.ps1`, `Modelfile`), journal `IA/Journal/` ; relais testé avec Claude et Big Pickle, remplacé par `IA/Prompteur/prompteur.ps1` : le modèle local (qwen2.5:3b) ne fait que trier, le script transmet le message original ; testé de bout en bout (2026-10-16), essais dans `Essais des modeles.md` | ✅ fait |
| Notice utilisateur [[Utiliser le prompteur]] (lancer, préfixes, coûts, journal, dépannage) ; liée depuis le Sommaire et Lisez-moi IA (2026-10-16) | ✅ fait |
| `ORCHESTRATOR.md` à la racine : rôles des modèles, appels, règle d'enregistrement ; option `-Agent` de `relais.ps1` pour les agents opencode (testée avec `explainer`) (2026-10-16) | ✅ fait |
| `ORCHESTRATOR.md` supprimé (sauvegarde : `IA/Prompteur/Sauvegardes/2026-10-16 15h40 - avant suppression ORCHESTRATOR/`) : un fichier par rôle dans `agents/` (`ORCHESTRATEUR.md`, `git.md`) ; `CLAUDE.md` et `AGENTS.md` conservés car chargés automatiquement par Claude Code et opencode, mis à jour pour pointer vers `agents/` (2026-10-16) | ✅ fait |
| Réorganisation committée en deux commits (« Renomme optimisation_Claude en Scripts et regroupe les fichiers IA dans IA/ » puis « Corrige le lien original du Serveur Dictionnaire »), sans push (2026-10-16) | ✅ fait |

## Passe de vérification par agents (demandée le 2026-10-13)

Les agents de `../agents` (écrits pour Java/Spring) sont utilisés avec leur rôle et leurs règles, adaptés au C# du Bloc Programmable. Rapports dans `IA/Revue/`.

| Agent | Périmètre | Rapport | État |
|---|---|---|---|
| reviewer A | 02 Tri, 03 Affichage, 01 Dictionnaire | `Revue/Revue - reviewer A.md` | ✅ fini : 0 bloquant, 2 majeurs (03 écrans reconstruits à chaque exécution ; 02 dictionnaire en erreur → coffres à alias vidés), 9 mineurs, 7 info |
| reviewer B | 05 Avaries, 06 Carburant, 04 Bus, 07 Diagnostic, 08 Sas, 09 Portes | `Revue/Revue - reviewer B.md` | ✅ fini : 0 bloquant, 3 majeurs (08 sas incomplet → porte verrouillée à vie ; 06 autonomie INFINIE à sec ; 05 rescan non budgété), 6 mineurs, 9 info |
| tester | les 7 scripts | `Revue/Scenarios de test.md` | ✅ fini : 174 scénarios, écarts E1-E5 |
| explainer | cohérence notes `.md` ↔ code | `Revue/Coherence documentation.md` | ✅ fini : 1 majeur (06 autonomie « INFINIE » à la panne sèche), 2 moyens (03 dictionnaire non filtré par construction), 9 mineurs, 0 lien cassé |
| Correction des problèmes confirmés (agent coder) | — | — | ⏸ en attente de l'accord de l'utilisateur |

### Liste consolidée des corrections (par priorité)

1. ✅ (corrigé le 2026-10-15) **06 Carburant** : réacteurs à sec → autonomie INFINIE, alarme coupée (`GestionnaireCarburant.cs:284`, 300-313). Vérifié dans le code, trouvé par 3 agents.
2. **08 Sas** : un sas devenu incomplet est retiré de `ListeDesSas` → porte verrouillée à vie (`GestionnaireSas.cs:905-914`, 494-502). Vérifié.
3. **02 Tri** : dictionnaire en erreur ou absent → alias vidés, le tri continue → coffres `{minerai}` vidés (`TriInventaire.cs:345-356`). Vérifié. Correction : mettre le tri en pause si le dictionnaire est invalide.
4. **05 Avaries** : rescan et tri final hors budget d'instructions (`ControleAvaries.cs:194-229`, 324-379).
5. **03 Affichage** : écrans reconstruits à chaque exécution à cause de l'énergie (`AffichageDataCenter.cs:308-311`, 805-809) ; `SERVEUR_DICTIONNAIRE` non filtré par construction (l. 350).
6. Mineurs : couleur mal formée dans le bus (rouge/vert qui alternent), `LCD_SCANNER` / `[SAS-lcd]` ignorent `Role`, fenêtre où les 2 portes du sas sont ouvertes (08:444-451), faux DÉTRUIT quand une sous-grille se détache, `CultureInfo` (09:290) à vérifier.
7. Documentation : appliquer les corrections de texte de `Revue/Coherence documentation.md`, puis recopier le code dans chaque note `.md` (doit rester identique au `.cs`).

Si la session s'arrête pendant les corrections : reprendre la liste ci-dessus au premier point non fait.
