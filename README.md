# Backlog da Tesouraria

Web API Minimal em C# / .NET 10. Tesouraria cria e homologa suas tarefas; desenvolvedor trabalha nas tarefas destinadas a ele; gestor consulta todas e cadastra usuários.

## Preparação

Requisitos: Docker Desktop com contêineres Linux, PowerShell e SDK .NET 10.0.400 para preparar os pacotes locais. Após a preparação, build, API, banco e testes funcionam sem serviços externos.

```powershell
./tools/init-env.ps1
./tools/prepare-offline.ps1
docker compose build
docker compose up -d --wait api
```

O script cria `.env` local com valores aleatórios, sem sobrescrever um arquivo existente. Configure nome/e-mail do gestor nesse arquivo. A senha inicial está somente no arquivo local: não a envie ao Git. `.env`, resultados e feed local estão ignorados pelo Git e segredos estão excluídos da imagem.

Acesse http://localhost:8080/scalar. Se a porta estiver ocupada, altere `API_PORT` no `.env`. A referência é habilitada por `DOCUMENTATION_ENABLED`; desabilite-a quando necessário. Seus scripts são locais; fontes remotas e o agente Scalar estão desabilitados.

A rede Compose é interna. Apenas a API está exposta ao host, vinculada a 127.0.0.1. PostgreSQL não publica porta.

## Configuração

| Variável | Uso |
| --- | --- |
| `DB_PASSWORD` | Credencial local PostgreSQL |
| `JWT_KEY` | Chave de assinatura de pelo menos 32 bytes UTF-8 |
| `BOOTSTRAP_NAME` | Nome do gestor inicial |
| `BOOTSTRAP_EMAIL` | E-mail do gestor inicial |
| `BOOTSTRAP_PASSWORD` | Senha inicial, 12 a 128 caracteres |
| `API_PORT` | Porta HTTP local, padrão 8080 |
| `JWT_ACCESS_TOKEN_MINUTES` | Expiração de 1 a 60 minutos, padrão 15 |
| `Documentation__Enabled` | Habilita OpenAPI/Scalar |

Migrations são aplicadas na inicialização. Se a tabela de usuários estiver vazia, é criado exatamente um gestor ativo, protegido por transação e bloqueio PostgreSQL. Sem configuração completa, a API falha. Reiniciar com base existente não altera a conta nem sua senha.

Use HTTPS no ponto de publicação fora da máquina local. O Compose atual entrega HTTP apenas em loopback.

## Autenticação e usuários

1. Execute `POST /auth/login` com JSON `email` e `password` do gestor configurado.
2. Use `accessToken` no campo Bearer do Scalar ou no cabeçalho `Authorization: Bearer <token>`.
3. O gestor cria usuários com `POST /users`: `name`, `email`, `password`, `role` e `isActive` (padrão true).
4. Perfis válidos: `tesouraria`, `desenvolvedor`, `gestor`.
5. `GET /users/me` retorna a identidade atual. Tesouraria consulta desenvolvedores ativos em `GET /users/developers`.
6. `POST /auth/logout` revoga a sessão atual imediatamente.

A API verifica assinatura, emissor, audiência e expiração do JWT, além da sessão e do cadastro atual em toda requisição protegida. Senhas são armazenadas com hash PBKDF2 e salt individual. Não há cadastro público, atualização de perfis ou exclusão de usuários neste MVP.

## Tarefas e arquivos

Fluxo:

```text
backlog -> desenvolvimento -> homologação -> em implantação -> finalizado
                                  |
                                  +----> desenvolvimento (motivo obrigatório)
```

| Operação | Autor | Materiais enviados pertencem a |
| --- | --- | --- |
| `POST /tasks` | Tesouraria | Backlog |
| `POST /tasks/{id}/start` | Desenvolvedor destinatário | Novo desenvolvimento |
| `POST /tasks/{id}/send-to-review` | Desenvolvedor responsável | Desenvolvimento encerrado |
| `POST /tasks/{id}/review` | Tesouraria criadora | Homologação encerrada |
| `POST /tasks/{id}/finish` | Tesouraria criadora | Finalizado |

Criação e ações usam `multipart/form-data`. Criação recebe `title`, `description`, `targetDeveloperId`, `note` opcional e arquivos repetidos no campo `files`. Ações recebem `expectedVersion`, `note` e `files` opcionais; homologação também recebe `decision`: `return-to-development` ou `send-to-deployment`.

Consulte `GET /tasks?page=1&pageSize=20&status=backlog` e `GET /tasks/{id}`. O servidor aplica o escopo por identidade. O detalhe retorna versão, destinatário, responsável, ações disponíveis e histórico cronológico. Datas são UTC/ISO 8601; etapa aberta tem `endedAt: null`. Listagem não carrega histórico; consulte o detalhe para obter as passagens.

Observação avulsa: `POST /tasks/{id}/stages/{stageId}/notes`, JSON `expectedVersion` e `text`.

Upload avulso: `POST /tasks/{id}/stages/{stageId}/attachments`, multipart `expectedVersion` e `files`. Só a etapa vigente permite escrita: Tesouraria em backlog/homologação/implantação, desenvolvedor em desenvolvimento. Gestor consulta e baixa, sem editar tarefas. Finalizado é terminal; seus materiais são recebidos somente na ação de conclusão.

Download: `GET /tasks/{id}/attachments/{attachmentId}`. Metadados aparecem no detalhe; os bytes não são incluídos no JSON.

Limites: título 200, descrição 10.000 e observação 4.000 caracteres. Cinco arquivos por passagem, até 5 MiB por arquivo. PDF, PNG, JPEG, texto simples e CSV UTF-8 são permitidos; conteúdo e nomes são verificados no servidor. Arquivos ficam em `bytea` no PostgreSQL.

Atualizações incrementam a versão. Envie a versão do último detalhe; em `409`, consulte novamente antes de repetir. Mudança de status e materiais são salvos na mesma transação.

Erros usam Problem Details: 400 entrada inválida; 401 autenticação inválida; 403 sem permissão; 404 recurso inexistente/não visível; 409 versão/status incompatível; 413 tamanho excedido; 503 banco indisponível.

## Testes

```powershell
docker compose --profile test build tests
docker compose --profile test run --rm tests
```

Testes unitários podem ser executados no host:

```powershell
dotnet restore Backlog.sln --locked-mode
dotnet test tests/Backlog.UnitTests --no-restore
```

Integração roda com HTTP via WebApplicationFactory e PostgreSQL real no Compose. Cada teste cria um banco exclusivo e o remove ao terminar. Os testes não usam o banco operacional da API. Resultados TRX ficam em `artifacts/test-results`.

A suíte cobre matriz de estados/identidades, ciclos, datas, concorrência, JWT e revogação, bootstrap, perda de schema, cadastro, escopo, arquivos e rollback. A verificação final também testa restart do contêiner PostgreSQL volátil e a alternativa persistente.

## Banco em memória — MVP atual

O servidor é PostgreSQL 17 real. Todo `PGDATA` fica em `tmpfs` com limite de 256 MiB; o contêiner tem limite de 512 MiB. Parar/reiniciar PostgreSQL apaga usuários, sessões, tarefas e anexos. API restart isolado preserva os dados enquanto PostgreSQL permanece ativo.

Depois de perder o banco, aguarde PostgreSQL saudável e reinicie a API:

```powershell
docker compose up -d --wait postgres
docker compose restart api
docker compose up -d --wait api
```

Migrations e gestor inicial serão recriados. Tokens antigos perdem validade porque suas sessões foram apagadas. Até reiniciar a API, requisições dependentes do schema perdido retornam 503.

## PostgreSQL persistente — preparação MVP 2

A alternativa usa as mesmas regras e migrations, trocando apenas a montagem do PGDATA:

```powershell
docker compose -f compose.yaml -f compose.persistent.yaml up -d --build --wait api
```

Os `-f` explícitos evitam carregar `compose.override.yaml` (memória). Não alterne modos sobre contêineres ativos. Um projeto separado permite validar sem tocar a base atual:

```powershell
./tools/verify-persistence.ps1
```

O script usa o projeto isolado `backlog-persistence-check`, porta 18080 e volume exclusivo, verifica retenção após restart e remove somente os recursos desse teste.

Antes da ativação com dados reais, defina retenção/capacidade, rotina de `pg_dump`, cópia protegida do backup e teste de restauração com `pg_restore` em banco isolado. Para migrar o MVP volátil, exporte enquanto o PostgreSQL ainda estiver ativo e restaure no volume persistente antes de mudar a aplicação. Após parar o banco em memória, seus dados não são recuperáveis.

## Arquitetura e rastreabilidade

- Domain: entidades e regras, sem pacotes ou referências externas.
- Application: serviços e interfaces; depende somente do Domain.
- Infrastructure: PostgreSQL/EF Core, repositórios, transações, hash/JWT e bootstrap.
- Api: DTOs, FluentValidation, endpoints, autenticação e documentação.
- UnitTests / IntegrationTests: projetos separados.

Especificação, plano, tarefas e constituição estão em `specs/`. Evidências de execução: [specs/execution.md](specs/execution.md). Cada tarefa possui um commit `T-XX: descrição`:

```powershell
git log --reverse --format="%h %s" --grep="^T-"
```

Pacotes ficam travados em `packages.lock.json`. O script de preparação copia as versões exatas para `.offline/nuget`; os comandos de build do Compose usam `network: none`. Para reconstrução integral, mantenha esse feed e as imagens base localmente disponíveis.
