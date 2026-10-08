$ErrorActionPreference = 'Stop'

$path = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'

if (Test-Path -LiteralPath $path) { throw '.env ja existe; nao sera sobrescrito.' }

function New-Secret {
    $bytes = New-Object byte[] 32

    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }

    return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
}

$lines = @(
    "DB_PASSWORD=$(New-Secret)",
    "JWT_KEY=$(New-Secret)",
    'BOOTSTRAP_NAME=Gestor inicial',
    'BOOTSTRAP_EMAIL=gestor@backlog.local',
    "BOOTSTRAP_PASSWORD=$(New-Secret)",
    'API_PORT=8080',
    'DOCUMENTATION_ENABLED=true',
    'JWT_ACCESS_TOKEN_MINUTES=15'
)

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.UTF8Encoding]::new($false))

Write-Output '.env criado com segredos aleatorios. Consulte-o localmente para o login inicial.'
