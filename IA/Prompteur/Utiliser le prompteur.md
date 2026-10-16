---
tags: [ia, prompteur, notice]
---
# Utiliser le prompteur

> [!summary] À quoi sert cette note ?
> Notice pour l'utilisateur. Le prompteur est un petit programme qui transmet vos messages à Claude ou à Big Pickle, affiche leur réponse et enregistre tout l'échange dans `IA/Journal/`. Un modèle local (qwen2.5:3b) choisit seulement **comment** envoyer le message : votre texte est transmis tel quel.

Retour au [[Lisez-moi IA]] · Format du journal : [[Journal des sessions]] · Choix du modèle local : [[Essais des modeles]].

---

## 1. Lancer

1. Vérifier qu'Ollama tourne : l'icône **Ollama** est dans la barre des tâches (elle démarre avec Windows). Sinon, lancer l'application Ollama.
2. Ouvrir un terminal **à la racine du dépôt** (`Space-Engineers-scripte`) et taper :

```powershell
powershell -ExecutionPolicy Bypass -File IA/Prompteur/prompteur.ps1
```

> [!info] Pourquoi `-ExecutionPolicy Bypass` ?
> Par défaut, Windows refuse de lancer un script PowerShell téléchargé ou écrit à la main. Cette option l'autorise pour ce lancement seulement, sans changer le réglage de l'ordinateur.

3. Le message `Prompteur prêt.` s'affiche. Pour sortir : `/quitter`. Pour l'aide : `/aide`.

## 2. Écrire un message

- **Sur une ligne** : écrire après `Vous:` et valider avec Entrée, comme dans une discussion.
- **Sur plusieurs lignes** (coller un message d'erreur, une liste…) : écrire `<<` seul, valider, coller ou écrire le texte, puis `>>` seul sur la dernière ligne.

Écrivez comme vous parleriez à Claude : le message lui arrive mot pour mot.

## 3. Lire la décision

Avant chaque envoi, une ligne indique le choix du prompteur :

```
→ Claude (rapide) · lecture · session en cours
```

| Partie | Valeurs | Signification |
|---|---|---|
| Destinataire | `Claude`, `Big Pickle` | Big Pickle si le message parle de git (commit, push, branche, issue…), sinon Claude. |
| Niveau (Claude) | `rapide`, `normal`, `expert` | Modèle Claude utilisé : Haiku (rapide, économique), Sonnet (par défaut), Opus (difficile, plus cher). |
| Mode | `lecture`, `edition` | En lecture, le modèle répond sans rien modifier. En édition, il peut modifier, créer ou commiter des fichiers. |
| Session | `session en cours`, `nouvelle session « sujet »` | En cours : le modèle se souvient de la discussion. Nouvelle : il repart de zéro et un nouveau fichier de journal est créé. |

**Confirmation** : si le mode est `edition` ou le niveau `expert`, le prompteur demande `Confirmer ? (o/n)`. Répondre `o` pour envoyer ; `n` annule, et vous pouvez renvoyer le message avec des préfixes (§4).

Ensuite s'affichent la réponse, puis une ligne de bilan :

```
--- relais : Claude · session cfcc6bef-… · coût 0,0642 $ · journal : IA\Journal\2026-10-16 13h45 - Claude - nombre de dossiers numérotés.md
```

## 4. Imposer un choix

Commencer le message par un ou plusieurs préfixes. Ils remplacent le choix du prompteur pour ce message.

| Préfixe | Effet |
|---|---|
| `/claude`, `/bp` | Destinataire : Claude ou Big Pickle |
| `/rapide`, `/normal`, `/expert` | Niveau de Claude |
| `/lecture`, `/edition` | Mode |
| `/nouveau` | Nouvelle session (nouveau sujet) |

Exemples :

```
/bp /lecture quel est le dernier commit ?
/expert pourquoi l'autonomie était infinie ?
/nouveau /edition ajoute un écran pour les batteries
/rapide /lecture relis la note Centrale d'Alarmes et dis-moi si elle est claire
```

## 5. Économiser

- **Le coût de chaque réponse de Claude est affiché** dans la ligne de bilan. Repère mesuré : une question courte en niveau `rapide` coûte environ **0,06 $**, surtout parce que Claude relit le contexte du projet (`CLAUDE.md`, consignes) à chaque appel. Big Pickle n'affiche pas de coût.
- **Une question simple → `/rapide`** si le prompteur a choisi `normal`.
- **`/expert` seulement quand c'est vraiment difficile** : c'est le niveau le plus cher.
- **Garder la session** tant que le sujet ne change pas : Claude se souvient de ce qui a été dit, inutile de tout réexpliquer. `/nouveau` seulement en changeant de sujet : la discussion précédente n'est plus relue.
- **Donner des chemins de fichiers plutôt que leur contenu** : Claude sait lire les fichiers du projet.
- **Un sujet par message** : les réponses sont plus courtes et plus justes.

## 6. Retrouver les échanges

- Tout est dans `IA/Journal/`, **un fichier par session** : `AAAA-MM-JJ HHhMM - Claude - sujet.md` (ou `Big Pickle`). Voir [[Journal des sessions]].
- Chaque échange y est daté, avec le mode, l'identifiant de session et le coût.
- **Avant de commiter le journal, le relire** : s'il contient une clé d'API, un mot de passe ou une donnée personnelle, la retirer. Une fois poussé sur GitHub, il est public pour tous ceux qui ont accès au dépôt.

## 7. Dépannage

| Symptôme | Cause | Que faire |
|---|---|---|
| `(modèle local indisponible : … — choix par défaut)` | Ollama est arrêté, ou le modèle `prompteur` n'existe plus. | Le message part quand même avec le choix par défaut (Claude, normal, lecture). Lancer Ollama ; si le problème reste, recréer le modèle (§8). |
| Le choix affiché ne convient pas | Le modèle local s'est trompé de niveau ou de mode. | Répondre `n` si une confirmation est demandée, puis renvoyer le message avec des préfixes (§4). |
| La réponse commence par `ERREUR` | Claude ou Big Pickle a échoué (budget, connexion, permission…). | Lire le message d'erreur ; il est aussi dans le journal. Réessayer, ou demander de l'aide à Claude en lui donnant l'erreur. |
| Claude a oublié la discussion | La session est retenue dans un fichier du dossier temporaire de Windows ; s'il a été vidé, une nouvelle session commence. | Rappeler le contexte en une phrase, ou citer le fichier de journal précédent. |
| Accents abîmés dans le terminal | Script modifié et réenregistré sans BOM. | Réenregistrer `prompteur.ps1` et `relais.ps1` en « UTF-8 avec BOM » (PowerShell 5.1 en a besoin). |

## 8. Recréer le modèle local

Si le modèle `prompteur` a été supprimé ou si le `Modelfile` a changé, à la racine du dépôt :

```powershell
ollama create prompteur -f IA/Prompteur/Modelfile
```

`ollama list` doit ensuite afficher `prompteur:latest`. Il faut aussi que `qwen2.5:3b` soit téléchargé (`ollama pull qwen2.5:3b`).

## Voir aussi

- [[Consignes prompteur]] : les règles de tri données au modèle local.
- [[Essais des modeles]] : pourquoi qwen2.5:3b et ce script plutôt qu'un agent opencode.
- [[Journal des sessions]] : différence entre `Journal/` et `Conversations/`.
