# ORCHESTRATOR : qui fait quoi entre l'utilisateur et les modèles

Ce fichier fixe le rôle de chaque modèle du projet, la façon de l'appeler et **la règle d'enregistrement** : toute consigne donnée à un modèle, et sa réponse, doit être retrouvable dans `IA/Journal/`.

À lire par tous les modèles en début de session, avec `IA/Lisez-moi IA.md`.

> **État de la mise en place** : ce document décrit le fonctionnement visé. Le script d'orchestration (§3) reste à écrire ; en attendant, `IA/Prompteur/prompteur.ps1` et `relais.ps1` (ancien circuit, orchestrateur = qwen2.5:3b) restent en service. Le modèle de l'orchestrateur n'est pas encore choisi (§1).

---

## 1. Les acteurs

| Acteur | Modèle / outil | Rôle | Ne fait jamais |
|---|---|---|---|
| **Utilisateur** | — | Donne les demandes, valide les choix coûteux (`expert`, `edition`), **autorise chaque push**. | — |
| **Orchestrateur** (aussi prompteur) | Modèle léger via opencode, pas Claude. Candidats : DeepSeek V4.1 Flash (1er choix), GLM-5.3-Flash (2e). Choix final après le test du §6. | Reçoit la demande de l'utilisateur, la **découpe en tâches**, indique **quelle partie du message part à quel rôle** et dans quel ordre, puis rend la répartition en JSON au script. | Écrire dans le journal à la main, appeler un modèle lui-même, reformuler ou résumer une réponse, décider d'un push. |
| **Codeur** | Claude Code (**moi**) ; niveaux `rapide` = Haiku, `normal` = Sonnet, `expert` = Opus | Écrit le code (`.cs`) et la documentation (`.md`, `.ini`), met à jour `IA/Suivi IA.md`. | Commiter ou pousser. Orchestrer : il exécute les tâches qu'on lui confie. |
| **Reviewer** | Big Pickle avec l'agent `reviewer` (opencode) | Vérifie le travail du Codeur (lecture seule), rapport dans `IA/Revue/` si long. | Modifier des fichiers. |
| **Gestionnaire git** | Big Pickle (opencode, `opencode/big-pickle`) | Tout git et GitHub : commits, messages, branches, push, milestones, issues, pull requests. | Pousser sans accord de l'utilisateur ; modifier le code ou la doc au-delà de ce qu'on lui demande de commiter. |
| **Autres agents** | Big Pickle + `tester` ou `explainer` (`agents`) | `tester` : scénarios de test. `explainer` : explication du code. Lecture seule. Écrits pour Java/Spring : leur rappeler le contexte C# du Bloc Programmable. | Modifier des fichiers. |
| **Script d'orchestration** | PowerShell (à écrire, §3) | Seul à appeler les modèles, à écrire dans le journal et à recopier les réponses **mot pour mot**. Exécute la répartition dans l'ordre, relève les questions à l'utilisateur. | Interpréter ou modifier un texte. |

Principe : **le modèle pense, le script exécute.** L'orchestrateur ne fait que du tri et du découpage ; tout ce qui doit être exact (copie, appels, sessions, coût, ordre) est fait par le script.

## 2. Déroulé d'une demande

1. L'utilisateur écrit sa demande au script d'orchestration.
2. Le script l'envoie à l'orchestrateur avec `IA/Prompteur/Consignes prompteur.md` (consignes système).
3. L'orchestrateur répond en **JSON uniquement** : la liste des tâches, avec pour chacune l'extrait exact de la demande, le rôle destinataire et les tâches dont elle dépend.
   ```json
   [{"id":1,"extrait":"supprimer le fichier X situé à la racine","role":"codeur"},
    {"id":2,"extrait":"vérifier la suppression du fichier X","role":"reviewer","apres":[1]},
    {"id":3,"extrait":"commiter le tout","role":"git","apres":[1,2]}]
   ```
   Rôles autorisés : `codeur`, `reviewer`, `git`, `tester`, `explainer`.
4. Le script valide le JSON (rôles connus, dépendances sans boucle). Invalide : une relance, puis arrêt avec message à l'utilisateur. Il écrit dans le journal la demande et la **Répartition**, affichée aussi à l'écran.
5. Il lance les tâches dans l'ordre des dépendances ; une tâche dont une dépendance a échoué n'est pas lancée. Chaque réponse est recopiée telle quelle dans le journal.
6. Si une réponse contient une question à l'utilisateur (ex. « peut-on push ? »), le script s'arrête, la pose à l'utilisateur, note la réponse dans le journal et reprend avec la tâche suivante.

Le **push** n'est lancé que sur un « oui » explicite de l'utilisateur, jamais décidé par l'orchestrateur ni par un autre modèle.

## 3. Format du journal

Un fichier par session : `IA/Journal/AAAA-MM-JJ HHhMM - sujet.md`. Modèle : `IA/Journal/Journal exemple.md`. Structure :

```
### hh:mm:ss · Utilisateur → orchestrateur (via prompteur, mode)

<demande exacte>

#### Répartition
"extrait" → Rôle
"extrait" → Rôle

### Réponse agent

#### hh:mm:ss · Rôle . Modèle (session xxxx · coût 0,0xxx $)

<réponse exacte>
```

- Une question à l'utilisateur se met en gras en fin de réponse : `**question utilisateur : "…"**`.
- La réponse de l'utilisateur ouvre un nouveau bloc `### hh:mm:ss · Utilisateur → orchestrateur` avec un sous-titre `#### Réponse Utilisateur`, puis la suite des réponses des agents.
- Le coût n'est indiqué que pour Claude ; pour Big Pickle, seule la session.

Commandes du script : à documenter ici une fois écrit. D'ici là, le circuit actuel est :

| Qui | Vers | Commande (à la racine du dépôt) |
|---|---|---|
| Utilisateur | tous | `powershell -ExecutionPolicy Bypass -File IA/Prompteur/prompteur.ps1` |
| Un rôle | Claude | `… relais.ps1 -Modele claude -Niveau rapide\|normal\|expert -Prompt "…"` |
| Un rôle | Big Pickle / agent | `… relais.ps1 -Modele bigpickle [-Agent reviewer\|tester\|explainer] -Mode edition -Prompt "…"` |

**Interdit hors tests** : appeler `claude -p` ou `opencode run` directement pour une vraie consigne. Ces appels ne laissent aucune trace dans `IA/`.

## 4. Règle d'enregistrement

Pour chaque consigne donnée à un modèle, on doit pouvoir retrouver : **la date, qui l'a envoyée, à qui, la consigne exacte et la réponse exacte**.

1. **Toute consigne passe par le script** (§3). Il écrit dans `IA/Journal/` le message envoyé puis la réponse reçue, **mot pour mot**, avec l'heure, la session et le coût. Aucun modèle n'écrit lui-même dans `IA/Journal/`.
2. **Consigne longue ou qui demande un compte rendu** (commit, revue, correction en plusieurs étapes) : l'émetteur l'écrit d'abord dans `IA/Conversations/AAAA-MM-JJ - émetteur → destinataire - sujet.md`, avec une section « Demande » puis une section vide « Compte rendu (destinataire) ». Le message envoyé par le script est court et renvoie à ce fichier ; **le destinataire écrit son compte rendu à la suite**.
3. **Réponse de l'utilisateur à une question d'un modèle** : notée dans le journal (§3). Si la session n'est pas celle du script, un fichier `IA/Conversations/AAAA-MM-JJ - User → destinataire - sujet.md` cite la question puis la réponse.
4. **Session directe dans Claude Code** (sans orchestrateur) : elle n'est pas enregistrée automatiquement. À la fin, le Codeur reporte dans `IA/Suivi IA.md` ce qui a été décidé et fait. Les délégations faites pendant la session passent quand même par le script (règle 1).
5. **Vérification** : quand un modèle annonce un résultat (commit, correction, test), un autre rôle ou le Codeur le vérifie (`git log`, `git show --stat`, relecture du fichier) avant de le présenter à l'utilisateur. Une erreur relevée dans un compte rendu est signalée à l'utilisateur, pas corrigée en silence.
6. **Secrets** : relire `IA/Journal/` et `IA/Conversations/` avant de commiter. Aucune clé d'API, aucun mot de passe.
7. **Sauvegarde avant de remplacer** : avant de réécrire des fichiers non commités ou des résultats d'essais, les copier dans `IA/Prompteur/Sauvegardes/AAAA-MM-JJ HHhMM - motif/`.

## 5. Qui reçoit quelle consigne

| Modèle | Consignes chargées automatiquement | Consignes à lire |
|---|---|---|
| Codeur (Claude) | `CLAUDE.md` (racine) | `ORCHESTRATOR.md`, `IA/Lisez-moi IA.md`, `Scripts/Conventions du projet.md`, `IA/Playbook IA.md`, `IA/Suivi IA.md` |
| Gestionnaire git, Reviewer | `AGENTS.md` (racine) | Les mêmes, en particulier le §3 « Gestion de git » de `IA/Playbook IA.md`, puis le fichier de `IA/Conversations/` reçu |
| Agents opencode | Leur fichier `agents` (local, non versionné) | Le message reçu, qui doit rappeler le contexte : C# 6 du Bloc Programmable, `Scripts/Conventions du projet.md` |
| Orchestrateur | `IA/Prompteur/Consignes prompteur.md`, envoyées par le script comme instructions système à chaque message | — |

| Type de demande | Rôle | Mode | Trace |
|---|---|---|---|
| Question, explication, avis | Codeur (`rapide` ou `normal`) ou `explainer` | lecture | Journal |
| Écrire ou corriger du code, de la doc | Codeur (`normal`), puis Reviewer | edition (confirmé) | Journal ; `Suivi IA.md` mis à jour par le Codeur |
| Conception ou bug difficile | Codeur (`expert`, accord de l'utilisateur) | lecture, puis edition | Journal |
| Commit, branche, issue, milestone | Gestionnaire git | edition | Journal + `Conversations/` avec compte rendu |
| Push | Gestionnaire git, **après accord de l'utilisateur** | edition | Journal + `Conversations/` |
| Revue de code, scénarios de test | Reviewer / `tester` | lecture | Journal + rapport dans `IA/Revue/` |

## 6. Choix du modèle de l'orchestrateur

Test à faire avant de figer le choix : donner à DeepSeek V4.1 Flash et à GLM-5.3-Flash les mêmes demandes (celle de `Journal exemple.md` et deux demandes ambiguës) avec le prompt du §2. Garder celui qui sort du JSON valide à chaque fois, n'invente aucune tâche et respecte l'ordre des dépendances. Noter les résultats dans `IA/Prompteur/Sauvegardes/` (règle 7) puis remplacer la mention du §1.
