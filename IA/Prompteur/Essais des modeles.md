---
tags: [ia, prompteur, essais]
---
# Essais des modèles pour le prompteur

> [!summary] À quoi sert cette note ?
> Garder les résultats des essais de modèles locaux pour le rôle de prompteur, avant de choisir et de remplacer quoi que ce soit. Les versions des fichiers en place avant chaque changement sont dans `Sauvegardes/`.

Retour aux [[Consignes prompteur]].

**Machine** : GPU NVIDIA RTX A1000 Laptop, **4 Go** de mémoire vidéo. Un modèle qui ne tient pas dans ces 4 Go (poids + contexte) tourne en partie sur le processeur et devient très lent.

## qwen3:8b (`qwen3-prompteur`, contexte 16 384) — 2026-10-16

| Essai | Résultat |
|---|---|
| Agent opencode par défaut (« build ») + « lis les consignes » | ❌ N'a pas lu les consignes ; a répondu lui-même (Glob, Read). Réponse juste par hasard, rien dans le journal. |
| Agent `prompteur` (consignes en instructions système, lecture interdite, shell autorisé) | ❌ A lancé sa propre commande PowerShell pour compter les dossiers. ~2 min par étape. |
| Agent `prompteur`, essai de l'utilisateur, message « niveau haiku » | ❌ A compris « écrire un haïku » (poème). Le mot `haiku` a été renommé en niveau `rapide`. Réflexion sauvegardée dans `Sauvegardes/…/relexion_qwant_prompteur.md`. |
| API Ollama, « Dis bonjour en un mot », sans `/no_think` | 167 s, 492 caractères de réflexion |
| Idem avec `/no_think` dans les instructions système | 316 s (Ollama partagé avec une autre session), 386 caractères de réflexion : **`/no_think` ne coupe pas la réflexion** |
| Occupation mémoire (`ollama ps`) | 7,8 Go, **70 % processeur / 30 % GPU** |

**Conclusion** : trop gros pour 4 Go et réfléchit toujours. Écarté pour ce rôle.

## llama3.2:3b et qwen2.5:3b (contexte 12 288) — 2026-10-16

Même test pour les deux : agent `prompteur`, message « Transmets à Claude, niveau rapide : « Combien de dossiers numérotés y a-t-il dans Scripts/ ? Réponds en une ligne. » ».

### llama3.2:3b (`prompteur-llama32`)

| Mesure | Résultat |
|---|---|
| Durée | 91 s |
| Occupation mémoire | 3,9 Go, **40 % processeur / 60 % GPU** (contexte 12 288 trop gros pour 4 Go) |
| `prompt.md` écrit | ❌ non |
| `relais.ps1` lancé | ❌ non : a appelé un outil `execute` inexistant avec du code inventé (`/opt('question', …)`) |
| Fidélité du message | ❌ accents abîmés (« numérotis », « Dossiɑrs ») |
| Réponse affichée | ❌ erreur reformulée et conseil inventé (« repositionner le fichier ») |

**Conclusion** : écarté.

### qwen2.5:3b (`prompteur-qwen25`)

Premier essai non concluant : opencode a répondu « Model unavailable: ollama/prompteur-qwen25:latest » (modèle créé après le démarrage du service opencode, pas encore dans sa liste). Deuxième essai :

| Mesure | Résultat |
|---|---|
| Durée | **11 s** |
| Occupation mémoire | 2,8 Go, **15 % processeur / 85 % GPU** |
| `prompt.md` écrit | ❌ non |
| `relais.ps1` lancé | ❌ non : a appelé l'outil `execute` avec du JavaScript inventé (`new Filesystem().ls(…)`) pour compter lui-même |
| Fidélité | ❌ a répondu « Je ne peux pas accéder aux fichiers… Comment puis-je vous aider ? » au lieu de transmettre |

**Conclusion** : rapide et tient presque entièrement dans le GPU, mais ne suit pas les consignes dans opencode.

## Bilan

- Aucun des trois modèles ne suit les consignes du prompteur **dans opencode** : tous essaient de répondre eux-mêmes. Les instructions d'opencode et ses nombreux outils (dont `execute`, non bloqué par les permissions de l'agent) sont trop pour un petit modèle.
- Pour la vitesse, **qwen2.5:3b** est le seul utilisable sur 4 Go (11 s, 85 % GPU) ; llama3.2:3b abîme en plus les accents.

## Solution retenue : `prompteur.ps1` + qwen2.5:3b (contexte 4 096, température 0)

- Le modèle ne fait que trier : il renvoie un JSON imposé par un schéma (niveau, mode, nouvelle session, sujet). **~3 s par message, 100 % GPU** (2,2 Go).
- 1er essai sur 10 messages : « repenser l'architecture du tri » envoyé à **Big Pickle en édition** ❌. Le destinataire est donc décidé par mots-clés git dans le script, plus par le modèle.
- 2e essai : 10/10 acceptables (relecture de note classée `rapide`, « écris un haiku » classé `edition` + nouvelle session : sans danger, l'édition demande confirmation).
- Bout en bout : « Combien de dossiers numérotés… » → Claude rapide → bonne réponse, journal correct (accents compris), 0,064 $, ~17 s.
- Modèles supprimés : qwen3:8b, llama3.2:3b et leurs variantes.

- Piste qui a mené à cette solution : ne plus confier au modèle l'appel des outils. Un script PowerShell lit le message, demande seulement au modèle **quel destinataire et quel niveau** (réponse JSON courte), puis appelle `relais.ps1` avec le message **original** : le modèle ne peut ni le modifier ni répondre à sa place.
