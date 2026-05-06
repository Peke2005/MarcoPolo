param(
    [string]$DatabaseUrl = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $repo 'Backend'

if ([string]::IsNullOrWhiteSpace($DatabaseUrl)) {
    $envPath = Join-Path $backend '.env'
    if (Test-Path $envPath) {
        $line = Get-Content $envPath | Where-Object { $_ -match '^\s*DATABASE_URL\s*=' } | Select-Object -First 1
        if ($line) {
            $DatabaseUrl = ($line -replace '^\s*DATABASE_URL\s*=\s*', '').Trim().Trim('"').Trim("'")
        }
    }
}

if ([string]::IsNullOrWhiteSpace($DatabaseUrl)) {
    $DatabaseUrl = Read-Host 'Supabase DATABASE_URL'
}

$DatabaseUrl = $DatabaseUrl.Trim().Trim('"').Trim("'")
if ($DatabaseUrl -notmatch '^postgres(ql)?://') {
    throw 'DATABASE_URL debe empezar por postgres:// o postgresql://'
}
if ($DatabaseUrl -match 'PROJECT_REF|PASSWORD|change-me|your-|example') {
    throw 'DATABASE_URL parece placeholder. Copia la connection string real de Supabase.'
}

Push-Location $backend
try {
    if (!(Test-Path node_modules)) {
        npm install --omit=dev
    }
    $env:DATABASE_URL = $DatabaseUrl
    node scripts/apply-supabase-schema.js
} finally {
    Pop-Location
}
