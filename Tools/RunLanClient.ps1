param(
    [Parameter(Mandatory=$true)]
    [string]$Address,
    [int]$Port = 7777,
    [switch]$Windowed = $true
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'Builds\Smoke\MarcoPoloSmoke\MarcoPoloSmoke.exe'
if (!(Test-Path $exe)) {
    throw "No existe build: $exe. Usa la misma build que el host."
}

$logDir = Join-Path $repo 'Builds\Smoke'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'radmin-client.log'

$args = @('-logFile', $log, '-fpLanClient', '-fpAddress', $Address, '-fpPort', $Port)
if ($Windowed) {
    $args = @('-screen-width','1280','-screen-height','720','-popupwindow') + $args
}

Write-Host "Conectando a host LAN $Address`:$Port"
Write-Host "Log: $log"
Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe)
