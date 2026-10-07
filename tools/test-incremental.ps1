param([string]$Filter = '')

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent

$overlay = Join-Path $repoRoot 'artifacts/test-overlay'

New-Item -ItemType Directory -Path $overlay -Force | Out-Null

Get-ChildItem (Join-Path $repoRoot 'src'),(Join-Path $repoRoot 'tests') -Filter '*.cs' -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
    $relative = $_.FullName.Substring($repoRoot.Length + 1)

    $destination = Join-Path $overlay $relative

    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null

    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}

Push-Location $repoRoot

try
{
    $arguments = @('compose', '--profile', 'test', 'run', '--rm', '--volume', "${overlay}:/overlay:ro", '--entrypoint', '/bin/sh', 'tests', '-c')

    $command = 'cp -a /overlay/. /workspace/ && dotnet test Backlog.sln -c Release --no-restore --logger trx --results-directory /results'

    if ($Filter)
    {
        if ($Filter -notmatch '^[a-zA-Z0-9_.~|=]+$') { throw 'Filtro inválido' }

        $command += " --filter '$Filter'"
    }

    docker @arguments $command

    if ($LASTEXITCODE -ne 0) { throw 'Testes incrementais falharam' }
}
finally
{
    Pop-Location
}
