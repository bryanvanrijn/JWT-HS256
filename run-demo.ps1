# ============================================================================
#  run-demo.ps1
# ----------------------------------------------------------------------------
#  Opent DRIE losse PowerShell-vensters, één per applicatie, in de juiste
#  startvolgorde (eerst de ontvangers, dan de Gateway). Zo kun je de demo
#  met eigen ogen volgen en zelf op ENTER drukken.
#
#  Gebruik (vanuit de projectmap):
#      powershell -ExecutionPolicy Bypass -File .\run-demo.ps1
#
#  Daarna:
#    1) Druk ENTER in het GATEWAY-venster      -> App1 toont "Bericht ONTVANGEN"
#    2) Druk ENTER in het CONSOLEAPP1-venster  -> App2 toont "ONTVANGEN via de Gateway"
#    Typ 'exit' in Gateway/App1 om te stoppen; App2 sluit je met Ctrl+C.
# ============================================================================

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Eerst één keer bouwen, zodat de drie vensters meteen kunnen starten.
Write-Host "Bouwen..." -ForegroundColor Cyan
dotnet build "$root\JwtGatewaySample.slnx" | Out-Null

# Helper: open een nieuw PowerShell-venster dat één app start en open blijft.
function Open-AppWindow($title, $projectPath) {
    $cmd = "`$host.UI.RawUI.WindowTitle='$title'; dotnet run --no-build --project `"$projectPath`""
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $cmd
}

# Volgorde: ontvangers eerst, Gateway als laatste.
Open-AppWindow 'ConsoleApp2 (:5002)' "$root\ConsoleApp2"
Start-Sleep -Seconds 2
Open-AppWindow 'ConsoleApp1 (:5001)' "$root\ConsoleApp1"
Start-Sleep -Seconds 2
Open-AppWindow 'GATEWAY (:5000)'     "$root\Gateway"

Write-Host ""
Write-Host "Drie vensters geopend." -ForegroundColor Green
Write-Host "1) ENTER in het GATEWAY-venster      -> App1 ontvangt het bericht"
Write-Host "2) ENTER in het CONSOLEAPP1-venster  -> App2 ontvangt het via de Gateway"
