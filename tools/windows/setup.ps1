# Project Apex - one-click Windows setup.
# Finds an installed Unity 6 editor, creates the Unity project in unity\ProjectApex
# (first run only), adds the game package, runs the "Project Apex" setup in batch
# mode and finally opens Unity with the Boot scene in Play mode.
# Requirements: Unity Hub with a Unity 6 (6000.x) editor installed and signed in.

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProjectPath = Join-Path $Root 'unity\ProjectApex'
$LogDir = Join-Path $Root 'unity\Logs-Setup'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-Step($text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Fail($text) {
    Write-Host ""
    Write-Host "FEHLER: $text" -ForegroundColor Red
    exit 1
}

# --- 1. Find Unity 6 -----------------------------------------------------------
Write-Step "Suche Unity 6 ..."
$editorRoots = @(
    (Join-Path $env:ProgramFiles 'Unity\Hub\Editor'),
    (Join-Path ${env:ProgramFiles(x86)} 'Unity\Hub\Editor')
)
$secondary = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
if (Test-Path $secondary) {
    $p = (Get-Content $secondary -Raw).Trim().Trim('"')
    if ($p) { $editorRoots += $p }
}
$editors = @()
foreach ($r in $editorRoots) {
    if ($r -and (Test-Path $r)) {
        $editors += Get-ChildItem -Path $r -Directory | Where-Object {
            $_.Name -like '6000.*' -and (Test-Path (Join-Path $_.FullName 'Editor\Unity.exe'))
        }
    }
}
if ($editors.Count -eq 0) {
    Write-Host "Kein Unity 6 gefunden. Bitte zuerst installieren:" -ForegroundColor Yellow
    Write-Host "  1. Unity Hub: https://unity.com/download (Konto anlegen/anmelden, Personal-Lizenz)"
    Write-Host "  2. Im Hub: Installs -> Install Editor -> neueste Unity 6 LTS (+ Android Build Support)"
    Write-Host "  3. Dieses Skript erneut starten."
    Start-Process 'https://unity.com/download'
    exit 1
}
$editor = $editors | Sort-Object { [version]($_.Name -replace '[a-zA-Z].*$', '') } -Descending | Select-Object -First 1
$Unity = Join-Path $editor.FullName 'Editor\Unity.exe'
Write-Host "Gefunden: Unity $($editor.Name)"

function Run-Unity($arguments, $logName) {
    $log = Join-Path $LogDir $logName
    $argLine = ($arguments + @('-logFile', "`"$log`"")) -join ' '
    $proc = Start-Process -FilePath $Unity -ArgumentList $argLine -Wait -PassThru -WindowStyle Hidden
    if ($proc.ExitCode -ne 0) {
        Write-Host "Unity meldet Exit-Code $($proc.ExitCode). Letzte Zeilen aus ${log}:" -ForegroundColor Yellow
        if (Test-Path $log) { Get-Content $log -Tail 40 | ForEach-Object { Write-Host "  $_" } }
        Fail "Siehe Log oben. Haeufige Ursache: im Unity Hub nicht angemeldet (Lizenz) oder keine Internetverbindung."
    }
}

# --- 2. Create project (first run only) ----------------------------------------
if (-not (Test-Path (Join-Path $ProjectPath 'ProjectSettings'))) {
    Write-Step "Erstelle Unity-Projekt (dauert einige Minuten) ..."
    Run-Unity @('-batchmode', '-quit', '-createProject', "`"$ProjectPath`"") 'create-project.log'
} else {
    Write-Step "Unity-Projekt existiert bereits: $ProjectPath"
}

# --- 3. Add the game package to Packages\manifest.json -------------------------
Write-Step "Trage das Spielpaket ein ..."
$manifestPath = Join-Path $ProjectPath 'Packages\manifest.json'
if (-not (Test-Path $manifestPath)) { Fail "manifest.json nicht gefunden: $manifestPath" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.dependencies | Add-Member -NotePropertyName 'com.projectapex.game' -NotePropertyValue 'file:../../com.projectapex.game' -Force
$manifest | Add-Member -NotePropertyName 'testables' -NotePropertyValue @('com.projectapex.game') -Force
$json = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding $false))

# --- 4. Run the Project Apex setup in batch mode -------------------------------
Write-Step "Importiere Pakete und richte das Projekt ein (beim ersten Mal mehrere Minuten) ..."
Run-Unity @('-batchmode', '-quit', '-projectPath', "`"$ProjectPath`"", '-executeMethod', 'Apex.EditorTools.ApexProjectSetup.SetupAll') 'setup.log'

# --- 5. Open Unity and start the game -------------------------------------------
Write-Step "Starte Unity mit der Boot-Szene ..."
$argLine = "-projectPath `"$ProjectPath`" -executeMethod Apex.EditorTools.ApexProjectSetup.OpenBootSceneAndPlay"
Start-Process -FilePath $Unity -ArgumentList $argLine
Write-Host ""
Write-Host "Fertig. Unity oeffnet sich und startet das Spiel automatisch." -ForegroundColor Green
Write-Host "Steuerung: W/S Gas/Bremse, A/D lenken, C Kamera, Ruecktaste Reset, F3 Telemetrie, Esc Pause."
Write-Host "Spaeter erneut starten: diese Datei wieder doppelklicken (geht dann schnell)."
