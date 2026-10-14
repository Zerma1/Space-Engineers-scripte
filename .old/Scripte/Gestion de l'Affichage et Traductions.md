# Guide du Data Center : Gestion de l'Affichage et Traductions dans Space Engineers

Ce document détaille la mise en place du module d'affichage de votre Data Center. L'objectif est double : récupérer les données de vos coffres et réservoirs, puis les afficher sur vos écrans LCD en remplaçant les noms techniques du jeu par des termes clairs dans votre langue (ex: "Iron" devient "Fer").

Ce guide est pensé pour les débutants en C# : chaque étape et chaque ligne de code clé sont expliquées.

---

## 1. Le Serveur Dictionnaire : Centraliser les traductions

Pour éviter d'avoir à modifier le code C# à chaque fois que vous ajoutez un nouveau minerai (notamment avec des mods), nous utilisons les **Données Personnalisées (Custom Data)** d'un bloc central.

1. Choisissez un bloc dans votre base (ex: un écran, un Bloc Programmable ou un conteneur).
2. Nommez-le exactement `SERVEUR_DICTIONNAIRE`.
3. Ouvrez ses **Données Personnalisées** et ajoutez la section `[Affichage]` suivante :

```ini
[Affichage]
; Format -> NomDuJeu=VotreTraduction
Iron=Fer
Nickel=Nickel
Silicon=Silicium
Gold=Or
Stone=Pierre
Ice=Glace
```
_Note pour les débutants : L'ordinateur va lire ce texte. S'il trouve le mot "Iron" dans un coffre, il regardera cette liste et le remplacera automatiquement par "Fer" sur vos écrans._

## 2. Configurer vos Écrans LCD

Chaque écran LCD de votre vaisseau doit savoir ce qu'il est censé afficher (les minerais pour le mineur, les munitions pour l'artilleur, etc.). Utilisez les **Données Personnalisées** de chaque écran pour lui donner ses instructions.

**Exemple de configuration pour un écran de Chef Mineur (Affiche une page fixe) :**

Ini, TOML

```ini
[Ecran]
Mode=Fixe
Page=1
TotalPages=1
Categories=ore, ingot
Exclusions=Stone
AfficherGaz=false
AfficherEnergie=false
```

## 3. Le Script d'Affichage (À copier dans le Bloc Programmable)

Voici le code C# complet à placer dans votre Bloc Programmable dédié aux écrans. Il est abondamment commenté pour vous permettre de comprendre sa logique et de le modifier à votre guise.

C#

```C#
// ==============================================================================
// SCRIPT D'AFFICHAGE DU DATA CENTER (RESSOURCES & TRADUCTIONS)
// ==============================================================================

// Outil fourni par le jeu pour lire les textes au format INI (les Custom Data)
MyIni lecteurIni = new MyIni();

// Ce "Dictionnaire" est une mémoire virtuelle. On lui donne un mot anglais, il nous rend le mot français.
Dictionary<string, string> DicoAffichage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

// Compteur pour gérer le temps qui passe (utile pour tourner les pages des écrans rotatifs)
int compteurTicksGlobal = 0;
const int TICKS_PAR_PAGE = 5; 

// ------------------------------------------------------------------------------
// DÉMARRAGE DU SCRIPT
// ------------------------------------------------------------------------------
public Program()
{
    // On demande au script de s'exécuter de lui-même tous les 100 "ticks" (environ 1.6 secondes)
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    
    // Au démarrage, on charge nos traductions
    InitialiserTraduction();
}

// Fonction qui va lire le bloc SERVEUR_DICTIONNAIRE pour apprendre les mots
void InitialiserTraduction()
{
    // On cherche le bloc sur le réseau du vaisseau
    IMyTerminalBlock serveur = GridTerminalSystem.GetBlockWithName("SERVEUR_DICTIONNAIRE");
    
    // Si on ne le trouve pas, on arrête la fonction ici
    if (serveur == null) return; 

    // On lit les Données Personnalisées du bloc
    if (lecteurIni.TryParse(serveur.CustomData))
    {
        List<MyIniKey> cles = new List<MyIniKey>();
        // On récupère toutes les lignes sous la catégorie [Affichage]
        lecteurIni.GetKeys("Affichage", cles);
        
        // Pour chaque ligne trouvée (ex: "Iron=Fer")...
        foreach (var cle in cles)
        {
            // On mémorise la traduction dans notre cerveau virtuel (DicoAffichage)
            string nomJoli = lecteurIni.Get("Affichage", cle.Name).ToString();
            DicoAffichage[cle.Name] = nomJoli;
        }
    }
}

// ------------------------------------------------------------------------------
// BOUCLE PRINCIPALE (S'exécute toutes les 1.6 secondes)
// ------------------------------------------------------------------------------
public void Main(string argument, UpdateType updateSource)
{
    // 1. RECHERCHE DES ÉCRANS
    // On cherche tous les écrans LCD du vaisseau qui possèdent une balise [Ecran] dans leurs données
    List<IMyTextPanel> tousLesEcrans = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(tousLesEcrans);

    // 2. INVENTAIRE GLOBAL
    // On appelle notre fonction qui compte absolument tout dans le vaisseau
    Dictionary<string, double> toutesRessources = CompterRessources();

    // 3. AFFICHAGE ET TRADUCTION POUR CHAQUE ÉCRAN
    foreach (var ecran in tousLesEcrans)
    {
        // On vérifie si l'écran possède bien la balise [Ecran]
        if (lecteurIni.TryParse(ecran.CustomData) && lecteurIni.ContainsSection("Ecran"))
        {
            // On lit la configuration voulue par le joueur (Catégories, Exclusions...)
            string catStr = lecteurIni.Get("Ecran", "Categories").ToString("");
            string exStr = lecteurIni.Get("Ecran", "Exclusions").ToString("");
            
            // On prépare une liste vide qui contiendra le texte final à afficher
            List<string> lignesAAfficher = new List<string>();

            // On parcourt tout ce qu'on a trouvé dans le vaisseau
            foreach (var ressource in toutesRessources)
            {
                // On sépare la catégorie du nom (ex: "ore:Iron" devient "ore" et "Iron")
                string[] parties = ressource.Key.Split(':');
                string categorie = parties[0].ToLower();
                string nomInterne = parties[1]; 

                // L'écran veut-il afficher cette ressource ?
                bool estInclus = catStr.ToLower().Contains(categorie);
                bool estExclu = exStr.ToLower().Contains(nomInterne.ToLower());

                if (estInclus && !estExclu)
                {
                    // --- APPLICATION DE LA TRADUCTION ---
                    string nomAAfficher = nomInterne; // Par défaut, on garde le nom anglais
                    
                    // Si notre dictionnaire connaît ce mot (ex: "Iron")...
                    if (DicoAffichage.ContainsKey(nomInterne))
                    {
                        // ...on le remplace par le mot français (ex: "Fer")
                        nomAAfficher = DicoAffichage[nomInterne];
                    }

                    // On assemble le texte final pour cette ligne (ex: "Fer : 15 000")
                    lignesAAfficher.Add($"{nomAAfficher} : {ressource.Value:N0}");
                }
            }

            // On envoie le texte final sur la vitre de l'écran LCD
            string texteFinal = "=== DATA CENTER ===\n" + string.Join("\n", lignesAAfficher);
            ecran.ContentType = SpaceEngineers.Game.ModAPI.Ingame.ContentType.TEXT_AND_IMAGE;
            ecran.WriteText(texteFinal);
        }
    }
}

// ------------------------------------------------------------------------------
// FONCTION DE COLLECTE DES RESSOURCES
// ------------------------------------------------------------------------------
Dictionary<string, double> CompterRessources()
{
    Dictionary<string, double> invGlobal = new Dictionary<string, double>();
    List<IMyTerminalBlock> blocs = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(blocs); // Trouve tous les blocs de la base

    foreach (var bloc in blocs)
    {
        if (!bloc.HasInventory) continue; // Si le bloc ne peut rien stocker, on passe au suivant
        
        for (int i = 0; i < bloc.InventoryCount; i++)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            bloc.GetInventory(i).GetItems(items); // Récupère le contenu de l'inventaire

            foreach (var item in items)
            {
                // Nettoie le nom de la catégorie pour qu'il soit lisible (enlève le "MyObjectBuilder_")
                string cat = item.Type.TypeId.ToString().Replace("MyObjectBuilder_", "");
                
                // Crée une clé unique combinant catégorie et nom (ex: "Ore:Iron")
                string cle = $"{cat}:{item.Type.SubtypeId}";
                
                // Additionne les quantités trouvées
                if (!invGlobal.ContainsKey(cle)) invGlobal[cle] = 0;
                invGlobal[cle] += (double)item.Amount;
            }
        }
    }
    return invGlobal; // Renvoie le total au script principal
}
```