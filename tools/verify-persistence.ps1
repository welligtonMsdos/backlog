$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent

$previousPort = $env:API_PORT

$env:API_PORT = '18080'

Push-Location $repoRoot

try
{
    docker compose -p backlog-persistence-check -f compose.yaml -f compose.persistent.yaml up -d --wait api

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar a verificação persistente' }

    docker compose -p backlog-persistence-check -f compose.yaml -f compose.persistent.yaml exec -T postgres psql -U backlog -d backlog -v ON_ERROR_STOP=1 -c 'CREATE TABLE persistence_probe (id integer PRIMARY KEY); INSERT INTO persistence_probe VALUES (1);'

    if ($LASTEXITCODE -ne 0) { throw 'Falha na gravação de teste' }

    docker compose -p backlog-persistence-check -f compose.yaml -f compose.persistent.yaml restart postgres

    if ($LASTEXITCODE -ne 0) { throw 'Falha no restart' }

    docker compose -p backlog-persistence-check -f compose.yaml -f compose.persistent.yaml up -d --wait api

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao aguardar saúde' }

    $count = docker compose -p backlog-persistence-check -f compose.yaml -f compose.persistent.yaml exec -T postgres psql -U backlog -d backlog -Atc 'SELECT count(*) FROM persistence_probe'

    if ($LASTEXITCODE -ne 0 -or $count.Trim() -ne '1') { throw 'Persistência não confirmada' }

    Write-Output 'Migrations e retenção após restart confirmadas em projeto Docker isolado.'
}
finally
{
    docker compose -p backlog-persistence-check -f compose.yaml -f compose.persistent.yaml down -v

    $env:API_PORT = $previousPort

    Pop-Location
}
