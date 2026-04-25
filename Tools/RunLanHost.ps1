param(
    [int]$Port = 7777,
    [switch]$Windowed = $true
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'Builds\Smoke\MarcoPoloSmoke\MarcoPoloSmoke.exe'
if (!(Test-Path $exe)) {
    throw "No existe build: $exe. Crea build desde Unity: Tools/Smoke/BuildGameSceneWindows o ejecuta StandaloneSmokeBuild."
}

$logDir = Join-Path $repo 'Builds\Smoke'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'radmin-host.log'

$args = @('-logFile', $log, '-fpLanHost', '-fpPort', $Port)
if ($Windowed) {
    $args = @('-screen-width','1280','-screen-height','720','-popupwindow') + $args
}

Write-Host "Iniciando host LAN en puerto UDP $Port"
Write-Host "Tu amigo debe conectar a tu IP Radmin 26.x.x.x:$Port"
Write-Host "Log: $log"
Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe)
