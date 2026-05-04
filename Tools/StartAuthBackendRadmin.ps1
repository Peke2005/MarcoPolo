param(
    [string]$RadminIp = '26.17.117.206',
    [int]$Port = 3001
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $repo 'Backend'

Write-Host "Starting auth backend Docker in $backend"
Push-Location $backend
try {
    docker compose up -d --build
} finally {
    Pop-Location
}

try {
    $ruleName = 'FrentePartido Auth 3001 TCP'
    if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
        Write-Host "Firewall rule created: $ruleName"
    }
} catch {
    Write-Warning "Firewall rule not created. Run PowerShell as admin if friends cannot reach port $Port. $($_.Exception.Message)"
}

Write-Host 'Local health:'
curl.exe -sS --max-time 5 "http://127.0.0.1:$Port/health"
Write-Host "`nRadmin health ($RadminIp):"
curl.exe -sS --max-time 5 "http://$RadminIp`:$Port/health"
Write-Host "`nIf Radmin health fails: check Radmin VPN, Docker, and Windows Firewall on this PC."
