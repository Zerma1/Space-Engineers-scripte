<#
.SYNOPSIS
    Relaie un prompt vers Claude ou Big Pickle et enregistre l'échange, mot pour mot, dans IA/Journal.

.DESCRIPTION
    L'orchestrateur (prompteur.ps1 + qwen2.5:3b) n'écrit jamais les réponses lui-même : ce script recopie le prompt envoyé
    et la réponse reçue, sans reformulation, puis affiche la réponse.
    La session de chaque modèle est retenue d'un appel à l'autre (fichier d'état dans %TEMP%) :
    le modèle garde le fil de la discussion et le cache de prompt réduit la consommation de tokens.

.EXAMPLE
    .\IA\Prompteur\relais.ps1 -Modele claude -FichierPrompt IA\Prompteur\prompt.md -Sujet "corrections sas"
    .\IA\Prompteur\relais.ps1 -Modele bigpickle -FichierPrompt IA\Prompteur\prompt.md -Mode edition
    .\IA\Prompteur\relais.ps1 -Modele claude -Nouvelle -Sujet "nouveau script" -FichierPrompt IA\Prompteur\prompt.md
    .\IA\Prompteur\relais.ps1 -Modele bigpickle -Agent reviewer -Nouvelle -Sujet "revue sas" -Prompt "Relis GestionnaireSas.cs"
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('claude', 'bigpickle')][string]$Modele,
    [string]$FichierPrompt,
    [string]$Prompt,
    # Niveau du modèle Claude : rapide (haiku, économique), normal (sonnet, par défaut), expert (opus, difficile)
    [ValidateSet('rapide', 'normal', 'expert')][string]$Niveau = 'normal',
    # lecture : le modèle répond sans rien modifier ; edition : il peut modifier les fichiers
    [ValidateSet('lecture', 'edition')][string]$Mode = 'lecture',
    # Démarre une nouvelle session (nouveau sujet) au lieu de reprendre la précédente
    [switch]$Nouvelle,
    [string]$Sujet = 'session',
    # Agent opencode à utiliser avec Big Pickle (reviewer, tester, explainer, coder… de .opencode/agents)
    [string]$Agent,
    # Journal imposé (l'orchestrateur écrit toute la session dans un seul fichier)
    [string]$Journal,
    # Qui envoie la consigne, pour le titre dans le journal
    [string]$Origine = 'Utilisateur',
    # Plafond de dépense en dollars pour cet appel Claude (0 = pas de plafond)
    [double]$BudgetUsd = 0
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$racine = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dossierJournal = Join-Path $racine 'IA\Journal'
# Une session par destinataire : Big Pickle et chacun de ses agents ont la leur
$suffixeAgent = if ($Agent) { "-$Agent" } else { '' }
$fichierEtat = Join-Path $env:TEMP "prompteur-space-engineers-$Modele$suffixeAgent.json"
$nomModele = if ($Modele -eq 'claude') { 'Claude' } elseif ($Agent) { "Big Pickle (agent $Agent)" } else { 'Big Pickle' }

# --- Prompt ---
if ($FichierPrompt) {
    $chemin = if ([IO.Path]::IsPathRooted($FichierPrompt)) { $FichierPrompt } else { Join-Path $racine $FichierPrompt }
    $Prompt = [IO.File]::ReadAllText($chemin, $utf8)
}
if (-not $Prompt -or -not $Prompt.Trim()) { throw 'Prompt vide : passer -FichierPrompt ou -Prompt.' }

# --- Session : on reprend celle de l'appel précédent, sauf -Nouvelle ---
$etat = $null
if (-not $Nouvelle -and (Test-Path $fichierEtat)) { $etat = [IO.File]::ReadAllText($fichierEtat, $utf8) | ConvertFrom-Json }
if (-not $etat) { $etat = New-Object PSObject -Property @{ session = ''; journal = '' } }

# --- Journal : celui imposé par l'orchestrateur, sinon un fichier par session ---
if ($Journal) {
    $fichierJournal = $Journal
}
else {
    if ($Nouvelle -or -not $etat.journal -or -not (Test-Path $etat.journal)) {
        $nomJournal = '{0} - {1} - {2}.md' -f (Get-Date -Format "yyyy-MM-dd HH'h'mm"), $nomModele, $Sujet
        $etat.journal = Join-Path $dossierJournal $nomJournal
        $entete = "---`ntags: [ia, journal]`n---`n# {0}`n`nRetour au [[Journal des sessions]].`n" -f [IO.Path]::GetFileNameWithoutExtension($etat.journal)
        [IO.File]::WriteAllText($etat.journal, $entete, $utf8)
    }
    $fichierJournal = $etat.journal
}

function Ajouter-AuJournal([string]$titre, [string]$texte) {
    $bloc = "`n### {0} · {1}`n`n{2}`n" -f (Get-Date -Format 'HH:mm:ss'), $titre, $texte.TrimEnd()
    [IO.File]::AppendAllText($fichierJournal, $bloc, $utf8)
}

Ajouter-AuJournal "$Origine → $nomModele ($Mode)" $Prompt

# --- Appel du modèle (le prompt passe par l'entrée standard : pas de problème de guillemets) ---
Push-Location $racine
try {
    if ($Modele -eq 'claude') {
        $modeleClaude = @{ rapide = 'haiku'; normal = 'sonnet'; expert = 'opus' }[$Niveau]
        $arguments = @('-p', '--output-format', 'json', '--model', $modeleClaude)
        $arguments += if ($Mode -eq 'edition') { @('--permission-mode', 'acceptEdits') } else { @('--permission-mode', 'plan') }
        if ($etat.session) { $arguments += @('--resume', $etat.session) }
        if ($BudgetUsd -gt 0) { $arguments += @('--max-budget-usd', $BudgetUsd.ToString([Globalization.CultureInfo]::InvariantCulture)) }
        $sortie = ($Prompt | & claude @arguments) -join "`n"
        $resultat = $sortie | ConvertFrom-Json
        $reponse = [string]$resultat.result
        $etat.session = $resultat.session_id
        $bilan = 'session {0} · coût {1:N4} $' -f $resultat.session_id, [double]$resultat.total_cost_usd
        if ($resultat.is_error) { $reponse = "ERREUR ($($resultat.subtype)) : $reponse" }
    }
    else {
        $arguments = @('run', '--model', 'opencode/big-pickle', '--format', 'json')
        if ($Mode -eq 'edition') { $arguments += '--auto' }
        if ($Agent) { $arguments += @('--agent', $Agent) }
        if ($etat.session) { $arguments += @('--session', $etat.session) }
        $lignes = $Prompt | & opencode @arguments
        $morceaux = @()
        foreach ($ligne in $lignes) {
            if (-not $ligne.StartsWith('{')) { continue }
            $evenement = $ligne | ConvertFrom-Json
            if ($evenement.sessionID) { $etat.session = $evenement.sessionID }
            if ($evenement.type -eq 'text') { $morceaux += $evenement.part.text }
        }
        $reponse = ($morceaux -join "`n`n")
        if (-not $reponse) { $reponse = "ERREUR : aucune réponse texte reçue.`n" + ($lignes -join "`n") }
        $bilan = 'session {0}' -f $etat.session
    }
}
catch {
    $reponse = "ERREUR du relais : $($_.Exception.Message)"
    $bilan = 'échec'
}
finally {
    Pop-Location
}

Ajouter-AuJournal "$nomModele ($bilan)" $reponse
[IO.File]::WriteAllText($fichierEtat, ($etat | ConvertTo-Json), $utf8)

# --- Sortie pour le prompteur : la réponse telle quelle, puis le bilan ---
Write-Output $reponse
Write-Output ''
Write-Output "--- relais : $nomModele · $bilan · journal : $($fichierJournal.Replace($racine + '\', ''))"
