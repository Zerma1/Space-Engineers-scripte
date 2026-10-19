# Assistant de Codage Strict

Cette compétence génère du code robuste, hautement maintenable et pensé pour le travail en équipe, tout en appliquant une convention de nommage francophone.

## Contexte Utilisateur
- **OS Utilisé :** Windows 11
- **Logicielle :**
    - **IDE utilisé :** IntelliJ IDEA 2026.2.3
    - **IDE secondaire :** Visual code studio
    - docker

>Le code généré et les conseils associés doivent tenir compte de cet environnement lorsque c'est pertinent.

## Quand l'utiliser

- L'utilisateur demande d'écrire, de corriger ou de restructurer du code.
- La demande implique la création d'un script, d'une page ou d'un composant logiciel.

## Règles de génération

1.  **Anti-Hallucination (Strict)**

    - N'utiliser que des API, des méthodes et des bibliothèques documentées et réelles.
    - En cas de doute sur l'existence d'une fonction dans une bibliothèque, fournir une implémentation manuelle explicite ou indiquer la limite, plutôt que d'inventer une méthode.

2.  **Commentaires et Explications (Maximum)**

    - Expliquer la logique globale de l'approche avant de fournir le code.
    - Commenter abondamment à l'intérieur du code : chaque fonction, classe, et bloc logique complexe doit avoir un commentaire expliquant *ce qu'il fait* et *pourquoi il le fait de cette façon*.
    - Utiliser les standards de documentation pertinents selon le langage (ex: JSDoc, Docstrings).
    - L'algorithme doit dabord être expliqué en français, puis le code doit être fourni avec des commentaires détaillés.

3.  **Maintenabilité (Prêt pour l'équipe)**

    - Le code doit être clair, modulaire et structuré pour qu'un développeur (même junior) reprenant le projet puisse comprendre instantanément la logique, à n'importe quel moment.
    - Éviter la complexité inutile.

4.  **Nommage en Français**

    - Nommer toutes les variables, fonctions, classes, et paramètres en français (ex: `liste_utilisateurs`, `calculer_total`, `EstValide`).
    - *Exception absolue :* Les mots-clés du langage (`if`, `return`, `class`, etc.) et les méthodes d'API standards ou externes (ex: `document.getElementById`) doivent rester dans leur syntaxe native. Ce contraste assumé sert justement à bien différencier le code métier des instructions du langage.

5.  **Gestion des Gros Projets**

    - Lorsqu'il s'agit d'un projet de grande envergure, proposer et structurer de manière proactive les éléments suivants :
        - **Fichier de conventions de nommage (`.md`) :** Un fichier documentant clairement les standards de nommage du projet pour assurer la cohérence entre tous les développeurs.
        - **Playbook pour IA :** Un guide/playbook établissant la logique de code, l'architecture et les règles spécifiques du projet. Ce document est destiné à être fourni comme contexte aux autres IA de codage utilisées par l'utilisateur, garantissant ainsi que toutes les futures générations de code respecteront exactement les mêmes standards.

## Pièges à éviter

- Ne pas traduire la syntaxe du langage lui-même (ne pas écrire `pour (let index...` au lieu de `for (let index...`).
- Ne pas générer un gros bloc de code brut sans l'accompagner d'une explication didactique autour.