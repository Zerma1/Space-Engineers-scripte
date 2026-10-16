---
tags: [ia, playbook]
---
# Playbook IA

> [!summary] À quoi sert cette note ?
> Comment un agent IA (Claude, Big Pickle, Gemini…) travaille sur ce projet : par où commencer, comment créer un script, qui fait quoi sur git. Les règles de code elles-mêmes sont dans les [[Conventions du projet]], communes au joueur, au développeur et aux IA.

Retour au [[Lisez-moi IA]].

---

## 1. Créer un nouveau script

1. Lire les [[Conventions du projet]] en entier, puis un script existant proche (ex. [[Controle des Avaries]]) comme modèle.
2. Suivre la procédure du §5 « Ajouter un script » des conventions (gabarit de note, code identique au `.cs`, `[Ecran]`/`Role`, `BusAlarmes`, Sommaire).
3. **Anti-hallucination** : n'utiliser que des API Ingame réelles. En cas de doute, ne pas l'utiliser et le noter dans « Limites connues » de la note.
4. Mettre à jour [[Suivi IA]] quand le script est terminé.

## 2. Gestion de git : répartition des rôles

**Big Pickle** (IA sur opencode) gère tout ce qui touche à git et GitHub : commits et messages, branches, push, milestones, issues et pull requests. Claude supervise : il lance Big Pickle, relit le résultat (`git log`, `git show --stat`) et **propose un push** à l'utilisateur quand c'est pertinent (fin de lot cohérent, script terminé et documenté). Rien n'est poussé sans accord.

- Claude écrit le code (`.cs`) et la documentation (`.md`, `.ini`) ; il ne commite pas lui-même sauf demande explicite.
- Un seul acteur à la fois sur git : Claude signale la fin de ses modifications, puis Big Pickle commite.
- Commits ciblés par sujet (un script ou une note par commit), jamais de `git add -A` global.
- Messages en français, à l'indicatif présent (« Ajoute… », « Corrige… »), comme dans l'historique.
- `.obsidian/`, `.opencode/` et `.idea/` sont dans `.gitignore` : ne jamais les commiter.
- Les consignes et comptes rendus échangés entre Claude et Big Pickle vont dans `IA/Conversations/` (voir [[Conversations entre modeles]]).
