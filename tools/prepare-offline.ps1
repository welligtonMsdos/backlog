$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent

Push-Location $repoRoot

try
{
    dotnet restore Backlog.sln --locked-mode

    if ($LASTEXITCODE -ne 0) { throw 'Falha no restore' }

    $feed = Join-Path $repoRoot '.offline/nuget'

    New-Item -ItemType Directory -Path $feed -Force | Out-Null

    $cache = (dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', ''

    $packages = @{}

    Get-ChildItem src,tests -Filter packages.lock.json -Recurse | ForEach-Object {
        $lock = Get-Content $_.FullName -Raw | ConvertFrom-Json

        $lock.dependencies.PSObject.Properties | ForEach-Object {
            $_.Value.PSObject.Properties | ForEach-Object {
                if ($_.Value.type -ne 'Project')
                {
                    $packages[($_.Name.ToLowerInvariant() + '/' + $_.Value.resolved)] = $true
                }
            }
        }
    }

    foreach ($package in $packages.Keys)
    {
        $parts = $package.Split('/')

        $source = Join-Path $cache "$package/$($parts[0]).$($parts[1]).nupkg"

        Copy-Item -LiteralPath $source -Destination $feed
    }

    foreach ($image in @('mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317', 'mcr.microsoft.com/dotnet/aspnet:10.0@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4', 'postgres:17-alpine@sha256:18cfe3ef5e6815560c98237d6216d1e5119702fb0f3894c8785dd58b8bbe5d73'))
    {
        docker image inspect $image --format '{{.Id}}' 2>$null | Out-Null

        if ($LASTEXITCODE -ne 0)
        {
            docker pull $image

            if ($LASTEXITCODE -ne 0) { throw 'Imagem indisponivel' }
        }
    }

    Write-Output 'Cache local preparado. Build: docker compose build --no-cache (rede do build desabilitada no Compose).'
}
finally
{
    Pop-Location
}
