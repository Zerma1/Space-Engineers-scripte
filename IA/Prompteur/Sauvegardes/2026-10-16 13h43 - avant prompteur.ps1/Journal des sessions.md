---
tags: [ia, journal]
---
# Journal des sessions

> [!summary] À quoi sert ce dossier ?
> Enregistrement en temps réel des prompts de l'utilisateur et des réponses des modèles (Claude, Big Pickle…). Les messages passent par le **prompteur** (le modèle local `qwen3-prompteur`, sous Ollama), qui lance le script `IA/Prompteur/relais.ps1`. C'est le script qui écrit le journal, mot pour mot : le prompteur n'y touche pas.

Retour au [[Lisez-moi IA]] · Consignes du prompteur : [[Consignes prompteur]].

## Différence avec `Conversations/`

| Dossier | Contenu |
|---|---|
| `Journal/` | Tout l'échange utilisateur ↔ modèle, brut et horodaté, écrit automatiquement. |
| `Conversations/` | Consignes ciblées entre modèles (ex. Claude → Big Pickle) et réponses de l'utilisateur, écrites à la main (voir [[Conversations entre modeles]]). |

## Format

- Un fichier par session : `AAAA-MM-JJ HHhMM - modele - sujet.md`. Une nouvelle session (option `-Nouvelle`) crée un nouveau fichier.
- Chaque échange est ajouté à la fin, avec l'heure, le mode (lecture / édition), l'identifiant de session et, pour Claude, le coût.
- Ne jamais y laisser de clé d'API ni de mot de passe : relire avant de commiter.

## Mise en route (une seule fois)

1. **Modèle local** : `ollama create qwen3-prompteur -f IA/Prompteur/Modelfile` (déjà fait le 2026-10-16). Il s'agit de `qwen3:8b` avec 16 384 tokens de contexte et une température basse.
2. **Agent opencode** : `.opencode/agents/prompteur.md` (local, non versionné). Ses instructions sont une copie de [[Consignes prompteur]] : **recopier** la note dans l'agent après chaque modification. Ses permissions lui interdisent de lire ou chercher dans les fichiers et ne l'autorisent à lancer **que** `relais.ps1` : il ne peut pas répondre à la place des modèles. opencode détecte Ollama tout seul, aucune configuration à ajouter.
3. **Lancer** : à la racine du dépôt, `ollama launch opencode --model qwen3-prompteur`, puis `Tab` jusqu'à l'agent **prompteur** (vérifier en bas de l'écran que le modèle est bien `qwen3-prompteur`, sinon le choisir avec `/models`). Écrire ensuite ses messages normalement.
