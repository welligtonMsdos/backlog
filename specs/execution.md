# Execução incremental

Cada tarefa de `specs/tasks.md` foi realizada em um commit cujo título começa pelo respectivo ID. Confira com `git log --oneline --grep='T-'`.

| ID | Entrega | Verificação |
| --- | --- | --- |
| T-01 | Solução .NET 10 e projetos com responsabilidades separadas | Solução compilada |
| T-02 | Dependências fixadas, arquivos de lock e cache local | Restore e build offline |
| T-03 | Dockerfile multi-stage e configuração inicial de segredos locais | Build sem rede externa |
| T-04 | Compose para API, PostgreSQL e testes | `docker compose config --quiet` |
| T-05 | PostgreSQL em `tmpfs`, limitado e sem porta publicada | Reinício descartou o banco conforme especificado |
| T-06 | Entidades de usuário, sessão, tarefa e passagem | Build e testes de domínio |
| T-07 | Regras de transição, autoria e controle de versão | Matriz de transições unitária |
| T-08 | Interfaces de repositórios, relógio, senha, tokens e transação | Referências entre projetos revisadas |
| T-09 | Modelo PostgreSQL, restrições e migration inicial | Migration aplicada na integração |
| T-10 | Repositórios EF Core e operações transacionais | Integrações com PostgreSQL |
| T-11 | Hash de senha, JWT e relógio | Testes unitários e HTTP |
| T-12 | Cadastro inicial idempotente do gestor | Teste de inicialização concorrente |
| T-13 | Login, logout e sessão revogável | Testes HTTP de autenticação |
| T-14 | Cadastro de usuários exclusivo do gestor | Testes HTTP de autorização |
| T-15 | Minimal API, DI, autorização e Problem Details | Build e testes HTTP |
| T-16 | DTOs e validação FluentValidation | Testes de validação |
| T-17 | Rotas de autenticação e administração de usuários | Testes HTTP de autenticação e perfis |
| T-18 | Criação de tarefa e materiais iniciais atômicos | Testes de fluxo e rollback |
| T-19 | Listagem e detalhe com visibilidade por perfil | Testes HTTP de escopo |
| T-20 | Início do desenvolvimento com atribuição e versão | Testes HTTP e concorrência |
| T-21 | Envio para homologação | Testes HTTP do fluxo |
| T-22 | Aprovação para implantação ou devolução ao desenvolvimento | Testes HTTP do fluxo e ciclos repetidos |
| T-23 | Conclusão da implantação e status terminal | Testes HTTP do fluxo completo |
| T-24 | Observações, anexos e validação de acesso | Testes HTTP de notas e arquivos |
| T-25 | DTOs e validators de tarefa, transição e arquivo | Testes unitários e HTTP |
| T-26 | Rotas de criação, listagem e detalhe de tarefas | Testes HTTP/PostgreSQL |
| T-27 | Rotas de transição autenticadas | Testes HTTP/PostgreSQL |
| T-28 | Rotas de observação, upload e download | Testes HTTP de acesso e limites |
| T-29 | OpenAPI com Bearer e Scalar com recursos locais | Testes HTTP da especificação, schemas e recursos |
| T-30 | Testes de domínio: status, papéis, datas, histórico e concorrência | 134 testes unitários aprovados |
| T-31 | Testes unitários de serviços, JWT, senhas e validators | 134 testes unitários aprovados |
| T-32 | Suíte de integração com PostgreSQL em Docker | Build Docker sem rede; suíte executada no Compose |
| T-33 | Integração de autenticação, bootstrap e sessões | 19 testes de integração aprovados na suíte final |
| T-34 | Integração de escopo, tarefas, notas e anexos | 19 testes de integração aprovados na suíte final |
| T-35 | Fluxos completos, concorrência, rollback e Scalar | 19 testes de integração aprovados na suíte final |
| T-36 | Configuração persistente opcional para o MVP 2 | Reinício do PostgreSQL preservou os dados de prova |
| T-37 | README de operação, segredos, testes e migração | Comandos e instruções conferidos com Compose |
| T-38 | Revisão final de arquitetura, formatação e critérios de aceite | Build Release offline, testes, Scalar, readiness e ciclo de perda/recuperação aprovados |

## Validação final da T-38

- `dotnet build Backlog.sln --no-restore --nologo`: aprovado, zero avisos e zero erros.
- `dotnet format whitespace Backlog.sln --verify-no-changes --no-restore`: aprovado.
- `docker compose --profile test build --no-cache`: imagens de API e testes construídas usando cache local e sem rede durante o build.
- `docker compose --profile test run --rm tests`: 134 testes unitários e 19 testes de integração aprovados; nenhum ignorado.
- `docker compose up -d --build --wait api`: API e PostgreSQL iniciados com health checks saudáveis.
- Scalar, OpenAPI Bearer, schemas de DTO/Problem Details e carregamento de recursos locais verificados pela integração; `/health/ready` e `/scalar` também responderam `200` no host pela porta 8080.
- `tools/verify-memory.ps1`: PostgreSQL em `tmpfs` perdeu o schema após restart; readiness da API respondeu 503; restart da API reaplicou migrations e recriou o gestor inicial.
- `tools/verify-persistence.ps1` (T-36): configuração persistente opcional reteve os dados após restart do PostgreSQL.
- `.env` permanece ignorado pelo Git; o repositório registra apenas `.env.example`.