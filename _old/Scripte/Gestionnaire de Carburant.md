### Comment le jeu calcule le temps restant ?

Dans _Space Engineers_, la physique est bien faite : **1 kg de lingot d'uranium produit exactement 1 MWh d'énergie**. Donc, si ton vaisseau consomme 10 MW (MégaWatts) à l'instant T, il brûle 10 kg d'uranium par heure. Si tu as 5 kg en stock, il te reste 30 minutes. Le script va faire ce calcul en temps réel !

### 1. Préparation en jeu

1. **L'Alarme :** Construis un bloc sonore (Sound Block) ou un gyrophare. Groupe-le sous le nom **`ALARMES_URANIUM`**.
    
2. **Le Bouton d'Acquittement :** Construis un Panneau de Boutons (Button Panel).
    
    - Fais glisser le Bloc Programmable contenant ce script dans la barre d'action du bouton.
        
    - Choisis l'action **"Exécuter avec l'argument..." (Run with argument)**.
        
    - Tape exactement **`ACK`** (pour _Acknowledge_ / Acquitter) et valide.
        

### 2. Le Script "Gestionnaire de Carburant"

Voici le code à placer dans un nouveau Bloc Programmable. J'ai bien commenté la logique de l'acquittement pour que tu puisses la réutiliser pour d'autres scripts !

C#

```
// ==============================================================================
// SCRIPT DE SURVEILLANCE CARBURANT (URANIUM) AVEC GYROPHARES COULEURS
// ==============================================================================

bool alarmeAcquittee = false;
bool sirènesAllumées = false;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    // 1. GESTION DE L'ACQUITTEMENT
    if (argument.ToUpper() == "ACK")
    {
        alarmeAcquittee = true; 
        ReglerAlarmes(false, new Color(0, 0, 0)); // Éteint les gyrophares
        Echo("Alarme acquittée.");
    }

    // 2. CALCUL DU CARBURANT
    List<IMyReactor> reacteurs = new List<IMyReactor>();
    GridTerminalSystem.GetBlocksOfType<IMyReactor>(reacteurs);

    float totalUraniumKg = 0;
    float totalConsommationMW = 0;
    MyItemType typeUranium = MyItemType.MakeIngot("Uranium");

    foreach (var reacteur in reacteurs)
    {
        totalConsommationMW += reacteur.CurrentOutput;
        totalUraniumKg += (float)reacteur.GetInventory().GetItemAmount(typeUranium);
    }

    float heuresRestantes = float.MaxValue;
    if (totalConsommationMW > 0)
    {
        heuresRestantes = totalUraniumKg / totalConsommationMW;
    }

    // 3. LOGIQUE DE DÉCLENCHEMENT (Couleur Verte demandée : RGB = 0, 255, 0)
    if (heuresRestantes <= 1.0f)
    {
        if (!alarmeAcquittee && !sirènesAllumées)
        {
            // On allume en VERT (R=0, G=255, B=0)
            ReglerAlarmes(true, new Color(0, 255, 0)); 
            sirènesAllumées = true;
        }
    }
    else 
    {
        alarmeAcquittee = false;
        if (sirènesAllumées)
        {
            ReglerAlarmes(false, new Color(0, 0, 0)); 
            sirènesAllumées = false;
        }
    }

    // 4. AFFICHAGE
    string rapport = "=== CONTRÔLE RÉACTEURS ===\n\n";
    rapport += $"Consommation : {totalConsommationMW:0.00} MW\n";
    rapport += $"Uranium : {totalUraniumKg:0.0} kg\n";
    rapport += $"Autonomie : {(heuresRestantes == float.MaxValue ? "INFINIE" : heuresRestantes.ToString("0.0") + " H")}\n";
    
    if (heuresRestantes <= 1.0f)
    {
        rapport += "\n/!\\ ALERTE CARBURANT /!\\\n";
        if (alarmeAcquittee) rapport += "(Acquittée)\n";
    }
    Echo(rapport);
}

// ------------------------------------------------------------------------------
// FONCTION UTILITAIRE : Gestion des gyrophares multicouleurs
// ------------------------------------------------------------------------------
void ReglerAlarmes(bool allumer, Color couleurAlerte)
{
    List<IMyLightingBlock> gyrophares = new List<IMyLightingBlock>();
    
    // On cherche toutes les lumières (dont les gyrophares) avec le tag spécifique
    GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(gyrophares, b => b.CustomName.Contains("[giro-alarme]"));
    
    foreach (var gyro in gyrophares)
    {
        if (allumer)
        {
            gyro.Color = couleurAlerte; // Change la couleur
            gyro.Enabled = true;        // Allume le gyrophare
        }
        else
        {
            gyro.Enabled = false;       // Éteint le gyrophare
        }
    }
}
```

_Note technique pour le futur :_ Le jour où tu auras plusieurs scripts d'alarmes (un pour les dégâts, un pour l'uranium) qui ciblent les mêmes `[giro-alarme]`, ils risquent de se battre. L'un dira "Allume-toi en vert" et l'autre dira "Éteins-toi, il n'y a pas de dégâts".