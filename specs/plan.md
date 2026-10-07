# Plano técnico

## Contexto

- Implementar a [spec.md](spec.md) como Web API Minimal em C# com .NET 10. Os perfis são `tesouraria`, `desenvolvedor` e `gestor`; todos precisam de cadastro ativo e autenticação. O MVP entrega apenas a API e a referência Scalar; não inclui cliente web operacional. As listas, dados de formulário e ações disponíveis serão contratos da API para um cliente futuro.
- A Tesouraria cria e homologa tarefas próprias; o desenvolvedor consulta tarefas destinadas a ele e executa as etapas de desenvolvimento; o gestor cadastra usuários e consulta todas as tarefas, inclusive histórico e anexos, sem permissão de edição ou mudança de status das tarefas.
- Fluxo permitido: `backlog → desenvolvimento → homologação → em implantação → finalizado`, com retorno de `homologação → desenvolvimento`. Toda outra transição deve ser recusada no domínio e na API.
- O MVP atual deve funcionar inteiramente em Docker Compose, com API, PostgreSQL volátil em memória e execução dos testes em contêineres. No MVP 2, o PostgreSQL terá armazenamento persistente sem mudar os contratos nem as regras de negócio. Não haverá serviços externos de autenticação, banco, armazenamento de arquivos ou interface de documentação em CDN. Imagens base e pacotes de terceiros necessários ao build devem estar disponíveis localmente ou em repositório interno para permitir build sem acesso à internet; em execução, os contêineres não dependem de rede externa.

## Arquitetura

### Projetos e dependências

| Projeto | Responsabilidade | Referências permitidas |
| --- | --- | --- |
| `Backlog.Domain` | Entidades, perfis, estados, regras de transição, invariantes e eventos/histórico de etapas | Nenhum outro projeto da solução |
| `Backlog.Application` | Casos de uso, serviços, interfaces de repositório, transações, relógio, armazenamento de anexos e emissão/validação de sessão | `Backlog.Domain` |
| `Backlog.Infrastructure` | EF Core com Npgsql/PostgreSQL, mapeamentos, migrations, repositórios, transações, persistência de anexos, hash de senha e implementação de JWT/sessões | `Backlog.Application`, `Backlog.Domain` |
| `Backlog.Api` | Minimal API, DTOs de entrada/saída, validators FluentValidation, filtros de validação, autenticação/autorização, OpenAPI e Scalar | `Backlog.Application`, `Backlog.Infrastructure` |
| `Backlog.UnitTests` | Testes do domínio, validators e serviços com interfaces simuladas | Projetos sob teste |
| `Backlog.IntegrationTests` | Testes HTTP reais da API com PostgreSQL no Compose | API e infraestrutura de teste |

- Os endpoints apenas recebem DTOs, executam validação, identificam o usuário autenticado, chamam serviços e traduzem resultados para HTTP. Não acessam `DbContext` nem implementam transições diretamente.
- Serviços de aplicação orquestram casos de uso e chamam interfaces de repositório. Repositórios implementam persistência sem decidir permissões ou transições. As entidades de domínio concentram invariantes e a tabela de transições. Interfaces pequenas e injeção de dependência preservam responsabilidade única e inversão de dependência (SOLID).
- Usar métodos curtos, nomes explícitos, classes por responsabilidade e organização vertical: deixar uma linha em branco entre declarações de classes, entre métodos e entre cada comando dentro dos métodos. Aplicar essa convenção em código e testes e conferi-la na revisão.

### Fluxo de requisição

1. JWT Bearer é validado; a API consulta o cadastro e a sessão ativa para confirmar perfil atual e situação `ativo`.
2. O endpoint valida o DTO com FluentValidation e entrega ao serviço de aplicação o identificador do usuário obtido da sessão, nunca do corpo da requisição.
3. O serviço verifica visibilidade/permissão, executa a operação de domínio e salva por repositório dentro de transação quando houver mais de uma alteração.
4. O endpoint responde com DTO sem hash de senha ou conteúdo de anexo embutido. Downloads usam endpoint próprio com nova checagem de autorização.

## Decisões

### Autenticação e autorização

- Emitir JWT assinado pela própria API após login. Validar assinatura, emissor, audiência e expiração. Configurar chave e credenciais do banco por variáveis/segredos do Compose, sem gravá-las no repositório. Senhas são guardadas apenas como hash com algoritmo adequado a senhas e salt individual.
- Manter uma sessão no banco por `jti` do token. Logout revoga a sessão; cada requisição protegida confirma sessão ativa e usuário ativo. Tokens curtos e expiração configurável; não registrar tokens em logs. A estrutura permite refletir uma futura mudança de perfil na requisição seguinte, embora editar perfis esteja fora deste MVP.
- Autorizar por identidade atual e perfil atual: Tesouraria lê/cria apenas tarefas próprias e decide homologação/implantação; desenvolvedor lê apenas tarefas destinadas/atribuídas a ele e inicia/envia desenvolvimento; gestor lê qualquer tarefa e seus anexos e pode cadastrar usuários, mas não escreve em tarefas.
- Após aplicar migrations, um serviço de inicialização deve verificar se a tabela de usuários está vazia. Nesse caso, cria exatamente um gestor inicial ativo usando nome, e-mail e senha recebidos por segredo/variável do Compose. Exigir configuração completa e senha válida, armazenar somente o hash e falhar na inicialização se faltar um campo. A operação deve ser idempotente e serializada com bloqueio no PostgreSQL e transação para evitar duas contas iniciais em partidas simultâneas. Com usuários já existentes, não alterar a conta inicial nem recriar sua senha.
- Expor `POST /users` somente para gestores autenticados e ativos, permitindo cadastro nos três perfis. Não oferecer rota de cadastro público nem alteração de perfil pelo próprio usuário. Edição, inativação e exclusão de usuários permanecem fora do MVP atual.

### Persistência, anexos e Docker

- No MVP atual, usar PostgreSQL real em um contêiner Docker, com `PGDATA` inteiro em `tmpfs` e sem volume persistente. Usar imagem PostgreSQL 17 com `PGDATA=/var/lib/postgresql/data` e montar `tmpfs` nesse caminho. `tmpfs` tem limite de tamanho configurado no Compose. Reiniciar o contêiner apaga usuários, sessões, tarefas e anexos. A recuperação exige aguardar o healthcheck do PostgreSQL e reiniciar a API; sua inicialização reaplica migrations e recria o gestor inicial. Até esse reinício, a API deve falhar sem efetuar gravações parciais.
- Persistir anexos em tabela PostgreSQL (`bytea`) junto com metadados, ligados à tarefa e à passagem pelo status. Limitar tamanho, quantidade e tipos permitidos; validar nome e tipo no servidor. Não usar sistema de arquivos persistente ou armazenamento externo.
- Usar EF Core com Npgsql, migrations versionadas e aplicação das migrations na inicialização após o healthcheck do PostgreSQL. O Compose deve conter serviços `api`, `postgres` e `tests`, rede interna, healthchecks, dependência por saúde e Dockerfile multi-stage com SDK 10 para build/teste e runtime ASP.NET 10 para API. Expor somente a porta da API ao host; PostgreSQL permanece na rede interna.
- Fornecer configuração local de exemplo sem segredos reais, `.dockerignore`, instruções para `docker compose up --build` e `docker compose run --rm tests`. O serviço de testes usa banco/schema isolado no mesmo PostgreSQL volátil e limpa o estado entre cenários.
- Preparar para o MVP 2 um perfil/configuração de Compose que substitua apenas o `tmpfs` do diretório `PGDATA` por volume persistente, mantendo PostgreSQL, migrations, repositórios, serviços, contratos da API e armazenamento dos anexos. Validar migração de dados e estratégia de backup antes de ativar essa configuração; o banco volátil do MVP atual não contém dados recuperáveis para migrar após ser parado.
- “Sem referência externa” significa ausência de serviços e recursos externos em tempo de execução. As bibliotecas solicitadas (`FluentValidation`, `Scalar.AspNetCore`, Npgsql, EF Core e pacotes de teste) ficam empacotadas na imagem; build integralmente offline exige cache/feed interno e imagens base previamente disponíveis.

### API e validação

- Gerar documento OpenAPI com suporte nativo do ASP.NET Core e servir Scalar localmente em `/scalar`, com recursos locais e fontes padrão desabilitadas para não depender de CDN. Documentar esquema Bearer, operações, respostas e erros. Restringir o acesso à referência conforme ambiente/configuração.
- Registrar `IValidator<T>` para cada DTO de entrada. Executar validação explicitamente em endpoint filter ou adaptador comum da Minimal API, retornando `400` com erros por campo. Validar formato, obrigatoriedade, comprimento, e-mail, perfil, identificadores, observações obrigatórias e limites de anexos. Validação de DTO não substitui autorização e regras de estado dos serviços/domínio.
- Padronizar respostas de erro com Problem Details: `400` entrada inválida, `401` sem autenticação válida, `403` sem permissão, `404` recurso não visível/inexistente, `409` conflito de versão ou transição, `413` anexo acima do limite.

## Modelo de dados

| Tabela | Campos principais e regras |
| --- | --- |
| `users` | `id`, `name`, `email` único, `password_hash`, `role` (`tesouraria`, `desenvolvedor`, `gestor`), `is_active`, `created_at`, `updated_at` |
| `auth_sessions` | `id`/`jti` único, `user_id`, `expires_at`, `revoked_at`, `created_at`; índice por usuário e expiração |
| `tasks` | `id`, `title`, `description`, `creator_id`, `target_developer_id`, `current_developer_id` opcional, `current_status`, `created_at`, `updated_at`, `version` para concorrência; chaves estrangeiras e índices por criador, destinatário, responsável e status |
| `task_stage_entries` | `id`, `task_id`, `sequence`, `status`, `started_at`, `ended_at` opcional, `started_by_user_id`, `ended_by_user_id` opcional; unicidade de `(task_id, sequence)` e no máximo uma etapa aberta por tarefa |
| `stage_notes` | `id`, `stage_entry_id`, `author_user_id`, `text`, `created_at`; observações preservadas por passagem |
| `stage_attachments` | `id`, `stage_entry_id`, `uploaded_by_user_id`, `original_name`, `content_type`, `size_bytes`, `content` (`bytea`), `created_at` |

- Guardar datas em UTC. Ao sair de um status, fechar a passagem vigente e abrir a próxima com o mesmo instante em uma transação. Na conclusão, criar `finalizado` com início e fim iguais. Retornos da homologação criam novas passagens, sem editar o histórico anterior.
- Vincular observações e anexos enviados na criação à passagem de `backlog`; em `start`, à nova passagem de `desenvolvimento`; em `send-to-review`, à passagem de `desenvolvimento` antes de fechá-la; em `review`, à passagem de `homologação` antes de fechá-la; em `finish`, à nova passagem de `finalizado`. Salvar vínculo, fechamento e abertura atomicamente e registrar autor e instante de inclusão.
- Uma atualização de status deve comparar `version`/estado esperado e incrementar a versão atomicamente. Requisições concorrentes perdedoras retornam `409`; não podem criar duas etapas abertas nem dois responsáveis.
- Aplicar restrições de nulidade, unicidade e chaves estrangeiras no banco. A consulta por perfil deve aplicar o filtro na query do repositório; consultas de anexo verificam o acesso à tarefa antes de retornar bytes.

## Contratos

| Método e rota | Perfil | Entrada/resultado |
| --- | --- | --- |
| `POST /auth/login` | Público | E-mail e senha; retorna JWT, expiração e perfil |
| `POST /auth/logout` | Autenticado | Revoga a sessão do token atual |
| `GET /users/me` | Autenticado | Identidade, perfil e situação do cadastro |
| `POST /users` | Gestor autenticado e ativo | Nome, e-mail, senha, perfil; retorna usuário sem hash |
| `GET /users/developers` | Tesouraria | Lista desenvolvedores ativos selecionáveis |
| `POST /tasks` | Tesouraria | Título, descrição, desenvolvedor destinatário, observação e anexos iniciais; retorna tarefa em `backlog` |
| `GET /tasks` | Tesouraria, desenvolvedor, gestor | Lista paginada e filtrada por status, com escopo aplicado no servidor |
| `GET /tasks/{id}` | Quem pode consultar | Detalhe, responsável e passagens com datas, notas e metadados dos anexos |
| `POST /tasks/{id}/start` | Desenvolvedor destinatário | `backlog → desenvolvimento`; versão esperada; observação/anexos opcionais vinculados à nova passagem de desenvolvimento |
| `POST /tasks/{id}/send-to-review` | Desenvolvedor responsável | `desenvolvimento → homologação`; versão esperada; observação/anexos opcionais vinculados à passagem de desenvolvimento encerrada |
| `POST /tasks/{id}/review` | Tesouraria criadora | Decisão `return-to-development` ou `send-to-deployment`, versão esperada e observação/anexos vinculados à homologação encerrada; motivo obrigatório no retorno |
| `POST /tasks/{id}/finish` | Tesouraria criadora | `em implantação → finalizado`; versão esperada; observação/anexos vinculados à nova passagem terminal |
| `POST /tasks/{id}/stages/{stageId}/notes` | Tesouraria criadora em `backlog`, `homologação` ou `em implantação`; desenvolvedor responsável em `desenvolvimento` | Acrescenta observação à passagem vigente |
| `POST /tasks/{id}/stages/{stageId}/attachments` | Mesmos autores da inclusão de observação | Upload multipart de arquivo para a passagem vigente; anexos de `finalizado` são recebidos na ação de conclusão |
| `GET /tasks/{id}/attachments/{attachmentId}` | Quem pode consultar | Download do arquivo após conferir vínculo à tarefa e permissão |

- DTOs de resposta devem apresentar datas em ISO 8601, `endedAt: null` para etapa aberta, lista vazia para notas/anexos ausentes e dados de destinatário/responsável sem expor credenciais. A interface pode mostrar `em aberto`, `sem observação` e `nenhum arquivo` a partir desses valores.
- Documentar matriz de transições nos contratos e devolver ações disponíveis no detalhe conforme papel e estado. O servidor valida novamente no momento da ação.

## Riscos

- **Volatilidade no MVP atual:** `tmpfs` apaga toda a base ao parar o contêiner; usuários cadastrados pelo gestor, tarefas e anexos somem. Apenas o gestor inicial é recriado pela configuração após reiniciar a API com o banco saudável. Documentar o reinício conjunto e verificar limites de RAM.
- **Gestor inicial:** ausência de segredos, senha fraca ou exposição da configuração pode comprometer o cadastro de todos os usuários. Não incluir credenciais reais no repositório, logs ou imagem e testar partida com configuração ausente, duplicada e válida.
- **Cadastro e perfis:** somente gestores podem acessar `POST /users`, inclusive para cadastrar outros gestores. Proteger a rota no servidor e cobrir tentativa de acesso anônimo, de Tesouraria e de desenvolvedor em testes.
- **MVP 2 persistente:** a troca de `tmpfs` por volume de PostgreSQL altera retenção de dados e demanda backup, controle de capacidade e migração; manter a camada de aplicação independente da modalidade de armazenamento.
- **JWT e logout:** um JWT autoassinado continua criptograficamente válido até expirar; a consulta à tabela de sessões e ao usuário em cada requisição é necessária para revogação e atualização imediata do perfil. Proteger a chave de assinatura.
- **Build sem rede externa:** Scalar, FluentValidation, Npgsql, EF Core e as imagens .NET/PostgreSQL são dependências de terceiros. Preparar cache/feed interno e imagens locais, fixar versões compatíveis com .NET 10 e testar build com rede externa indisponível. Scalar deve servir seus recursos sem CDN.
- **Concorrência e histórico:** transições e uploads simultâneos podem duplicar etapas ou associar anexos à passagem errada. Usar transação, versão esperada e testes concorrentes.
- **Testes:** unitários devem cobrir matriz completa de transições, permissão por papel/identidade, datas, devoluções, senha e validators. Integração deve usar API HTTP e PostgreSQL real em `tmpfs`, cobrindo criação idempotente do gestor inicial, reinício da API após perda do banco, cadastro exclusivo por gestor, JWT, logout, listagem por perfil, leitura global do gestor, vínculo de anexos em cada transição, upload/download, erros de validação e conflitos. Não substituir PostgreSQL por banco EF em memória, pois isso esconderia diferenças de persistência.
