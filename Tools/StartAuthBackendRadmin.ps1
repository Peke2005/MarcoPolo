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

$dockerDesktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
if (-not (Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue) -and (Test-Path $dockerDesktop)) {
    Write-Host "Starting Docker Desktop..."
    Start-Process -FilePath $dockerDesktop -WindowStyle Hidden
}

Write-Host "Waiting for Docker daemon..."
$dockerReady = $false
for ($i = 0; $i -lt 60; $i++) {
    docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        $dockerReady = $true
        break
    }
    Start-Sleep -Seconds 2
}
if (-not $dockerReady) {
    throw "Docker daemon not ready. Open Docker Desktop and wait until it says Running."
}

$listeners = netstat -ano | Select-String ":$Port\s" | Where-Object { $_.Line -match 'LISTENING' }
foreach ($listener in $listeners) {
    $listenerPid = [int](($listener.Line -split '\s+')[-1])
    $proc = Get-CimInstance Win32_Process -Filter "ProcessId=$listenerPid" -ErrorAction SilentlyContinue
    if ($proc -and $proc.Name -match '^node\.exe$' -and $proc.CommandLine -match 'src/server\.js') {
        Write-Host "Stopping local Node auth backend on port $Port (PID $listenerPid). Docker will own this port."
        Stop-Process -Id $listenerPid -Force
    }
}

Write-Host "Starting auth backend Docker in $backend"
Push-Location $backend
try {
    $envPath = Join-Path $backend '.env'
    $useSupabase = $false
    if (Test-Path $envPath) {
        $useSupabase = Select-String -Path $envPath -Pattern '^\s*DATABASE_URL\s*=' -Quiet
    }

    if ($useSupabase) {
        Write-Host "Supabase DATABASE_URL found in Backend\.env. Starting API without local Postgres container."
        docker compose -f docker-compose.supabase.yml up -d --build
    } else {
        Write-Host "No Supabase DATABASE_URL found. Starting local Docker Postgres + API."
        docker compose up -d --build
    }
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
    Write-Warning "Firewall rule not created because this PowerShell is not admin. This is only a problem if friends cannot login. $($_.Exception.Message)"
}

Write-Host 'Local health:'
curl.exe -sS --max-time 5 "http://127.0.0.1:$Port/health"
Write-Host "`nRadmin health ($RadminIp):"
$radminHealthOk = $false
try {
    $radminHealth = curl.exe -sS --max-time 5 "http://$RadminIp`:$Port/health"
    Write-Host $radminHealth
    if ($LASTEXITCODE -eq 0 -and $radminHealth -match '"status"\s*:\s*"ok"') {
        $radminHealthOk = $true
    }
} catch {
    Write-Host $_.Exception.Message
}

if ($radminHealthOk) {
    Write-Host "`nOK: backend is reachable through Radmin on this PC."
    Write-Host "If your friend still cannot login, open PowerShell as admin and run this script again to create the firewall rule."
} else {
    Write-Warning "Radmin health failed. Check Radmin VPN and Windows Firewall on this PC."
}

Write-Host "`nFriend auth URL should be: http://$RadminIp`:$Port"
