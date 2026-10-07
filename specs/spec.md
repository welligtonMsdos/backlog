# Gestão de tarefas da Tesouraria

## Problema
Usuários da Tesouraria precisam passar demandas aos desenvolvedores e acompanhar a execução, homologação, implantação e conclusão. Falta um fluxo com responsáveis, transições válidas e histórico de datas, observações e arquivos por etapa. Cada pessoa deve acessar apenas suas tarefas.

## Objetivo
Permitir que a Tesouraria crie e acompanhe tarefas, que o desenvolvedor registre a execução e que o usuário homologue a entrega e acompanhe a conclusão. O sistema deve preservar o histórico de cada passagem por um status e impedir mudanças fora do fluxo autorizado.

## Usuários
- **Usuário da Tesouraria:** cria tarefas, vê apenas as que criou e o desenvolvedor encarregado, registra a homologação e acompanha a implantação e a conclusão.
- **Desenvolvedor:** vê apenas as tarefas encaminhadas a ele, assume o desenvolvimento, registra seu andamento e envia a entrega para homologação.
- **Gestor:** possui cadastro e autenticação com perfil `gestor`, cadastra os demais usuários e pode consultar todas as tarefas, seus detalhes, históricos e anexos, independentemente do criador ou desenvolvedor.

Os três perfis são usuários do sistema e precisam de cadastro individual e autenticação para acessá-lo. O criador e o desenvolvedor destinatário devem ser identificados na tarefa para aplicação das permissões também no servidor. Neste MVP, o produto entregue é uma Web API; um cliente web operacional fica para uma etapa futura. Menções a formulário, lista e ações visíveis descrevem os dados e as ações que a API oferece ao cliente.

## Histórias
- Como usuário da Tesouraria, desenvolvedor ou gestor, quero ter um cadastro vinculado ao meu perfil e entrar no sistema com minhas credenciais para acessar as funções autorizadas.
- Como gestor, quero visualizar todas as tarefas e seus históricos para acompanhar as demandas da Tesouraria e o trabalho dos desenvolvedores.
- Como gestor, quero cadastrar usuários com o perfil correto para que possam entrar no sistema e exercer suas funções.
- Como usuário da Tesouraria, quero criar uma tarefa com título, descrição, desenvolvedor destinatário, observação e arquivos para encaminhar a demanda.
- Como usuário da Tesouraria, quero consultar minhas tarefas, seus status, datas e desenvolvedor responsável para acompanhar o trabalho.
- Como desenvolvedor, quero ver as tarefas encaminhadas a mim e iniciar uma tarefa em `backlog` para assumir sua execução.
- Como desenvolvedor, quero registrar andamento e evidências e enviar a tarefa para `homologação` ao concluir o desenvolvimento.
- Como usuário da Tesouraria, quero registrar o resultado da homologação, devolver a tarefa para correção quando necessário ou encaminhá-la para implantação.
- Como usuário da Tesouraria, quero consultar o histórico de cada etapa, inclusive observações e arquivos, para conferir o que ocorreu.

## Requisitos funcionais
### Cadastro e autenticação de usuários

- Na primeira inicialização de uma base vazia, o sistema deve criar automaticamente um usuário gestor inicial, ativo, a partir de nome, e-mail e senha fornecidos na configuração segura do ambiente. A criação deve ser idempotente: reiniciar a API sem apagar o banco não deve duplicar nem sobrescrever esse usuário.
- Somente um gestor autenticado e ativo pode cadastrar os demais usuários, inclusive outro gestor. Não há cadastro público nem escolha de perfil por usuários não gestores.
- O sistema deve permitir o cadastro individual de usuários com identificador, nome, e-mail de acesso único, credencial de autenticação, perfil obrigatório (`tesouraria`, `desenvolvedor` ou `gestor`) e situação do cadastro (`ativo` ou `inativo`).
- O usuário deve se autenticar com suas credenciais antes de acessar o sistema. Após a autenticação, o sistema deve identificar seu cadastro e perfil e disponibilizar apenas as funções autorizadas. Deve haver opção de encerrar a sessão.
- Credenciais devem ser armazenadas de forma segura, sem exibir a senha em consultas ou respostas da API. Cadastros inativos e credenciais inválidas não devem permitir acesso.
- Um usuário não pode alterar o próprio perfil por meio das funções comuns do sistema. Alteração de perfil após o cadastro não faz parte deste MVP.

### Cadastro e consulta

- A requisição de criação deve aceitar título obrigatório, descrição obrigatória, desenvolvedor destinatário obrigatório, observação inicial e arquivos anexos. O sistema preenche criador e data de criação e define o status inicial como `backlog`.
- A tarefa deve guardar identificador, título, descrição, criador, desenvolvedor destinatário, desenvolvedor responsável atual, status atual, data de criação e data da última atualização. Antes do início do desenvolvimento, o destinatário é exibido como encarregado, com indicação de que ainda não iniciou. Ao iniciar, ele passa a ser também o responsável atual.
- A lista da Tesouraria deve conter apenas tarefas criadas pelo usuário autenticado. A lista do desenvolvedor deve conter apenas tarefas destinadas ou atribuídas a ele. A lista do gestor deve conter todas as tarefas. Filtros por status não podem ampliar o conjunto permitido para cada perfil.
- A resposta de detalhe deve trazer os dados da tarefa, o histórico cronológico completo das etapas, transições e anexos e as ações disponíveis para o papel e o status. A API deve validar novamente as mesmas regras quando uma ação for solicitada.

### Registro de cada status

- Cada passagem por um status deve ter: status, data e hora de início, data e hora de fim, observação, arquivos anexos, pessoa que iniciou e pessoa que encerrou a etapa. Observações e arquivos adicionados durante a etapa devem registrar autor e data de inclusão.
- O início é preenchido automaticamente na entrada do status e o fim, na saída. Enquanto a etapa estiver ativa, mostrar `em aberto` como fim. Datas devem ser armazenadas em UTC ou com fuso horário e exibidas no fuso do usuário.
- O campo de observação deve existir em todas as etapas, exibindo `sem observação` quando vazio. O campo de arquivos deve existir em todas as etapas, exibindo `nenhum arquivo` quando não houver anexos. Os arquivos ficam vinculados à passagem em que foram inseridos.
- Na criação, observação e arquivos iniciais pertencem a `backlog`. Na ação de iniciar, os materiais enviados pertencem à nova passagem de `desenvolvimento`. No envio para homologação, pertencem à passagem de `desenvolvimento` que está sendo encerrada. Na decisão da homologação, pertencem à passagem de `homologação` que está sendo encerrada. Na conclusão, pertencem à passagem terminal de `finalizado`, criada na mesma operação. Cada vínculo e mudança de status devem ser persistidos atomicamente.
- Cada retorno de `homologação` para `desenvolvimento` cria uma nova passagem por `desenvolvimento`; registros anteriores, inclusive suas datas, observações e arquivos, permanecem intactos.
- `Finalizado` é registrado como marco terminal, com início e fim iguais à data e hora da conclusão, além de observação e arquivos de encerramento.

### Campos e ações por etapa

| Status | Início | Fim | Observação e arquivos | Ação |
| --- | --- | --- | --- | --- |
| `backlog` | Criação | Início do desenvolvimento | Contexto e anexos iniciais da demanda | Desenvolvedor destinatário inicia o desenvolvimento |
| `desenvolvimento` | Início ou retomada pelo desenvolvedor | Envio para homologação | Andamento, solução, evidências e anexos do desenvolvedor | Desenvolvedor responsável envia para homologação |
| `homologação` | Envio pelo desenvolvedor | Devolução ou encaminhamento para implantação | Resultado dos testes, ressalvas e anexos do usuário | Usuário criador devolve para desenvolvimento ou encaminha para implantação |
| `em implantação` | Encaminhamento após homologação | Conclusão da implantação | Observações e evidências da implantação | Usuário criador finaliza |
| `finalizado` | Conclusão | Mesmo instante da conclusão | Observação e arquivos de encerramento | Nenhuma |

## Regras de negócio
- A criação do gestor inicial deve ocorrer apenas quando a base ainda não contém usuários. Seu perfil deve ser `gestor`; sua senha deve ser armazenada somente como hash e não pode existir uma credencial padrão gravada no código ou no repositório.
- O cadastro de usuários exige sessão válida de gestor. O gestor informa o perfil no cadastro; o sistema deve rejeitar perfis inválidos e e-mails já utilizados. Tesouraria e desenvolvedores não podem cadastrar usuários.
- Todas as operações com tarefas exigem uma sessão autenticada e um cadastro ativo. O perfil e a identidade usados na autorização devem vir da sessão validada no servidor.
- Apenas usuários cadastrados com perfil `desenvolvedor` podem ser selecionados como destinatários de tarefas. O gestor autenticado e ativo pode consultar todas as tarefas, mas não pode criá-las nem mudar seus status apenas por possuir esse perfil.
- A criação sempre define `backlog`. O usuário criador escolhe o desenvolvedor destinatário, que poderá visualizar a tarefa antes de iniciá-la. Abrir o detalhe não muda o status: o clique em **Iniciar desenvolvimento** atribui o responsável atual e muda para `desenvolvimento`.
- De `backlog`, a única transição é para `desenvolvimento`, executada pelo desenvolvedor destinatário.
- De `desenvolvimento`, a única transição é para `homologação`, executada pelo desenvolvedor responsável. Não pode voltar a `backlog` nem avançar diretamente para `em implantação` ou `finalizado`.
- De `homologação`, o usuário criador só pode devolver para `desenvolvimento` ou encaminhar para `em implantação`. A devolução exige observação com o motivo. Não pode voltar a `backlog` nem avançar diretamente para `finalizado`.
- De `em implantação`, a única transição é para `finalizado`, executada pelo usuário criador. Não pode voltar a qualquer etapa anterior.
- `Finalizado` é terminal: todas as ações de mudança de status ficam desabilitadas e a API rejeita qualquer transição posterior.
- Cada transição encerra a passagem atual e inicia a seguinte em uma única operação, com a mesma data e hora de transição. A operação registra quem a realizou.
- O criador, o desenvolvedor destinatário/responsável e qualquer gestor autenticado e ativo podem consultar a tarefa, seu histórico e seus anexos. A autorização de leitura, escrita e download deve ser verificada no servidor pelo usuário autenticado; a permissão de consulta do gestor não concede permissão de alteração.

## Casos de borda
- Se a configuração do gestor inicial estiver ausente ou incompleta em uma base vazia, a inicialização deve falhar com erro claro, sem criar uma conta com senha conhecida ou vazia.
- Reiniciar apenas a API não deve recriar o gestor inicial. No MVP atual, reiniciar o contêiner do PostgreSQL apaga a base em memória. Para recuperar o sistema, é obrigatório reiniciar a API após o banco ficar saudável; nessa inicialização, as migrations e o bootstrap recriam apenas o gestor inicial. Até a API ser reiniciada, as operações com tarefas devem falhar sem gravar dados parciais.
- Tentativa de cadastro por usuário não gestor ou sem autenticação deve ser recusada, inclusive por chamada direta à API.
- Cadastro com e-mail já utilizado, perfil ausente ou inválido deve ser recusado com mensagem apropriada.
- Requisições sem autenticação válida, com sessão encerrada ou de usuário inativo devem ser recusadas antes de consultar ou alterar tarefas.
- Dois cliques ou requisições simultâneas para iniciar a mesma tarefa não podem criar duas atribuições. A segunda operação recebe o status atualizado.
- Duas mudanças simultâneas devem ser condicionadas ao status vigente; somente uma pode ser aceita e a outra deve receber conflito para atualizar a tela.
- Tentativas de consultar, alterar ou baixar anexos de uma tarefa fora do acesso do usuário devem ser negadas, inclusive por URL direta ou chamada à API. O gestor pode consultar e baixar anexos de qualquer tarefa, mas não alterar a tarefa apenas por ser gestor.
- Múltiplas devoluções na homologação devem gerar ciclos separados de desenvolvimento e homologação, em ordem cronológica.
- Falha no envio de arquivo não pode criar anexo incompleto; se a transição depender desse registro, ela não deve aparecer como concluída.
- Devolução sem a observação exigida deve ser recusada com indicação do campo a preencher.
- Datas e horas do histórico não podem ser alteradas pelo formulário de tarefa.

## Fora de escopo
- Cliente web operacional; edição ou mudança de status de tarefas pelo gestor; edição, inativação, exclusão e mudança de perfil de usuários após o cadastro.
- Persistência durável no MVP atual. No MVP 2, substituir o armazenamento volátil por PostgreSQL persistente, preservando o modelo de dados e as regras de negócio.
- Reatribuição a outro desenvolvedor após a criação.
- Comentários em tempo real, notificações externas, prioridade, estimativas, prazo e relatórios gerenciais.
- Aprovação por vários usuários ou desenvolvimento compartilhado da mesma tarefa.

## Critérios de aceite
- Ao iniciar a aplicação com uma base vazia e configuração válida, existe exatamente um gestor inicial capaz de se autenticar. Reiniciar a API com a mesma base não duplica nem altera essa conta; iniciar uma base vazia sem configuração válida falha sem criar credenciais padrão.
- Após perder o banco em `tmpfs`, reiniciar a API com o PostgreSQL saudável reaplica migrations e recria somente o gestor inicial; operações feitas antes da recuperação não geram registros parciais.
- Um gestor autenticado cadastra usuários da Tesouraria, desenvolvedores e outros gestores. Usuários sem perfil `gestor` e pessoas não autenticadas recebem acesso negado ao tentar cadastrar usuários.
- É possível cadastrar usuários com cada um dos perfis `tesouraria`, `desenvolvedor` e `gestor`; cada cadastro possui identidade e e-mail de acesso únicos. Cadastro sem perfil válido ou com e-mail repetido é recusado.
- Cada perfil consegue se autenticar com credenciais válidas e encerrar a sessão. Credenciais inválidas, cadastro inativo e requisições sem sessão válida não dão acesso às tarefas.
- O gestor autenticado consegue listar e abrir todas as tarefas, inclusive as criadas por outros usuários e destinadas a outros desenvolvedores, e consultar seus históricos e anexos. Não consegue mudar status ou editar tarefas apenas por possuir perfil `gestor`.
- Uma tarefa válida criada pelo usuário aparece em sua lista como `backlog`, com destinatário, data de criação, observação e anexos iniciais. Outro usuário da Tesouraria não a vê.
- O desenvolvedor destinatário vê a tarefa e, ao acionar **Iniciar desenvolvimento**, torna-se responsável atual. O histórico fecha `backlog` e abre `desenvolvimento`. Outro desenvolvedor não a vê nem consegue iniciá-la.
- O desenvolvedor responsável só consegue enviar `desenvolvimento` para `homologação`. Tentativas de ir para qualquer outro status são recusadas também pela API.
- O usuário criador consegue registrar homologação e escolher somente `desenvolvimento` ou `em implantação`. Uma devolução mantém as passagens anteriores e cria outra de `desenvolvimento`. Uma tentativa de avançar diretamente para `finalizado` é recusada.
- Em `em implantação`, só é possível ir para `finalizado`. Após a conclusão, todas as opções de status ficam desabilitadas e a API rejeita mudanças.
- Toda passagem mostra início, fim ou `em aberto`, observação ou `sem observação`, arquivos ou `nenhum arquivo` e autores. Esses dados permanecem associados à passagem correta após várias homologações.
- Os anexos e observações enviados junto de cada transição aparecem exatamente na passagem definida para aquela ação: criação em `backlog`, início em `desenvolvimento`, envio para homologação em `desenvolvimento`, decisão em `homologação` e conclusão em `finalizado`.
- Listagem, detalhe, alterações e download de anexos respeitam o acesso do criador, do desenvolvedor destinatário/responsável e a consulta global do gestor, inclusive em chamadas diretas à API.
