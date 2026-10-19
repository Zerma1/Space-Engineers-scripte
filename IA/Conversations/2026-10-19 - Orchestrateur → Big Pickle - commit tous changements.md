# Consigne pour Big Pickle (gestionnaire git)

## Demande

Commit de TOUS les changements non commités du dépôt (fichiers modifiés + non versionnés).

## Fichiers à commiter

### Fichiers modifiés (staged ou non) :
- `.gitignore`
- `AGENTS.md`
- `CLAUDE.md`
- `IA/Journal/2026-10-16 14h25 - Big Pickle - commit prompteur.md`
- `IA/Journal/Journal exemple.md`
- `IA/Lisez-moi IA.md`
- `IA/Prompteur/Consignes prompteur.md`
- `IA/Prompteur/relais.ps1`
- `IA/Suivi IA.md`
- `agents/ORCHESTRATEUR.md`

### Fichiers supprimés (staged) :
- `IA/Prompteur/Sauvegardes/2026-10-16 13h37 - avant choix du modele/*` (5 fichiers)
- `IA/Prompteur/Sauvegardes/2026-10-16 13h43 - avant prompteur.ps1/*` (7 fichiers)
- `IA/Prompteur/Sauvegardes/2026-10-16 13h49 - avant notice/*` (5 fichiers)

### Fichiers non versionnés :
- `IA/Conversations/2026-10-19 - Orchestrateur → Big Pickle - commit orchestrateur.md`
- `IA/Journal/2026-10-16 14h29 - Big Pickle (agent explainer) - test agent.md`
- `agents/coder.md`
- `agents/explainer.md`
- `agents/git.md`
- `agents/reviewer.md`
- `agents/tester.md`

## NE PAS commiter

- `.opencode/` (dans `.gitignore`, interdit par les règles du projet)

## Contraintes

- Commits ciblés par sujet (pas de `git add -A`)
- Pas de push
- Format de commit habituel du projet
- Messages en français, à l'indicatif présent
