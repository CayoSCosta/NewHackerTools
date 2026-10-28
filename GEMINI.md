# Gemini — Contexto do Projeto

## Leia primeiro

Leia e siga [AGENTS.md](AGENTS.md) antes de qualquer alteração. Ele é a fonte compartilhada da verdade para Gemini, Copilot, Claude e outras IAs. Atualize-o quando mudar uma decisão, regra ou prioridade; mantenha este arquivo como guia de navegação do Gemini, sem copiar regras divergentes.

## O que é o projeto

Hacker.Toolbox é uma aplicação web ASP.NET Core MVC com Razor em .NET 10. Reúne utilitários para desenvolvimento e marketing, encurtamento de URLs, autenticação ASP.NET Core Identity e administração por roles. SQLite é o provedor padrão; PostgreSQL está preparado em um overlay separado. A infraestrutura existente já foi validada e não é o foco desta etapa.

## Outros projetos

- `Agent/RubinotTunnelAgent/` é um agente desktop separado, feito em WinForms/.NET 8, para encaminhamento TCP ao relay do projeto. Ele não faz parte da integração Shopee.
- A solução da raiz (`HackerToolbox.slnx`) agrupa o agente, o site, as migrations PostgreSQL e os testes; `Site/HackerToolbox.slnx` continua sendo a solução focada na aplicação web e seus projetos associados.

## Shopee: estado atual e direção

- **Implementado:** painel `/Shopee`, acessível somente a `Admin`, com consulta somente leitura de produtos ativos da loja vendedora por Shopee Open Platform v2. O cliente assina as chamadas e a tela mostra dados de produto quando a conta está configurada.
- **Configuração pendente:** Partner ID/Key, Shop ID e Access Token devem ser configurados pelo operador via User Secrets, variáveis de ambiente ou secret manager; nunca peça que sejam enviados no chat nem os grave em arquivos versionados.
- **Limite conhecido:** o token é configurado externamente; o fluxo OAuth, sua persistência e renovação ainda não foram implementados. Consulte [docs/SHOPEE.md](docs/SHOPEE.md).
- **Roadmap, ainda não implementado:** evoluir o painel para métricas de vendas e comissões, consultas e geração/acesso a links de afiliado e, depois, avaliar pedidos, estoque, preços e indicadores.
- **Não misture APIs:** Seller Open Platform v2 (a integração atual da loja) e Shopee Affiliate Open API (links/comissões de afiliado) têm produtos, permissões, autenticação e contratos distintos. Antes de programar cada recurso, confirme na documentação oficial qual API oferece os dados e quais permissões a conta possui.

## Redes sociais: direção futura

Avaliar integrações para Instagram, Pinterest, WhatsApp e TikTok, além de outras redes conforme necessidade. Possíveis funcionalidades futuras incluem gestão de links, métricas e automações de publicação ou comunicação, mas cada plataforma deve ser analisada separadamente quanto a APIs oficiais, permissões da conta, aprovação de aplicativo, escopos e limites. Não trate essas funcionalidades como existentes, nem prometa automações ainda não confirmadas.

Use APIs oficiais e respeite os termos, consentimento e limites de cada plataforma. Para WhatsApp, distinguir a WhatsApp Business Platform/Cloud API do aplicativo pessoal; não implementar disparos sem consentimento, scraping, contorno de limites ou automação não autorizada.

## Ordem de trabalho recomendada

1. Preservar alterações locais, dados, banco e infraestrutura existente.
2. Concluir e validar a integração atual de produtos Shopee com configuração segura da conta.
3. Confirmar documentação oficial e acesso da conta antes de escolher Seller API ou Affiliate API para cada dashboard de vendas, comissões e links.
4. Implementar integrações sociais uma plataforma por vez, começando por escopo e capacidades oficialmente disponíveis.
5. Testar autenticação, assinatura, paginação, erros e autorização com credenciais/dados fictícios; nunca executar ações de escrita na loja ou publicar/enviar mensagens reais sem autorização explícita.

Consulte [docs/SHOPEE.md](docs/SHOPEE.md) para detalhes atuais e [AGENTS.md](AGENTS.md) para os padrões, restrições e comandos de build/teste do projeto.
