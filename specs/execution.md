# ExecuÃ§Ã£o incremental

Cada etapa usa o ID de `tasks.md` no tÃ­tulo do commit. Consulte `git log --oneline --grep='T-'` para rastrear o histÃ³rico. ValidaÃ§Ãµes amplas serÃ£o registradas nas etapas de testes e na revisÃ£o T-38.

| ID | Entrega | VerificaÃ§Ã£o |
| --- | --- | --- |
| T-01 | SoluÃ§Ã£o .NET 10 com seis projetos e dependÃªncias por camada | Build da soluÃ§Ã£o |
| T-02 | Dependencias fixadas, locks e preparacao do cache local offline | Restore/build; build Docker offline na T-38 |
| T-03 | Dockerfile multi-stage, exemplo de configuracao e geracao segura de segredos | Segredos aleatorios locais; build Docker na T-38 |
| T-04 | Compose com API, PostgreSQL, testes e rede interna | Compose config; healthchecks na T-38 |
| T-05 | PostgreSQL 17 em tmpfs limitado e sem porta no host | PostgreSQL iniciado; volatilidade na T-38 |
| T-06 | modela usuarios sessoes tarefas e historico no dominio | Build dominio; testes T-30 |
| T-07 | aplica matriz de transicoes autoria e versionamento | Build dominio; testes T-30 |
| T-08 | Interfaces de repositorios, transacoes, relogio, hash e tokens | Build Application sem referencias de infraestrutura |
| T-09 | Modelo PostgreSQL com seis tabelas, indices, restricoes e migration inicial | Migration gerada e compilada; integridade verificada T-35 |
| T-10 | Repositorios EF com filtros, bloqueio e transacoes atomicas | Build infraestrutura; testes PostgreSQL T-34/T-35 |
| T-11 | implementa hash de senhas JWT e relogio | Build; testes de seguranca T-31/T-33 |
| T-12 | cria gestor inicial idempotente apos migrations | Build; testes de seguranca T-31/T-33 |
| T-13 | adiciona login logout e validacao de sessao ativa | Build; testes de seguranca T-31/T-33 |
| T-14 | restringe cadastro de usuarios ao gestor | Build; testes de seguranca T-31/T-33 |
| T-15 | Minimal API, DI, JWT com sessão viva, Problem Details e healthchecks | Build aprovado; integração nas T-32 a T-35 |
| T-16 | DTOs de usuários e validação assíncrona FluentValidation | Build aprovado |
| T-17 | Rotas de autenticação e usuários sem exposição de hash | Build aprovado |
| T-18 | criar tarefas com materiais iniciais atomicamente | Build aprovado; verificações de comportamento nas T-30 a T-35 |
| T-19 | consultar tarefas por perfil e retornar responsáveis | Build aprovado; verificações de comportamento nas T-30 a T-35 |
| T-20 | iniciar desenvolvimento com versão e bloqueio transacional | Build aprovado; verificações de comportamento nas T-30 a T-35 |
| T-21 | enviar desenvolvimento para homologação | Build aprovado; verificações de comportamento nas T-30 a T-35 |
| T-22 | homologar ou devolver preservando passagens | Build aprovado; verificações de comportamento nas T-30 a T-35 |
| T-23 | finalizar implantação com passagem terminal | Build aprovado; verificações de comportamento nas T-30 a T-35 |
| T-24 | gerenciar observações e anexos com acesso e limites | Build aprovado; verificações de comportamento nas T-30 a T-35 |
| T-25 | validar tarefas, transições e uploads | Build aprovado; integração nas T-34 e T-35 |
| T-26 | expor criação, lista e detalhes de tarefas | Build aprovado; integração nas T-34 e T-35 |
| T-27 | expor ações de transição autenticadas | Build aprovado; integração nas T-34 e T-35 |
| T-28 | expor observações e upload e download autorizado | Build aprovado; integração nas T-34 e T-35 |
| T-29 | OpenAPI Bearer e Scalar local configurável, fontes e agente externos desabilitados | Build aprovado; recursos locais verificados na integração |
| T-30 | Matriz completa: 25 pares de status × 5 identidades, ciclos, datas, estado terminal e versão | Testes unitários aprovados |
| T-31 | Testes de serviços com repositórios simulados, senha, JWT, validators e arquivos | Testes unitários aprovados |
