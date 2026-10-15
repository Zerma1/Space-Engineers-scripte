# 🛠️ GUIDE DE SURVIE DE L'INGÉNIEUR : Le Scanner de Brèches

Bienvenue dans ce guide d'introduction à la programmation sur Space Engineers ! 
Aujourd'hui, nous allons apprendre à utiliser un **Bloc Programmable** et un **Projecteur** pour créer un scanner de diagnostic. L'objectif est de savoir instantanément si votre vaisseau a perdu des morceaux (blocs complètement détruits) suite à une collision ou un combat.

---

## 1. Pourquoi utiliser un Projecteur ?

Un script ne peut "voir" que ce qui existe. Si un bloc d'armure est complètement pulvérisé par un missile, il disparaît du jeu. Le script ne peut donc pas le trouver pour vous dire qu'il est cassé.

**La solution :** Le Projecteur. 
En chargeant le plan (Blueprint) de votre vaisseau intact dans un projecteur superposé à votre vaisseau réel, le jeu calcule automatiquement la différence entre "ce qui devrait être là" et "ce qui est vraiment là". Notre script va simplement lire ce calcul.

### Prérequis en jeu :
1. Avoir un Projecteur nommé exactement `PROJECTEUR_REPARATION`.
2. Y charger le Blueprint du vaisseau actuel.
3. Aligner la projection parfaitement sur le vaisseau.
4. Cocher "Garder la projection" dans les paramètres du projecteur.
5. (Optionnel) Avoir un écran LCD nommé `LCD_SCANNER`.

---

## 2. Le Script Décortiqué

Voici le code complet. Lisez attentivement les commentaires (les lignes qui commencent par `//`), ils expliquent ce que fait chaque instruction.

```csharp
// ==============================================================================
// SCRIPT DE DÉTECTION DES BLOCS DÉTRUITS
// Niveau : Débutant
// ==============================================================================

// La méthode Program() est le point de départ. Elle s'exécute une seule fois
// au moment où vous cliquez sur "Vérifier le code" (Recompiler).
public Program()
{
    // Runtime.UpdateFrequency définit à quelle vitesse le script boucle.
    // Ici on met "None" (Aucune) car scanner le projecteur en permanence 
    // ferait lagger le serveur. On veut que le joueur lance le script manuellement.
    Runtime.UpdateFrequency = UpdateFrequency.None;
}

// La méthode Main() est le cœur du programme. 
// Elle s'exécute à chaque fois qu'on appuie sur "Exécuter" dans le terminal.
public void Main(string argument, UpdateType updateSource)
{
    // ---------------------------------------------------------
    // ÉTAPE 1 : TROUVER LE PROJECTEUR SUR LE VAISSEAU
    // ---------------------------------------------------------
    
    // On demande au système (GridTerminalSystem) de chercher un bloc par son nom.
    // Le "as IMyProjector" est une conversion (cast). On dit au jeu : 
    // "Je sais que ce bloc est un projecteur, donne-moi accès à ses options de projecteur".
    IMyProjector projecteur = GridTerminalSystem.GetBlockWithName("PROJECTEUR_REPARATION") as IMyProjector;

    // SECURITÉ : Si on s'est trompé dans le nom, la variable "projecteur" sera vide (null).
    // On arrête tout (return) pour éviter que le jeu ne plante.
    if (projecteur == null)
    {
        Echo("ERREUR : Impossible de trouver le bloc 'PROJECTEUR_REPARATION'.");
        return; 
    }

    // SECURITÉ : On vérifie si le projecteur est allumé et contient bien un plan.
    if (!projecteur.IsProjecting)
    {
        Echo("ERREUR : Le projecteur est éteint ou ne contient aucun plan (Blueprint).");
        return;
    }

    // ---------------------------------------------------------
    // ÉTAPE 2 : RÉCOLTER LES INFORMATIONS
    // ---------------------------------------------------------
    
    // L'API (le dictionnaire de commandes du jeu) nous donne des variables toutes prêtes :
    int totalBlocsPlan = projecteur.TotalBlocks;       // Le nombre total de blocs sur le plan idéal
    int blocsManquants = projecteur.RemainingBlocks;   // Le nombre de blocs qui ont disparu de la réalité
    int blocsReconstructibles = projecteur.BuildableBlocks; // Les blocs manquants qui touchent un bloc existant

    // ---------------------------------------------------------
    // ÉTAPE 3 : PRÉPARER LE TEXTE DU RAPPORT
    // ---------------------------------------------------------
    
    // On crée une variable de texte (string) avec le titre.
    // Le symbole "\n" sert à faire un retour à la ligne (comme la touche Entrée).
    string texteRapport = "=== DIAGNOSTIC STRUCTUREL ===\n\n";

    // "if" veut dire "Si". On vérifie une condition.
    if (blocsManquants == 0)
    {
        // += veut dire "Ajoute ce texte à la suite du texte précédent"
        texteRapport += "ÉTAT : INTÈGRE\n";
        texteRapport += "Aucun bloc manquant détecté. Le vaisseau est à 100%.";
    }
    // "else" veut dire "Sinon". S'il manque des blocs, on fait le code ci-dessous :
    else
    {
        texteRapport += "ÉTAT : BRÈCHES DÉTECTÉES\n";
        texteRapport += "---------------------------\n";
        
        // Le signe $ devant les guillemets permet d'insérer des variables 
        // directement dans le texte en utilisant des accolades { }.
        texteRapport += $"Blocs volatilisés : {blocsManquants} sur {totalBlocsPlan}\n";
        
        // Explication des blocs constructibles : 
        // Dans le jeu, on ne peut souder un bloc dans le vide. Il doit être accroché au vaisseau.
        if (blocsReconstructibles > 0)
        {
            texteRapport += $"\n{blocsReconstructibles} bloc(s) prêt(s) à être soudé(s).";
        }
        else
        {
            texteRapport += "\nATTENTION : Aucun bloc manquant n'est rattaché à la coque.";
        }
    }

    // ---------------------------------------------------------
    // ÉTAPE 4 : AFFICHER LE RÉSULTAT
    // ---------------------------------------------------------
    
    // Echo() écrit le texte dans la petite fenêtre en bas à droite du Bloc Programmable
    Echo(texteRapport);

    // On cherche un écran LCD nommé "LCD_SCANNER" pour afficher le résultat en grand.
    IMyTextPanel ecranScanner = GridTerminalSystem.GetBlockWithName("LCD_SCANNER") as IMyTextPanel;
    
    // Si l'écran existe (il n'est pas null), on écrit dessus.
    if (ecranScanner != null)
    {
        // On s'assure que l'écran est bien réglé pour afficher du texte
        ecranScanner.ContentType = SpaceEngineers.Game.ModAPI.Ingame.ContentType.TEXT_AND_IMAGE;
        // On remplace le texte de l'écran par notre rapport
        ecranScanner.WriteText(texteRapport);
    }
}