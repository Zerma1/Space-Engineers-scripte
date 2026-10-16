---
tags: [ia, sommaire]
---
# Lisez-moi IA

> [!summary] À quoi sert ce dossier ?
> Tout ce qui sert aux modèles (Claude, Big Pickle, Gemini…) et pas au joueur : playbook, reprise entre sessions, échanges entre modèles, rapports d'agents. Les scripts, leurs notes et les [[Conventions du projet]] sont dans `Scripts/` (voir [[00 - Sommaire]]).

## À lire en début de session, dans cet ordre

1. [[Conventions du projet]] : contraintes du Bloc Programmable, règles de performance et d'écriture, noms de blocs réservés, rôles des écrans, ajout d'un script.
2. [[Playbook IA]] : comment un agent travaille sur le projet, répartition des rôles sur git.
3. [[Suivi IA]] : état du travail et liste des corrections restantes. Reprendre au premier point non fait.
4. [[Jalons du projet]] : historique et jalon en cours.
5. Les derniers fichiers de `Conversations/` s'il y a un échange en cours (voir [[Conversations entre modeles]]).

## Contenu

| Emplacement | Rôle |
|---|---|
| [[Playbook IA]] | Méthode de travail des agents et répartition git. |
| [[Suivi IA]] | Sauvegarde de l'état entre deux sessions. **À mettre à jour** à chaque étape terminée. |
| `Conversations/` | Consignes et comptes rendus échangés entre modèles. |
| `Journal/` | Prompts et réponses enregistrés en temps réel (voir [[Journal des sessions]]). |
| `Prompteur/` | Le prompteur local `qwen3-prompteur` : [[Consignes prompteur]], script `relais.ps1`, `Modelfile`. |
| `Revue/` | Rapports des agents : [[Revue - reviewer A]], [[Revue - reviewer B]], [[Scenarios de test]], [[Coherence documentation]]. |

`CLAUDE.md` (Claude Code) et `AGENTS.md` (opencode), à la racine du dépôt, renvoient ici : chaque outil les charge automatiquement au démarrage.
