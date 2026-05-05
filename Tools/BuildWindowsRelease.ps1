param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$log = Join-Path $repo 'Temp\unity-release-build.log'
$buildDir = Join-Path $repo 'Builds\Release\FrentePartido'
$zip = Join-Path $repo 'Builds\Release\FrentePartido-Windows.zip'

$args = @(
    '-batchmode',
    '-quit',
    '-projectPath', $repo,
    '-executeMethod', 'FrentePartido.Editor.StandaloneSmokeBuild.BuildWindowsRelease',
    '-logFile', $log
)

$process = Start-Process -FilePath $Unity -ArgumentList $args -PassThru -Wait
if ($process.ExitCode -ne 0 -and !(Select-String -Path $log -Pattern 'Release built' -Quiet)) {
    Get-Content $log -Tail 120
    throw "Unity build failed: $($process.ExitCode)"
}

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $buildDir '*') -DestinationPath $zip -Force
Write-Host "Built: $buildDir"
Write-Host "Zip:   $zip"
