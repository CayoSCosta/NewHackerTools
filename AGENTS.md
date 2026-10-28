# AGENTS.md — Contexto Compartilhado do Projeto

> Este arquivo é a **fonte da verdade** para qualquer IA (Copilot, Claude, Gemini, GPT etc.) que trabalhe neste repositório.
>
> Leia estas diretrizes antes de alterar o projeto. Quando uma convenção, decisão arquitetural ou regra de negócio mudar, atualize este arquivo. Não crie instruções duplicadas ou divergentes.

## 1. Visão Geral

**Hacker.Toolbox** é uma suite de utilitários web construída em ASP.NET Core MVC, com ferramentas para desenvolvimento, marketing, encurtamento de URLs e funcionalidades futuras para a loja Shopee.

A aplicação utiliza ASP.NET Core Identity para autenticação e autorização baseada em funções (RBAC).

**Prioridade atual:** implementar a Shopee Open Platform v2 para consulta da loja vendedora e desenvolver dashboards funcionais usando dados reais, preservando a infraestrutura já validada.

## 2. Stack Tecnológica

- **Runtime:** .NET 10, C# e `async/await` como padrão.
- **Web:** ASP.NET Core MVC, Razor Views e controllers tradicionais. Razor Pages são utilizadas nas páginas scaffolded de Identity em `Areas/Identity`.
- **Dados:** SQLite (`Site/hackerbox.db`) como padrão atual.
- **PostgreSQL:** preparado por meio do Npgsql 10.0.3 e do overlay `compose.postgres.yaml`. Não é o padrão do `compose.yaml`. A seleção do provedor utiliza `Database:Provider` (`Sqlite` ou `Postgres`).
- **Entity Framework Core:** versão 10.
- **Autenticação:** ASP.NET Core Identity, `IdentityUser` e `IdentityRole`.
- **Front-end:** Tailwind CSS via CDN, paleta escura (`#050505`) e verde terminal (`#00ff66`), Font Awesome 6.4, jQuery e jQuery Validation.
- **Processamento de mídia:** FFmpeg e FFprobe para as ferramentas que necessitam desses recursos.
- **Integração externa prioritária:** Shopee Open Platform v2, por REST/HTTP com assinatura HMAC-SHA256.

### 2.1. Banco de dados e migrations

- Migrations SQLite: `Site/Migrations/`. São históricas e não devem ser editadas.
- Migrations PostgreSQL: `Migrations.Postgres/`, projeto separado.
- O startup não aplica migrations automaticamente.
- Não executar migrations destrutivas nem alterar o provedor ativo sem autorização explícita.
- Não excluir, substituir ou sobrescrever bancos, volumes ou backups sem autorização explícita.
- Consultar `docs/DATABASE.md` e `docs/DOCKER.md` antes de operações relacionadas ao banco ou à infraestrutura.

**Estado atual:** o SQLite original deve ser preservado até que uma eventual migração para PostgreSQL seja aprovada e concluída explicitamente. A existência da infraestrutura PostgreSQL não significa autorização para realizar a troca.

## 3. Estrutura do Projeto

A aplicação principal está em `Site/`.

- `Agent/RubinotTunnelAgent/`: agente desktop WinForms em .NET 8 para encaminhamento TCP; projeto separado da aplicação web.
- `Site/Controllers/`: controllers MVC, um por área funcional.
- `Site/Views/`: views organizadas por controller.
- `Site/Models/`: modelos de domínio.
- `Site/Data/`: `ApplicationDbContext`, configuração de provedores e fábricas de design time.
- `Site/Services/`: serviços de aplicação, incluindo funcionalidades administrativas.
- `Site/Areas/Identity/`: páginas de autenticação.
- `Site/Migrations/`: migrations históricas do SQLite.
- `Migrations.Postgres/`: projeto separado de migrations PostgreSQL.
- `docs/`: documentação de banco de dados, Docker e operações.
- `compose.yaml`: configuração Docker padrão.
- `compose.postgres.yaml`: configuração adicional para PostgreSQL.
- `HackerToolbox.slnx`: solução na raiz que agrupa o agente, o site, as migrations PostgreSQL e os testes. `Site/HackerToolbox.slnx` permanece disponível para build focado no site e seus projetos associados.

Antes de criar arquivos, verifique a estrutura e os padrões existentes. Não presuma que controllers, serviços ou views da Shopee já existem.

## 4. Prioridade Atual: Shopee Open Platform v2

A integração é com a **API de gestão da loja vendedora** da Shopee, não com a Affiliate Open API.

Documentação oficial consultada:

- [Product API — get_item_list](https://open.shopee.com/documents/v2/v2.product.get_item_list?version=2)
- [Product API — get_item_base_info](https://open.shopee.com/documents/v2/v2.product.get_item_base_info?version=2)
- [OpenAPI 2.0 Overview — signing](https://open.shopee.com/documents?module=87&type=2&id=58&version=2)

A documentação oficial é a referência para autenticação, assinatura, endpoints, campos, paginação, limites e formatos de resposta. Se algum detalhe não puder ser confirmado, registre a pendência e não invente uma implementação.

### 4.1. Objetivos funcionais

1. Manter um cliente HTTP tipado e testável em `Site/Services/ShopeeApiClient.cs`.
2. Disponibilizar o painel `/Shopee` integrado ao layout e protegido pela role `Admin`.
3. Implementar primeiro consultas reais de produtos em modo somente leitura.
4. Evoluir o painel para dashboards de vendas, comissões, links de afiliado e indicadores, escolhendo a API correta (Seller ou Affiliate) de acordo com os dados e a documentação oficial.
5. Avaliar pedidos, estoque e preços somente com endpoints oficialmente confirmados e requisito concreto.

**Estado atual:** consultas somente leitura a produtos ativos da loja vendedora pela Shopee Open Platform v2 estão implementadas em `/Shopee`. Os detalhes estão em [docs/SHOPEE.md](docs/SHOPEE.md). Para uso real ainda é necessário configurar Partner ID/Key, Shop ID e Access Token. O fluxo OAuth, armazenamento e renovação automática de tokens não foram implementados.

**Roadmap, ainda não implementado:** dashboards de vendas e comissões, links de afiliado e indicadores. Não presuma que esses dados venham da API de loja já integrada: avalie a Shopee Affiliate Open API para dados/links de afiliados e a Seller Open Platform para dados da loja, pedidos e catálogo. São APIs distintas, com contratos, autenticação e permissões próprios. Confirme documentação e acesso da conta antes de implementar cada recurso.

### 4.2. Autenticação e assinatura

- As chamadas v2 de loja usam `partner_id`, `timestamp`, `access_token`, `shop_id` e `sign`.
- A assinatura documentada é HMAC-SHA256 com `partner_key`; valide fórmula, ordem e codificação contra a documentação oficial e testes fictícios.
- Tokens reais devem ser emitidos/autorizados na Shopee e mantidos fora do repositório. Registre eventual ausência de automação de OAuth, armazenamento ou renovação de tokens como pendência.
- Não registre tokens, chaves, URLs de requisição com query sensível ou assinaturas nos logs.

### 4.3. Segurança e escopo

- Configure `Shopee:PartnerId`, `Shopee:PartnerKey`, `Shopee:ShopId` e `Shopee:AccessToken` por User Secrets em desenvolvimento ou variáveis de ambiente/secret manager em produção.
- Não grave credenciais em código, arquivos versionados ou `appsettings.json`.
- Não exponha tokens, chaves, URLs sensíveis ou respostas remotas em logs, HTML, erros ou respostas da aplicação.
- Configure timeout, leitura limitada de resposta, tratamento de erro e proteção contra excesso de chamadas.
- Comece com operações de leitura. Não altere produtos, preços, estoque, pedidos ou configurações da loja sem autorização explícita.
- Não implemente endpoints, parâmetros ou formatos sem confirmação na documentação oficial.

### 4.5. Roadmap: links e redes sociais

A aplicação poderá futuramente centralizar links, métricas e automações de redes sociais, incluindo Instagram, Pinterest, WhatsApp e TikTok, além de outras plataformas definidas pelo usuário. Isto é planejamento, não funcionalidade já implementada.

- Priorize APIs oficiais. Pesquise cada plataforma separadamente: recursos disponíveis, endpoints, autenticação, escopos, aprovações do app, limites e requisitos da conta podem diferir.
- Para métricas de vendas/comissões e links Shopee, confirme se a origem correta é a Shopee Affiliate Open API ou a Seller Open Platform; não misture dados nem autenticações dessas APIs.
- Planeje automações de publicação, agendamento e comunicação somente depois de confirmar capacidade oficial, consentimento e limites. WhatsApp deve usar a WhatsApp Business Platform/Cloud API quando aplicável; não presuma automação do aplicativo pessoal.
- Não implemente scraping, evasão de limites, publicação ou mensagens em massa não autorizadas. Não publique conteúdo, envie mensagens ou execute ações externas reais sem autorização explícita do usuário.
- Mantenha tokens fora do repositório e logs, associe as permissões à conta apropriada e valide respostas/erros sem expor dados sensíveis.

## 5. Convenções de Desenvolvimento

- Controllers e views seguem o padrão MVC existente.
- Evite misturar MVC com Razor Pages fora de `Areas/Identity`.
- Operações de I/O devem utilizar APIs assíncronas quando disponíveis.
- Utilize injeção de dependências e mantenha os serviços testáveis.
- Trate erros de entrada, falhas de rede, respostas HTTP inválidas e erros funcionais retornados pela API Shopee.
- Não presuma que um HTTP 200 significa que a operação da API foi bem-sucedida.
- Valide e trate paginação de acordo com o contrato oficial.
- Preserve os padrões visuais e de autorização existentes.
- Novos módulos devem integrar-se ao menu lateral em `_Layout.cshtml`, quando apropriado.
- Não adicione dependências sem necessidade comprovada.

### 5.1. EF Core

Ao adicionar tabelas ou campos:

- Crie uma migration nova para o provedor apropriado.
- Nunca edite migrations existentes já aplicadas.
- Verifique se a alteração precisa ser refletida nas migrations SQLite, PostgreSQL ou em ambas.
- Não aplique migrations automaticamente no startup.
- Não execute operações destrutivas no banco ativo sem autorização explícita.

Antes de persistir dados da Shopee, avalie se a funcionalidade realmente precisa de armazenamento local. Não crie tabelas por antecipação sem um requisito concreto.

## 6. Configuração e Segredos

- Configurações não sensíveis podem ficar nos arquivos de configuração apropriados.
- Utilize `dotnet user-secrets` para segredos no desenvolvimento local.
- No ambiente Docker PostgreSQL, as senhas do banco e do seed administrativo utilizam Compose secrets, conforme a configuração existente.
- Segredos reais nunca devem ser incluídos no repositório.
- Arquivos de secrets e backups devem continuar protegidos pelas regras de `.gitignore` e `.dockerignore`.
- Não imprima o conteúdo de arquivos de secrets durante diagnósticos.
- Não modifique permissões, credenciais ou volumes reais de produção sem autorização explícita.

Consulte `docs/DOCKER.md` e `docs/DATABASE.md` para os procedimentos já definidos.

## 7. Build, Execução e Testes

Build e execução local:

```powershell
dotnet build HackerToolbox.slnx
dotnet run --project Site/HackerToolbox.csproj
```

Execute os comandos a partir da raiz do repositório.

Ao concluir uma alteração:

1. Execute o build relevante.
2. Execute os testes relevantes.
3. Para a integração Shopee, utilize testes com credenciais fictícias e respostas simuladas.
4. Verifique o tratamento de falhas de autenticação, timeouts e erros da API Shopee.
5. Informe claramente quais testes passaram, falharam ou foram ignorados.
6. Não declare a integração funcional sem uma validação real ou evidência equivalente suficiente.

Testes que dependam de PostgreSQL ou de credenciais externas devem indicar claramente quando não foram executados.

## 8. Docker e Infraestrutura

A configuração Docker existente inclui:

- Dockerfile multi-stage baseado em .NET 10 e Ubuntu 24.04.
- Usuário não root.
- FFmpeg e FFprobe.
- `compose.yaml` com SQLite e porta vinculada a `127.0.0.1`.
- Volumes para dados e chaves do Data Protection.
- Endpoint de health check em `/healthz`.
- Cabeçalhos encaminhados configurados para proxies confiáveis.
- Overlay PostgreSQL separado em `compose.postgres.yaml`.

Consulte `docs/DOCKER.md` para os detalhes operacionais.

A infraestrutura já validada não deve ser objeto de novas auditorias gerais sem uma necessidade concreta.

**Não fazer sem autorização explícita:**

- Trocar o banco ativo para PostgreSQL.
- Criar ou substituir credenciais reais em volumes de produção.
- Realizar deploy em VPS ou outro ambiente.
- Excluir bancos, volumes, backups ou dados persistentes.
- Executar migrações destrutivas.

## 9. Regras para IAs de Código

1. Leia este arquivo antes de propor ou executar mudanças estruturais.
2. Inspecione apenas o contexto necessário para a tarefa atual.
3. Preserve todas as alterações locais existentes.
4. Não utilize comandos Git destrutivos, como `git reset`, `git clean` ou reversões indiscriminadas.
5. Não restaure funcionalidades removidas pelo usuário sem autorização.
6. Não repita auditorias gerais já concluídas sem uma razão técnica específica.
7. Não altere a infraestrutura quando a tarefa puder ser resolvida na aplicação.
8. Não crie arquivos de instrução paralelos com informações divergentes.
9. Atualize este arquivo quando uma decisão arquitetural ou regra de negócio mudar.
10. Ao finalizar, resuma os arquivos criados ou modificados, as decisões tomadas, os testes executados e as pendências reais.
11. Se faltar informação necessária, identifique a dúvida com precisão e evite inventar requisitos.
12. Não declare tarefas concluídas sem evidências verificáveis.

### 9.1. Uso eficiente do Copilot

Priorize entregas funcionais e evite consumo desnecessário de solicitações:

- Agrupe alterações relacionadas em etapas coesas.
- Evite prompts separados para auditorias redundantes, documentação repetida ou pequenas mudanças que possam ser feitas na mesma tarefa.
- Não refaça validações já concluídas sem uma mudança relevante.
- Priorize a primeira integração real com a Shopee e, em seguida, expanda as funcionalidades com base nos resultados.
- Evite refatorações cosméticas e melhorias de infraestrutura que não desbloqueiem a funcionalidade atual.

## 10. Critério de Conclusão

Uma funcionalidade só deve ser considerada concluída quando:

- Está implementada conforme a documentação e os padrões do projeto.
- Compila sem erros.
- Possui testes relevantes para os casos críticos.
- Trata falhas de forma segura.
- Não expõe credenciais ou dados sensíveis.
- Tem suas limitações e dependências externas documentadas.
- Preserva os dados e as alterações preexistentes.

**Diretriz principal:** preservar o que já funciona, implementar a Shopee Open Platform v2 com base na documentação oficial e priorizar resultados reais sem comprometer a segurança, os dados ou o orçamento de uso do Copilot.