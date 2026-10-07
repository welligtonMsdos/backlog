# Constituição do projeto

Este documento fixa as decisões que devem orientar a implementação da [spec.md](spec.md), do [plan.md](plan.md) e das tarefas em [tasks.md](tasks.md). O MVP atual entrega uma Web API para gestão de tarefas da Tesouraria; a persistência durável pertence ao MVP 2.

## Stack

- Linguagem e plataforma: C# com .NET 10 e ASP.NET Core Minimal API. O produto deste MVP é apenas a API HTTP; Scalar fornece a referência interativa dos endpoints. Cliente web operacional fica fora do MVP atual.
- Autenticação: JWT Bearer emitido pela API, com validação de assinatura, emissor, audiência e expiração. Sessões identificadas por `jti` são registradas no banco para permitir logout e revogação.
- Persistência: PostgreSQL 17 em Docker, acessado por EF Core e Npgsql, com migrations versionadas. No MVP atual, todo o diretório `PGDATA` fica em `tmpfs`, sem volume persistente; dados, sessões e anexos desaparecem quando o contêiner do banco para. No MVP 2, um volume persistente substitui o `tmpfs` sem alterar domínio, serviços ou contratos.
- Anexos: armazenados no PostgreSQL como `bytea`, com metadados e vínculo à passagem pelo status. Não usar armazenamento de arquivos externo.
- Validação: FluentValidation para DTOs de entrada, executado explicitamente nos endpoints/filtros da Minimal API. Regras de domínio e autorização são verificadas separadamente.
- Documentação da API: OpenAPI do ASP.NET Core e Scalar servido localmente em `/scalar`, com recursos locais e sem dependência de CDN ou fontes remotas.
- Execução: Docker Compose com serviços `api`, `postgres` e `tests`; Dockerfile multi-stage com SDK .NET 10 para build/testes e runtime ASP.NET 10 para a API. A API é a única porta publicada ao host.
- Dependências de terceiros devem ter versões fixadas e estar disponíveis por cache/feed interno e imagens base locais para build sem internet. Os contêineres em execução não dependem de serviços ou recursos externos.

## Arquitetura

- Separar responsabilidades nos projetos `Backlog.Domain`, `Backlog.Application`, `Backlog.Infrastructure`, `Backlog.Api`, `Backlog.UnitTests` e `Backlog.IntegrationTests`.
- `Domain` contém entidades, perfis, estados, invariantes e transições, sem depender de outros projetos. `Application` contém casos de uso, serviços e interfaces de repositórios/infraestrutura e depende apenas de `Domain`.
- `Infrastructure` implementa repositórios, transações, EF Core/Npgsql, migrations, hash de senha, JWT, sessões e armazenamento de anexos; depende de `Application` e `Domain`. `Api` contém endpoints mínimos, DTOs, validators, autenticação/autorização, mapeamento HTTP, OpenAPI e Scalar; depende de `Application` e `Infrastructure`.
- Endpoints não acessam `DbContext` e não implementam regras de transição. Recebem DTOs, validam, obtêm a identidade da sessão, chamam serviços de aplicação e retornam respostas HTTP. Serviços acessam dados exclusivamente por interfaces de repositório e coordenam transações. Repositórios não decidem permissões ou estados. Aplicar responsabilidade única, segregação de interfaces e inversão de dependência (SOLID).
- O domínio deve aceitar apenas `backlog → desenvolvimento → homologação → em implantação → finalizado` e `homologação → desenvolvimento`. Cada passagem registra início, fim, autores, observações e anexos; devoluções criam novas passagens, preservando o histórico. `Finalizado` é terminal.
- Alterações de status devem fechar a etapa atual e abrir a próxima atomicamente, com o mesmo instante UTC e verificação de versão para impedir gravações concorrentes. A passagem por `finalizado` tem início e fim no instante da conclusão. Observações e anexos da criação ficam em `backlog`; os de início em `desenvolvimento` novo; os de envio para homologação em `desenvolvimento` encerrado; os da decisão em `homologação` encerrada; os da conclusão em `finalizado`.
- O usuário da Tesouraria cria e acompanha somente suas tarefas e decide homologação/implantação; o desenvolvedor vê somente as tarefas destinadas/atribuídas a ele e conduz desenvolvimento; o gestor cadastra usuários e lê todas as tarefas, históricos e anexos, sem editar tarefas. Cada operação e download deve aplicar a permissão no servidor.
- A primeira inicialização de uma base vazia cria exatamente um gestor inicial a partir de configuração segura. A criação é idempotente e serializada no PostgreSQL. Somente gestor autenticado e ativo cadastra os demais usuários, inclusive outro gestor; não existe cadastro público.

## Qualidade

- Todos os DTOs de entrada têm validator FluentValidation para obrigatoriedade, formato, limites, perfil e identificadores. Validar tamanho, quantidade e tipo de anexos no servidor. Responder erros de campo como `400` e arquivos acima do limite como `413`.
- Padronizar erros HTTP com Problem Details: `401` para autenticação inválida/ausente, `403` para ação proibida, `404` para recurso inexistente ou não visível e `409` para estado/versão conflitante. Não expor hash, token, senha ou detalhes internos em respostas e logs.
- Armazenar senhas apenas como hash adequado a senhas com salt individual. Segredos de JWT, banco e gestor inicial entram por variáveis/segredos do Compose; não podem ser commitados, embutidos na imagem nem registrados em logs. Base vazia sem configuração válida do gestor deve impedir a inicialização.
- Consultar usuário ativo, perfil atual e sessão não revogada em cada requisição protegida. O gestor só tem leitura de tarefas; identidade e perfil nunca vêm do corpo de uma requisição para fins de autorização.
- Testes unitários devem cobrir matriz de transições, permissões, datas, histórico, devoluções, estado final, serviços, senha/JWT e validators. Testes de integração devem usar HTTP e PostgreSQL real em `tmpfs`, com dados isolados, cobrindo bootstrap, cadastro por gestor, login/logout, visibilidade por perfil, ciclo completo, anexos, erros e concorrência.
- O banco EF em memória não substitui PostgreSQL nos testes de integração. Build, testes e documentação Scalar devem funcionar sem rede externa após a preparação dos artefatos locais.
- A entrega só é considerada concluída quando todos os critérios de aceite da spec passam, as tarefas aplicáveis do MVP atual estão verificadas e `docker compose up --build` e `docker compose run --rm tests` funcionam conforme o README.

## Convenções

- Usar nomes de classes, métodos, tipos, propriedades e projetos claros e consistentes em C#; nomes de contratos JSON e rotas seguem um padrão único documentado no OpenAPI. Usar UTC internamente e datas ISO 8601 nas respostas.
- Manter uma linha em branco entre declarações de classes, entre métodos e entre cada comando dentro dos métodos, inclusive nos testes. Classes e métodos devem ter responsabilidade delimitada; evitar lógica de negócio em endpoints e repositórios.
- Um DTO representa o contrato HTTP, uma entidade representa o domínio e um repositório representa acesso aos dados. Não retornar entidades EF diretamente pela API. Arquivos são expostos por metadados no detalhe e por endpoint próprio de download.
- Organizar migrations no projeto de infraestrutura e registrá-las no controle de versão. Configuração local de exemplo deve usar valores não secretos e explicar as variáveis obrigatórias; credenciais reais ficam fora do repositório.
- Usar nomes de status e perfis definidos na spec, sem sinônimos adicionais no armazenamento ou na API. Toda mudança de status deve registrar autor, instante e versão esperada.

## Governança

- A [spec.md](spec.md) define comportamento, perfis, acesso e critérios de aceite; o [plan.md](plan.md) define as escolhas técnicas e contratos; a [tasks.md](tasks.md) ordena a execução e as verificações. Esta constituição contém restrições comuns a todos eles. Mudanças de fluxo, acesso ou stack devem atualizar os documentos afetados antes de serem implementadas.
- O escopo do MVP atual inclui API, JWT, gestor inicial, cadastro por gestor, fluxo de tarefas, anexos, PostgreSQL volátil em `tmpfs`, Docker, Scalar e testes. Após perda do banco volátil, a operação exige reiniciar a API quando o PostgreSQL estiver saudável para reaplicar migrations e bootstrap. A preparação da configuração persistente do MVP 2 está no plano; operar com retenção durável, backup e migração de dados pertence ao MVP 2.
- Edição/inativação/exclusão ou troca de perfil de usuários após cadastro, alteração de tarefas pelo gestor, reatribuição de desenvolvedor, notificações e relatórios não entram no MVP atual.
- Nenhuma tarefa é marcada como concluída apenas por compilar: aplicar a condição de conclusão indicada em `tasks.md` e verificar as permissões, o histórico e os efeitos no banco quando cabível.
- Novas dependências ou serviços externos exigem revisão da restrição de execução sem referências externas. Manter segredos fora do código e revisar exposição de rotas, arquivos e dados pessoais antes de finalizar a entrega.
