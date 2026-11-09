<#
.SYNOPSIS
    Lance plusieurs instances du client Juge (CLI) dans des fenêtres de terminal distinctes sur le même PC.

.DESCRIPTION
    Ce script démarre N instances de House.Of.Arbitration.Judge.Cli, chacune configurée avec sa position de juge (ex: Juge 1, 2, 3...).
    Grâce au support multi-instances, toutes les fenêtres peuvent être connectées simultanément au même serveur.

.PARAMETER Count
    Nombre de juges à lancer (par défaut 3, maximum 5 pour Sanda, ou 3 pour Taolu).

.PARAMETER Mode
    Mode de jugement : 'sanda' (positions 1, 2, 3...) ou 'taolu' (positions A_1, B_1, C_1...).

.PARAMETER Connect
    Index ou adresse du serveur auquel se connecter automatiquement (optionnel).

.EXAMPLE
    .\run-judges.ps1 -Count 3
    Lance 3 juges Sanda (Juge 1, Juge 2, Juge 3)

.EXAMPLE
    .\run-judges.ps1 -Count 5 -Connect 1
    Lance 5 juges Sanda et se connecte automatiquement au premier serveur détecté.

.EXAMPLE
    .\run-judges.ps1 -Mode taolu -Connect 1
    Lance 3 juges Taolu (A_1, B_1, C_1).
#>

param(
    [int]$Count = 3,
    [ValidateSet("sanda", "taolu")]
    [string]$Mode = "sanda",
    [string]$Connect = ""
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "House.Of.Arbitration.Judge.Cli"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " LANCEMENT MULTI-JUGES CLI (House of Arbitration)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Mode         : $Mode" -ForegroundColor Yellow
Write-Host "Nombre juges : $Count" -ForegroundColor Yellow
if ($Connect) {
    Write-Host "Connexion auto au serveur : $Connect" -ForegroundColor Yellow
}
Write-Host ""

$positions = @()
if ($Mode -eq "taolu") {
    $groups = @("A_1", "B_1", "C_1", "A_2", "B_2", "C_2", "A_3", "B_3", "C_3")
    $Count = [Math]::Min($Count, $groups.Count)
    for ($i = 0; $i -lt $Count; $i++) {
        $positions += $groups[$i]
    }
} else {
    $Count = [Math]::Min($Count, 5)
    for ($i = 1; $i -le $Count; $i++) {
        $positions += "$i"
    }
}

foreach ($pos in $positions) {
    $argList = @("--project", "`"$projectPath`"", "--", "--pos", "$pos")
    if ($Connect) {
        $argList += @("--connect", "$Connect")
    }

    $cmdString = "dotnet run " + ($argList -join " ")
    Write-Host "Lancement de la fenêtre pour le Juge $pos..." -ForegroundColor Green

    # Lance un nouveau terminal PowerShell pour chaque juge
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '=== JUGE $pos ===' -ForegroundColor Cyan; $cmdString"
    Start-Sleep -Milliseconds 400
}

Write-Host ""
Write-Host "$Count fenêtre(s) de juges démarrée(s) avec succès !" -ForegroundColor Cyan
