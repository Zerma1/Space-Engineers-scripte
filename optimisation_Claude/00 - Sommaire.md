---
tags: [space-engineers, sommaire, optimise]
---
# Sommaire : scripts optimisés Space Engineers

> [!summary] À quoi sert cette note ?
> Point d'entrée de tous les scripts optimisés pour le **Bloc Programmable** de Space Engineers. Chaque note explique l'installation, la configuration, les commandes et contient le code complet, prêt à copier-coller.

Commencez par lire [[00 - Conventions et Playbook IA]] : noms de blocs réservés, rôles des écrans, commandes communes. L'historique du projet est dans [[Jalons du projet]].

---

## Data Center (stocks)

| Note | Fichier | Rôle |
|---|---|---|
| [[Serveur Dictionnaire]] | `SERVEUR_DICTIONNAIRE.ini` | Traductions partagées (catégories, objets, noms affichés) dans le Custom Data d'un bloc. |
| [[Tri Automatique des Inventaires]] | `TriInventaire.cs` | Range les objets dans les coffres selon leur nom `{categorie}[liste]priorite`. |
| [[Affichage Data Center]] | `AffichageDataCenter.cs` | Stocks, gaz et énergie sur les écrans (`Role=Stocks`). |

## Sécurité & alarmes

| Note | Fichier | Rôle |
|---|---|---|
| [[Bus d'Alarmes]] | `SERVEUR_ALARMES.ini` | Tableau commun (optionnel) qui évite que deux scripts se disputent les gyrophares. |
| [[Controle des Avaries]] | `ControleAvaries.cs` | Intégrité des blocs, rapport `Role=Avaries`, groupe `ALARMES_CRITIQUES`. |
| [[Surveillance Carburant Uranium]] | `GestionnaireCarburant.cs` | Énergie : H2, uranium, glace, batteries, et alarme `Energie` qui monte en criticité quand plusieurs manquent (`Role=Carburant`). |
| [[Centrale d'Alarmes]] | `CentraleAlarmes.cs` | Pilote unique des gyrophares (groupe `ALARMES`, couleur du niveau de criticité le plus grave) et écrans `[alarme-line]` / `[alarme-quant]`. |
| [[Diagnostic Structurel par Projecteur]] | `DiagnosticStructurel.cs` | Blocs détruits détectés par comparaison avec un projecteur (`Role=Structure`). |

## Portes & sas

| Note | Fichier | Rôle |
|---|---|---|
| [[Sas Automatique (Airlock)]] | `GestionnaireSas.cs` | Sas de décompression automatiques `[SAS-Nom-int/ext/vent/lcd]`. |
| [[Fermeture Automatique des Portes]] | `FermeturePortes.cs` | Referme les portes restées ouvertes (ignore sas, hangars, `[NoAuto]`). |

---

## Ordre d'installation conseillé

1. Créer le bloc `SERVEUR_DICTIONNAIRE` (et `SERVEUR_ALARMES` si vous utilisez la [[Centrale d'Alarmes]] ou plusieurs scripts d'alarme).
2. Installer le [[Tri Automatique des Inventaires]], puis l'[[Affichage Data Center]].
3. Ajouter les scripts de sécurité et de portes selon vos besoins : chacun tourne dans **son propre** Bloc Programmable.

## Notes d'origine

Les scripts originaux restent inchangés dans le dossier `_old/Scripte` : [[_old/Scripte/guide_du_tri_automatique_et_data_center_se|guide_du_tri_automatique_et_data_center_se]], [[_old/Scripte/Gestion de l'Affichage et Traductions|Gestion de l'Affichage et Traductions]], [[_old/Scripte/Controle_Avaries_SE|Controle_Avaries_SE]], [[_old/Scripte/Gestionnaire de Carburant|Gestionnaire de Carburant]], [[_old/Scripte/Le Scanner de Brèches|Le Scanner de Brèches]], [[_old/Scripte/Script Space Engineers Diagnostic Structurel|Script Space Engineers Diagnostic Structurel]], [[_old/Scripte/Gestionnaire de Sas (Airlock) Intelligent|Gestionnaire de Sas (Airlock) Intelligent]], [[_old/Scripte/fermeture des porte|fermeture des porte]].
