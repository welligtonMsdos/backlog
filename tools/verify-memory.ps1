$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent

$previousPort = $env:API_PORT

$env:API_PORT = '18081'

Push-Location $repoRoot

try
{
    docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml up -d --wait api

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar API com PostgreSQL em tmpfs' }

    $before = docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml exec -T postgres psql -U backlog -d backlog -Atc 'SELECT id FROM users'

    if ($LASTEXITCODE -ne 0 -or !$before) { throw 'Gestor inicial ausente' }

    docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml restart postgres

    if ($LASTEXITCODE -ne 0) { throw 'Falha no restart' }

    docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml up -d --wait postgres

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao aguardar PostgreSQL' }

    $count = docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml exec -T postgres psql -U backlog -d backlog -Atc "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename = 'users'"

    if ($LASTEXITCODE -ne 0 -or $count.Trim() -ne '0') { throw 'Banco não foi apagado após restart' }

    $slash = [string][char]92

    $request = 'GET /health/ready HTTP/1.0' + $slash + 'r' + $slash + 'n' + $slash + 'r' + $slash + 'n'

    $bashCommand = 'exec 3<>/dev/tcp/127.0.0.1/8080; printf ''' + $request + ''' >&3; IFS= read -r -t 5 line <&3; printf ''%s'' "$line"'

    $readiness = docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml exec -T api bash -c $bashCommand

    if ($LASTEXITCODE -ne 0 -or $readiness -notmatch '503') { throw ('API did not return 503. Response: ' + $readiness) }

    docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml restart api

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao reiniciar API' }

    docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml up -d --wait api

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao recuperar API' }

    $after = docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml exec -T postgres psql -U backlog -d backlog -Atc 'SELECT id FROM users'

    if ($LASTEXITCODE -ne 0 -or !$after -or $after.Trim() -eq $before.Trim()) { throw 'Gestor não foi recriado' }

    Write-Output 'Perda de PGDATA em tmpfs, resposta 503 e recuperação após restart da API confirmadas.'
}
finally
{
    docker compose -p backlog-memory-check -f compose.yaml -f compose.memory.yaml down

    $env:API_PORT = $previousPort

    Pop-Location
}
