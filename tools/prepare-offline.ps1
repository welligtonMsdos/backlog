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

    Get-ChildItem src,tests -Filter packages.lock.json -Recurse | ForEach-Object
    {
        $lock = Get-Content $_.FullName -Raw | ConvertFrom-Json

        $lock.dependencies.PSObject.Properties | ForEach-Object
        {
            $_.Value.PSObject.Properties | ForEach-Object
            {
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

    foreach ($image in @('mcr.microsoft.com/dotnet/sdk:10.0', 'mcr.microsoft.com/dotnet/aspnet:10.0', 'postgres:17-alpine'))
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
