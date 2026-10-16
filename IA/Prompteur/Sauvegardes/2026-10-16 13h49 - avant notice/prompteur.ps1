<#
.SYNOPSIS
    Prompteur du projet : lit vos messages, laisse le modèle local décider du destinataire,
    puis transmet le message ORIGINAL à Claude ou Big Pickle avec relais.ps1 (qui écrit IA/Journal).

.DESCRIPTION
    Le modèle local (« prompteur », qwen2.5:3b sous Ollama) ne voit que le message et ne renvoie
    qu'une décision JSON : destinataire, niveau, mode, nouvelle session, sujet. Il ne peut ni modifier
    le message ni répondre à la place de Claude ou Big Pickle.

    Préfixes pour imposer un choix (sans passer par le modèle local pour ce champ), combinables :
        /claude  /bp            destinataire
        /rapide  /normal  /expert   niveau de Claude
        /lecture /edition       le modèle peut-il modifier les fichiers ?
        /nouveau                nouvelle session (nouveau sujet)
    Commandes seules : /aide, /quitter. Message sur plusieurs lignes : << puis le texte puis >>.

.EXAMPLE
    .\IA\Prompteur\prompteur.ps1
    .\IA\Prompteur\prompteur.ps1 -Message "combien de scripts .cs ?" -SeulementDecision
#>
param(
    # Un seul message, sans boucle interactive
    [string]$Message,
    # Affiche la décision sans appeler Claude ni Big Pickle (test du tri, gratuit)
    [switch]$SeulementDecision
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$relais = Join-Path $PSScriptRoot 'relais.ps1'
$fichierConsignes = Join-Path $PSScriptRoot 'Consignes prompteur.md'
$MODELE_LOCAL = 'prompteur'
$URL_OLLAMA = 'http://localhost:11434/api/chat'
# Le destinataire se décide par mots-clés, pas par le modèle local (essai : il envoyait une question d'architecture à Big Pickle)
$REGEX_GIT = '(?i)\b(git|github|commit\w*|branche\w*|push\w*|pouss\w*|pull request|issue\w*|milestone\w*|merge\w*|fusionn\w*)\b'

# Consignes = instructions système du modèle local (sans l'en-tête Obsidian)
$consignes = [IO.File]::ReadAllText($fichierConsignes, $utf8) -replace '(?s)^---.*?---\s*', ''

# Schéma imposé à la réponse du modèle local : il ne peut rien renvoyer d'autre
$schema = @{
    type       = 'object'
    properties = @{
        niveau   = @{ type = 'string'; enum = @('rapide', 'normal', 'expert') }
        mode     = @{ type = 'string'; enum = @('lecture', 'edition') }
        nouvelle = @{ type = 'boolean' }
        sujet    = @{ type = 'string' }
    }
    required   = @('niveau', 'mode', 'nouvelle', 'sujet')
}

<# Demande la décision au modèle local. En cas d'échec (Ollama arrêté…), décision par défaut. #>
function Obtenir-Decision([string]$texte) {
    $corps = @{
        model    = $MODELE_LOCAL
        stream   = $false
        format   = $schema
        messages = @(
            @{ role = 'system'; content = $consignes },
            @{ role = 'user'; content = $texte }
        )
    } | ConvertTo-Json -Depth 10
    try {
        $reponse = Invoke-RestMethod -Uri $URL_OLLAMA -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($corps))
        $contenu = $reponse.message.content
        # Invoke-RestMethod de PowerShell 5.1 peut décoder l'UTF-8 comme du Latin-1 : on corrige
        if ($contenu -match 'Ã') { $contenu = $utf8.GetString([Text.Encoding]::GetEncoding(28591).GetBytes($contenu)) }
        return $contenu | ConvertFrom-Json
    }
    catch {
        Write-Host "(modèle local indisponible : $($_.Exception.Message) — choix par défaut)" -ForegroundColor DarkYellow
        return New-Object PSObject -Property @{ niveau = 'normal'; mode = 'lecture'; nouvelle = $false; sujet = 'session' }
    }
}

<# Retire les préfixes /xxx du début du message et renvoie les choix imposés. #>
function Lire-Prefixes([string]$texte) {
    $imposes = @{}
    $correspondances = @{
        '/claude' = @('modele', 'claude'); '/bp' = @('modele', 'bigpickle')
        '/rapide' = @('niveau', 'rapide'); '/normal' = @('niveau', 'normal'); '/expert' = @('niveau', 'expert')
        '/lecture' = @('mode', 'lecture'); '/edition' = @('mode', 'edition'); '/nouveau' = @('nouvelle', $true)
    }
    while ($texte -match '^\s*(/[a-z]+)\b\s*') {
        $prefixe = $Matches[1]
        if (-not $correspondances.ContainsKey($prefixe)) { break }
        $imposes[$correspondances[$prefixe][0]] = $correspondances[$prefixe][1]
        $texte = $texte.Substring($Matches[0].Length)
    }
    return @{ texte = $texte; imposes = $imposes }
}

<# Traite un message : décision, confirmation si besoin, transmission. #>
function Traiter-Message([string]$saisie) {
    $lu = Lire-Prefixes $saisie
    $texte = $lu.texte.Trim()
    if (-not $texte) { return }

    $decision = Obtenir-Decision $texte
    $decision | Add-Member -NotePropertyName modele -NotePropertyValue $(if ($texte -match $REGEX_GIT) { 'bigpickle' } else { 'claude' }) -Force
    foreach ($champ in $lu.imposes.Keys) { $decision.$champ = $lu.imposes[$champ] }
    $sujet = ($decision.sujet -replace '[^\p{L}\p{N} -]', '').Trim()
    if (-not $sujet) { $sujet = 'session' }

    $nom = if ($decision.modele -eq 'claude') { "Claude ($($decision.niveau))" } else { 'Big Pickle' }
    $session = if ($decision.nouvelle) { "nouvelle session « $sujet »" } else { 'session en cours' }
    Write-Host "→ $nom · $($decision.mode) · $session" -ForegroundColor Cyan
    if ($SeulementDecision) { return }

    # Garde-fous : les choix coûteux ou qui modifient des fichiers sont confirmés
    if ($decision.mode -eq 'edition' -or $decision.niveau -eq 'expert') {
        $ok = Read-Host 'Confirmer ? (o/n)'
        if ($ok -notmatch '^[oOyY]') { Write-Host 'Annulé. Renvoyez le message avec des préfixes (/lecture, /normal…) pour changer le choix.'; return }
    }

    $arguments = @{ Modele = $decision.modele; Prompt = $texte; Mode = $decision.mode; Sujet = $sujet }
    if ($decision.modele -eq 'claude') { $arguments.Niveau = $decision.niveau }
    if ($decision.nouvelle) { $arguments.Nouvelle = $true }
    & $relais @arguments | ForEach-Object { Write-Host $_ }
}

# --- Un seul message ---
if ($Message) { Traiter-Message $Message; return }

# --- Boucle interactive ---
Write-Host 'Prompteur prêt. /aide pour les préfixes, /quitter pour sortir.' -ForegroundColor Green
while ($true) {
    Write-Host ''
    $saisie = Read-Host 'Vous'
    if ($saisie -eq '/quitter') { break }
    if ($saisie -eq '/aide') { Get-Help $PSCommandPath -Full | Out-String | Write-Host; continue }
    if ($saisie.Trim() -eq '<<') {
        $lignes = @()
        while (($ligne = Read-Host '…') -ne '>>') { $lignes += $ligne }
        $saisie = $lignes -join "`n"
    }
    Traiter-Message $saisie
}
