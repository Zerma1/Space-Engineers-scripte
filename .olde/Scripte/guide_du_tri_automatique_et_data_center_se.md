# Guide Complet : Tri Automatique & Data Center dans Space Engineers

Ce guide explique comment mettre en place un système de tri d'inventaire intelligent et centralisé. Il utilise le concept d'un "Data Center" : un bloc central stocke les règles (le dictionnaire), et les scripts de tri viennent lire ces règles à distance.

## 1. Mise en place du Data Center (Le Serveur Dictionnaire)

L'objectif est de ne pas coder les traductions en dur dans le script, pour pouvoir les modifier sans jamais toucher au code.

1. Construisez un bloc sur votre vaisseau (un Bloc Programmable, un écran LCD, ou même un conteneur).
2. Nommez ce bloc exactement **`SERVEUR_DICTIONNAIRE`** dans le panneau de contrôle.
3. Allez dans les **Données Personnalisées (Custom Data)** de ce bloc.
4. Collez la configuration suivante (format INI) :

```ini
[Dictionnaire]
; --- Pierres et ressources de base ---
stone=Stone
pierre=Stone
gravier=Stone
gravel=Stone
ice=Ice
glace=Ice

; --- Fer ---
fe=Iron
fer=Iron
iron=Iron

; --- Nickel ---
ni=Nickel
nickel=Nickel

; --- Silicium ---
si=Silicon
silicium=Silicon
silicon=Silicon

; --- Cobalt ---
co=Cobalt
cobalt=Cobalt

; --- Magnésium ---
mg=Magnesium
magnesium=Magnesium
magnésium=Magnesium

; --- Argent ---
ag=Silver
argent=Silver
silver=Silver

; --- Or ---
au=Gold
or=Gold
gold=Gold

; --- Platine ---
pt=Platinum
platine=Platinum
platinum=Platinum

; --- Uranium ---
u=Uranium
uranium=Uranium

; --- Déchets / Ferraille ---
scrap=Scrap
scrap metal=Scrap
ferraille=Scrap

; --- Munitions ---
ammo=Ammo
munitions=Ammo
munition=Ammo

; --- Composants de base ---
plaque d'acier=SteelPlate
steel plate=SteelPlate
steelplate=SteelPlate
plaque=SteelPlate

composant de construction=Construction
construction=Construction

plaque interieure=InteriorPlate
plaque intérieure=InteriorPlate
interior plate=InteriorPlate
interiorplate=InteriorPlate

moteur=Motor
motor=Motor

ordinateur=Computer
computer=Computer
pc=Computer

petit tube=SmallTube
small tube=SmallTube
smalltube=SmallTube

grand tube=LargeTube
large tube=LargeTube
largetube=LargeTube

affichage=Display
display=Display
ecran=Display
écran=Display

poutre=Girder
girder=Girder

grille metallique=MetalGrid
grille métallique=MetalGrid
metal grid=MetalGrid
metalgrid=MetalGrid
grille=MetalGrid

; --- Composants avancés ---
composant de propulseur=Thrust
thrust=Thrust
propulseur=Thrust

composant de reacteur=Reactor
composant de réacteur=Reactor
reactor=Reactor
reacteur=Reactor
réacteur=Reactor

cellule solaire=SolarCell
solar cell=SolarCell
solarcell=SolarCell

cellule d'energie=PowerCell
cellule d'énergie=PowerCell
power cell=PowerCell
powercell=PowerCell
batterie=PowerCell

superconducteur=Superconductor
superconductor=Superconductor

composant medical=Medical
composant médical=Medical
medical=Medical

composant de communication=RadioCommunication
radio=RadioCommunication
communication=RadioCommunication

composant de detecteur=Detector
composant de détecteur=Detector
detector=Detector
detecteur=Detector
détecteur=Detector

composant de gravite=GravityGenerator
composant de gravité=GravityGenerator
gravity=GravityGenerator
gravité=GravityGenerator
gravite=GravityGenerator
```

## 2. Le Script de Tri (À mettre dans un Bloc Programmable)

Ce script va se connecter au `SERVEUR_DICTIONNAIRE`, lire les règles, puis scanner tous les conteneurs qui utilisent votre format de nommage (ex: `{ore}[*-Ice]0`) pour ranger les objets.

Voici le code complet, abondamment commenté pour que les débutants puissent en comprendre la logique.

```csharp
// ==============================================================================
// SCRIPT DE TRI D'INVENTAIRE (CLIENT DU DATA CENTER)
// ==============================================================================

// Dictionnaire qui va stocker nos traductions en mémoire.
// "StringComparer.OrdinalIgnoreCase" permet d'ignorer les majuscules/minuscules.
Dictionary<string, string> DicoRessources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

// On déclare notre liste de conteneurs cibles qui sera mise à jour en continu
List<ConteneurCible> ListeConteneurs = new List<ConteneurCible>();

// Classe (un "moule" ou "modèle") pour mémoriser la configuration de chaque coffre
class ConteneurCible
{
    public IMyCargoContainer Bloc;
    public string Categorie; // ex: "ore"
    public int Priorite;     // 0 = prioritaire, 1 = secondaire...
    public bool AccepteTout; // Si vrai, on est en mode "*-"
    public List<string> Inclusions = new List<string>(); // Ce qu'on accepte
    public List<string> Exclusions = new List<string>(); // Ce qu'on refuse
}

// ==============================================================================
// FONCTION PROGRAM : Exécutée UNE SEULE FOIS au moment de la compilation (Check Code)
// ==============================================================================
public Program()
{
    // 1. On cherche le bloc qui sert de Data Center sur le réseau du vaisseau
    IMyTerminalBlock blocServeur = GridTerminalSystem.GetBlockWithName("SERVEUR_DICTIONNAIRE");

    if (blocServeur == null)
    {
        // "Echo" affiche un message dans la console du Bloc Programmable
        Echo("ERREUR : Impossible de trouver le bloc nommé 'SERVEUR_DICTIONNAIRE'.");
        return; // On arrête tout si on ne le trouve pas
    }

    // 2. On lit le texte (Custom Data) du serveur
    MyIni lecteurIni = new MyIni();
    MyIniParseResult resultat;
    
    // Si le texte n'est pas au bon format INI, on affiche une erreur
    if (!lecteurIni.TryParse(blocServeur.CustomData, out resultat))
    {
        Echo($"Erreur de format dans les données du serveur: {resultat.ToString()}");
        return;
    }

    // 3. On extrait toutes les traductions pour remplir notre Dictionnaire C#
    List<MyIniKey> cles = new List<MyIniKey>();
    lecteurIni.GetKeys("Dictionnaire", cles);

    foreach (var cle in cles)
    {
        string valeur = lecteurIni.Get("Dictionnaire", cle.Name).ToString();
        DicoRessources[cle.Name] = valeur;
    }

    Echo($"Succès : {cles.Count} traductions chargées depuis le Data Center.");
    
    // 4. On dit au script de tourner tout seul en boucle (tous les 100 tics)
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

// ==============================================================================
// FONCTION MAIN : Exécutée en boucle (tous les 100 tics)
// ==============================================================================
public void Main(string argument, UpdateType updateSource)
{
    // On efface la mémoire des coffres au début de chaque cycle,
    // pour que si le joueur renomme un coffre, ce soit pris en compte immédiatement.
    ListeConteneurs.Clear(); 
    
    // On récupère tous les blocs du vaisseau
    List<IMyTerminalBlock> tousLesBlocs = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(tousLesBlocs);
    
    // ÉTAPE 1 : Trouver les coffres trieurs et lire leurs règles
    foreach (var bloc in tousLesBlocs)
    {
        // Si le bloc est un conteneur ET que son nom contient { et }
        if (bloc is IMyCargoContainer && bloc.CustomName.Contains("{") && bloc.CustomName.Contains("}"))
        {
            // On envoie le bloc à notre fonction d'analyse
            AnalyserFormat((IMyCargoContainer)bloc, bloc.CustomName);
        }
    }
    
    // ÉTAPE 2 : Déplacer les objets (Trier)
    foreach (var bloc in tousLesBlocs)
    {
        // On scanne l'inventaire de chaque bloc pour voir s'il y a des choses à ranger
        TrierInventaire(bloc);
    }
}

// ==============================================================================
// SOUS-FONCTIONS (Les outils utilisés par le script)
// ==============================================================================

// Outil 1 : Convertir le mot du joueur en nom officiel grâce au Data Center
string NormaliserNom(string entree)
{
    entree = entree.Trim(); // Enlève les espaces inutiles autour du mot
    if (DicoRessources.ContainsKey(entree)) return DicoRessources[entree];
    return entree;
}

// Outil 2 : Lire le nom du coffre (ex: {ore}[Iron; Stone]0)
void AnalyserFormat(IMyCargoContainer conteneur, string nom)
{
    // On cherche la position des caractères clés
    int debutCat = nom.IndexOf('{');
    int finCat = nom.IndexOf('}');
    int debutItems = nom.IndexOf('[');
    int finItems = nom.IndexOf(']');
    
    // Si tous les crochets sont bien là et dans le bon ordre
    if (debutCat < finCat && debutItems < finItems && finCat < debutItems) 
    {
        ConteneurCible cible = new ConteneurCible();
        cible.Bloc = conteneur;
        
        // On extrait le mot entre {} (ex: "ore")
        cible.Categorie = nom.Substring(debutCat + 1, finCat - debutCat - 1).ToLower();
        
        // On extrait ce qu'il y a entre []
        string contenuBrut = nom.Substring(debutItems + 1, finItems - debutItems - 1).Trim();
        
        // On extrait le chiffre à la fin pour la priorité
        string strPriorite = nom.Substring(finItems + 1).Trim();
        cible.Priorite = 99; // Par défaut, priorité très basse
        int.TryParse(strPriorite, out cible.Priorite); // Convertit le texte en nombre

        // Vérifie si on a le symbole d'exclusion "*-"
        if (contenuBrut.StartsWith("*-"))
        {
            cible.AccepteTout = true;
            contenuBrut = contenuBrut.Substring(2); // On coupe le "*-"
        }
        else if (contenuBrut == "*") // Accepte absolument tout
        {
            cible.AccepteTout = true;
            contenuBrut = "";
        }

        // On découpe les minerais séparés par des points-virgules
        if (!string.IsNullOrWhiteSpace(contenuBrut))
        {
            string[] elements = contenuBrut.Split(';');
            foreach(string element in elements)
            {
                if (cible.AccepteTout) 
                    cible.Exclusions.Add(NormaliserNom(element));
                else 
                    cible.Inclusions.Add(NormaliserNom(element));
            }
        }
        
        // On sauvegarde le coffre configuré dans notre liste
        ListeConteneurs.Add(cible);
    }
}

// Outil 3 : Trouver la meilleure destination pour un item
ConteneurCible TrouverMeilleurConteneur(string categorieObjet, string nomObjet, IMyTerminalBlock blocSource)
{
    ConteneurCible meilleur = null;
    bool sourceEstUnBonCoffre = false;
    int prioriteDeLaSource = 999;

    foreach (var dest in ListeConteneurs)
    {
        if (dest.Categorie != categorieObjet) continue;

        bool estAccepte = false;
        if (dest.AccepteTout) {
            // S'il n'est pas dans la liste des exclusions, il est accepté
            if (!dest.Exclusions.Contains(nomObjet)) estAccepte = true;
        } else {
            // S'il est dans la liste des inclusions, il est accepté
            if (dest.Inclusions.Contains(nomObjet)) estAccepte = true;
        }

        if (estAccepte)
        {
            // Vérifie si l'objet est DÉJÀ dans un coffre valide
            if (dest.Bloc == blocSource) 
            {
                sourceEstUnBonCoffre = true;
                prioriteDeLaSource = dest.Priorite;
            }

            // Cherche le coffre avec le plus petit nombre (la meilleure priorité)
            if (meilleur == null || dest.Priorite < meilleur.Priorite)
            {
                meilleur = dest;
            }
        }
    }

    // Sécurité : On empêche le script de déplacer un objet en boucle
    if (sourceEstUnBonCoffre && meilleur != null && prioriteDeLaSource <= meilleur.Priorite) {
        return null; 
    }

    return meilleur;
}

// Outil 4 : Gérer l'inventaire physique des blocs
void TrierInventaire(IMyTerminalBlock blocSource)
{
    // Si le bloc ne peut pas contenir d'objets, on ignore
    if (!blocSource.HasInventory) return;

    for (int i = 0; i < blocSource.InventoryCount; i++)
    {
        IMyInventory inventaireSource = blocSource.GetInventory(i);
        List<MyInventoryItem> items = new List<MyInventoryItem>();
        inventaireSource.GetItems(items);

        // On compte à l'envers pour ne pas casser l'index quand un objet disparait
        for (int j = items.Count - 1; j >= 0; j--)
        {
            MyInventoryItem item = items[j];

            // Identifie la catégorie (ore, ingot)
            string categorieBrute = item.Type.TypeId.ToString();
            string categorieObjet = categorieBrute.Replace("MyObjectBuilder_", "").ToLower();
            
            // Identifie l'objet précis (Iron, Stone)
            string nomObjet = item.Type.SubtypeId.ToString();

            // On cherche où il doit aller
            ConteneurCible destination = TrouverMeilleurConteneur(categorieObjet, nomObjet, blocSource);

            // Si on a trouvé un coffre cible différent de celui où l'objet est actuellement
            if (destination != null && destination.Bloc != blocSource)
            {
                IMyInventory inventaireDest = destination.Bloc.GetInventory(0);
                
                // On ordonne au jeu de déplacer l'objet
                inventaireSource.TransferItemTo(inventaireDest, item, null);
            }
        }
    }
}
```

## 3. Guide de modification pour les débutants

Voici quelques astuces si vous souhaitez modifier ce code :

*   **Changer la vitesse de tri :** Dans la fonction `Program()`, modifiez `UpdateFrequency.Update100`. Remplacez `Update100` par `Update10` pour un tri très rapide, mais attention, cela demandera plus de ressources à votre ordinateur (ou au serveur).
*   **Ajouter des blocs :** Le script actuel lit les "Cargo Containers" (`IMyCargoContainer`). Si vous voulez qu'il analyse d'autres blocs (comme des connecteurs) pour devenir des coffres de destination, il faudra modifier la ligne `if (bloc is IMyCargoContainer...` dans la fonction `Main()`.
*   **Le Dictionnaire :** Grâce au système de Data Center, vous n'avez pas besoin de modifier le script C# pour ajouter de nouveaux minerais ou lingots (issus de mods, par exemple). Ajoutez simplement des lignes dans les Custom Data du bloc `SERVEUR_DICTIONNAIRE`.