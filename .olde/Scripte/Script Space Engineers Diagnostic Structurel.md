# Script Space Engineers : Diagnostic Structurel (Projecteur)

Ce script utilise un projecteur et un plan (Blueprint) sauvegardé pour détecter les blocs qui ont été totalement détruits (volatilisés) et qui ne peuvent plus être scannés physiquement.

## 🛠️ Prérequis d'installation en jeu

1. Sauvegarder le plan (Blueprint) du vaisseau lorsqu'il est intact (`Ctrl+B`).
2. Placer un **Projecteur** sur le vaisseau et le nommer exactement `PROJECTEUR_REPARATION`.
3. Charger le plan dans ce projecteur et l'aligner parfaitement avec les blocs réels.
4. **Cocher la case "Garder la projection" (Keep Projection)** dans le terminal du projecteur.
5. Placer ce code dans un Bloc Programmable et l'exécuter manuellement (via un bouton) après un combat.

## 💻 Code C# du Bloc Programmable

```csharp
// ==============================================================================
// SCRIPT DE DÉTECTION DES BLOCS DÉTRUITS (VIA PROJECTEUR)
// ==============================================================================

public Program()
{
    // Script manuel (pas de boucle automatique pour éviter le lag)
    Runtime.UpdateFrequency = UpdateFrequency.None;
}

public void Main(string argument, UpdateType updateSource)
{
    // On cherche le projecteur par son nom
    IMyProjector projecteur = GridTerminalSystem.GetBlockWithName("PROJECTEUR_REPARATION") as IMyProjector;

    if (projecteur == null)
    {
        Echo("ERREUR : Impossible de trouver le bloc 'PROJECTEUR_REPARATION'.");
        return;
    }

    if (!projecteur.IsProjecting)
    {
        Echo("ERREUR : Le projecteur est éteint ou ne contient aucun plan (Blueprint).");
        return;
    }

    // Récupération des données du projecteur
    int totalBlocsPlan = projecteur.TotalBlocks;
    int blocsManquants = projecteur.RemainingBlocks;
    int blocsReconstructibles = projecteur.BuildableBlocks;

    string texteRapport = "=== DIAGNOSTIC STRUCTUREL ===\n\n";

    if (blocsManquants == 0)
    {
        texteRapport += "ÉTAT : INTÈGRE\n";
        texteRapport += "Aucun bloc manquant détecté.\nLe vaisseau correspond à 100% au plan.";
    }
    else
    {
        texteRapport += "ÉTAT : BRÈCHES DÉTECTÉES\n";
        texteRapport += "---------------------------\n";
        texteRapport += $"Blocs volatilisés : {blocsManquants} sur {totalBlocsPlan}\n";
        
        if (blocsReconstructibles > 0)
        {
            texteRapport += $"\n{blocsReconstructibles} bloc(s) prêt(s) à être soudé(s) immédiatement.";
        }
        else
        {
            texteRapport += "\nATTENTION : Aucun bloc manquant n'est rattaché à la coque. Vérifiez l'alignement du projecteur.";
        }
    }

    // Affichage dans la console
    Echo(texteRapport);

    // Optionnel : Envoi vers un écran LCD
    IMyTextPanel ecranScanner = GridTerminalSystem.GetBlockWithName("LCD_SCANNER") as IMyTextPanel;
    if (ecranScanner != null)
    {
        ecranScanner.ContentType = SpaceEngineers.Game.ModAPI.Ingame.ContentType.TEXT_AND_IMAGE;
        ecranScanner.WriteText(texteRapport);
    }
}