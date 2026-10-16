---
tags: [ia, journal]
---
# Journal des sessions

> [!summary] À quoi sert ce dossier ?
> Enregistrement en temps réel des prompts de l'utilisateur et des réponses des modèles (Claude, Big Pickle…). Les messages passent par le **prompteur** (script `IA/Prompteur/prompteur.ps1`) : le modèle local `prompteur` (qwen2.5:3b sous Ollama) choisit seulement le niveau et le mode, puis `relais.ps1` transmet le message **original** et écrit le journal mot pour mot.

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

## Utilisation

1. **Une seule fois** : `ollama create prompteur -f IA/Prompteur/Modelfile` (fait le 2026-10-16).
2. **Lancer**, à la racine du dépôt : `powershell -ExecutionPolicy Bypass -File IA/Prompteur/prompteur.ps1`, puis écrire ses messages après `Vous:`.
3. Avant chaque envoi, le prompteur affiche son choix, ex. `→ Claude (rapide) · lecture · session en cours`. Il demande confirmation pour le niveau `expert` et pour le mode `edition`.
4. Pour imposer un choix, commencer le message par un ou plusieurs préfixes : `/claude`, `/bp` (Big Pickle), `/rapide`, `/normal`, `/expert`, `/lecture`, `/edition`, `/nouveau`. Message sur plusieurs lignes : `<<`, le texte, puis `>>`. Sortie : `/quitter`.

Le destinataire est choisi par mots-clés (git, commit, push, branche… → Big Pickle) ; le reste par le modèle local selon [[Consignes prompteur]]. Résultats des essais de modèles : [[Essais des modeles]].