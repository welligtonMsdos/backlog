# Execução incremental

Cada etapa usa o ID de `tasks.md` no título do commit. Consulte `git log --oneline --grep='T-'` para rastrear o histórico. Validações amplas serão registradas nas etapas de testes e na revisão T-38.

| ID | Entrega | Verificação |
| --- | --- | --- |
| T-01 | Solução .NET 10 com seis projetos e dependências por camada | Build da solução |
| T-02 | Dependencias fixadas, locks e preparacao do cache local offline | Restore/build; build Docker offline na T-38 |
