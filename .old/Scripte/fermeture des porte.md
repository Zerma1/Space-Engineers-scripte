### 1. Préparation en jeu

1. **Placer un Bloc Programmable** sur votre vaisseau ou votre base.
2. N'hésitez pas à vérifier le nom de vos portes : le script ignorera automatiquement toutes les portes contenant `[SAS-` dans leur nom afin de ne pas interférer avec votre gestionnaire de sas intelligent.

---

### 2. Le Script C# Complet

Copiez-collez ce code dans le **Bloc Programmable**, puis cliquez sur **Vérifier le code** (ou _Recompiler_) et enregistrez.

```
// ==============================================================================
// SCRIPT DE FERMETURE AUTOMATIQUE DES PORTES (2 SECONDES)
// ==============================================================================

// Dictionnaire pour enregistrer l'heure d'ouverture de chaque porte
Dictionary<IMyDoor, DateTime> chronoPortes = new Dictionary<IMyDoor, DateTime>();

public Program()
{
    // Update10 exécute le script environ toutes les 0.16 seconde.
    // Cela permet un suivi précis du temps sans charger le serveur.
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

public void Main(string argument, UpdateType updateSource)
{
    List<IMyDoor> portes = new List<IMyDoor>();
    GridTerminalSystem.GetBlocksOfType<IMyDoor>(portes);

    DateTime maintenant = DateTime.Now;

    foreach (var porte in portes)
    {
        // 1. On filtre les portes du sas pour ne pas perturber le script de décompression
        if (porte.CustomName.Contains("[SAS-")) continue;

        // 2. Si la porte commence à s'ouvrir ou est ouverte
        if (porte.Status == DoorStatus.Opening || porte.Status == DoorStatus.Open)
        {
            if (!chronoPortes.ContainsKey(porte))
            {
                // On mémorise le moment précis où la porte s'est ouverte
                chronoPortes[porte] = maintenant;
            }
            else
            {
                // On vérifie si 2.0 secondes se sont écoulées
                if ((maintenant - chronoPortes[porte]).TotalSeconds >= 2.0)
                {
                    porte.CloseDoor();
                    chronoPortes.Remove(porte); // On nettoie la mémoire pour cette porte
                }
            }
        }
        // 3. Si la porte est refermée, on réinitialise son suivi
        else if (porte.Status == DoorStatus.Closed)
        {
            if (chronoPortes.ContainsKey(porte))
            {
                chronoPortes.Remove(porte);
            }
        }
    }
}
```

---

### Comment ça fonctionne ?

- **Détection automatique :** Le script scrute l'ensemble des portes connectées à votre grille. Vous n'avez pas besoin de les nommer d'une façon spécifique (sauf si ce sont des portes de sas).
- **Protection des sas :** Si une porte contient la balise `[SAS-` (utilisée par le script de sas de vos guides), le script la laisse tranquille pour éviter tout conflit.
- **Temporisation précise :** Dès qu'un joueur ouvre une porte, le script enregistre l'heure. Exactement **2 secondes** plus tard, il ordonne la fermeture.