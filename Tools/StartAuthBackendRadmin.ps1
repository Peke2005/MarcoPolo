param(
    [string]$RadminIp = '',
    [int]$Port = 3001
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $repo 'Backend'

if ([string]::IsNullOrWhiteSpace($RadminIp)) {
    $RadminIp = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -like '26.*' -and $_.InterfaceAlias -like '*Radmin*' } |
        Select-Object -First 1 -ExpandProperty IPAddress)
}
if ([string]::IsNullOrWhiteSpace($RadminIp)) {
    $RadminIp = '26.234.30.190'
}

$localOk = $false
try {
    curl.exe -sS --max-time 2 "http://127.0.0.1:$Port/health" | Out-Null
    $localOk = $LASTEXITCODE -eq 0
} catch {
    $localOk = $false
}

if ($localOk) {
    Write-Host "Auth backend already running on http://127.0.0.1:$Port"
} else {
    Write-Host "Starting auth backend Docker in $backend"
    Push-Location $backend
    try {
        docker compose up -d --build
    } catch {
        Write-Warning "Docker start failed. If Docker Desktop is closed, open it or start backend manually. $($_.Exception.Message)"
    } finally {
        Pop-Location
    }
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
Write-Host "`nFriend auth URL should be: http://$RadminIp`:$Port"
Write-Host "If Radmin health fails: check Radmin VPN and Windows Firewall on this PC."
