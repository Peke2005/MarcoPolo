param(
    [string]$DatabaseUrl = '',
    [string]$JwtSecret = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repo 'Backend\.env'

if ([string]::IsNullOrWhiteSpace($DatabaseUrl)) {
    $DatabaseUrl = Read-Host 'Supabase DATABASE_URL'
}

if ([string]::IsNullOrWhiteSpace($JwtSecret)) {
    $JwtSecret = Read-Host 'JWT_SECRET largo'
}

$DatabaseUrl = $DatabaseUrl.Trim().Trim('"').Trim("'")
$JwtSecret = $JwtSecret.Trim()

if ($DatabaseUrl -notmatch '^postgres(ql)?://') {
    throw 'DATABASE_URL debe empezar por postgres:// o postgresql://'
}

if ($DatabaseUrl -match 'PROJECT_REF|PASSWORD|change-me|your-|example') {
    throw 'DATABASE_URL parece placeholder. Copia la connection string real de Supabase.'
}

if ($JwtSecret.Length -lt 24 -or $JwtSecret -match 'change-me|secret|password') {
    throw 'JWT_SECRET debe ser largo y no obvio. Usa una cadena aleatoria de 24+ caracteres.'
}

$content = @"
DATABASE_URL=$DatabaseUrl
PGSSLMODE=require
JWT_SECRET=$JwtSecret
AUTH_STORE=postgres
PORT=3001
"@

Set-Content -Path $envPath -Value $content -Encoding ASCII
Write-Host "Backend\.env creado. No se sube a git."
Write-Host "Ahora ejecuta HOST_RADMIN.bat o Tools\StartAuthBackendRadmin.ps1."
