# Execu√ß√£o incremental

Cada etapa usa o ID de `tasks.md` no t√≠tulo do commit. Consulte `git log --oneline --grep='T-'` para rastrear o hist√≥rico. Valida√ß√µes amplas ser√£o registradas nas etapas de testes e na revis√£o T-38.

| ID | Entrega | Verifica√ß√£o |
| --- | --- | --- |
| T-01 | Solu√ß√£o .NET 10 com seis projetos e depend√™ncias por camada | Build da solu√ß√£o |
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
| T-15 | Minimal API, DI, JWT com sess„o viva, Problem Details e healthchecks | Build aprovado; integraÁ„o nas T-32 a T-35 |
| T-16 | DTOs de usu·rios e validaÁ„o assÌncrona FluentValidation | Build aprovado |
| T-17 | Rotas de autenticaÁ„o e usu·rios sem exposiÁ„o de hash | Build aprovado |
| T-18 | criar tarefas com materiais iniciais atomicamente | Build aprovado; verificaÁıes de comportamento nas T-30 a T-35 |
