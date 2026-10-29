# Claude — Contexto do Projeto

## Leia primeiro

Leia e siga [AGENTS.md](AGENTS.md) antes de qualquer alteração. Ele é a fonte compartilhada da verdade para Claude, Copilot, Gemini e outras IAs. Atualize-o quando mudar uma decisão, regra ou prioridade; mantenha este arquivo como guia de navegação do Claude, sem copiar regras divergentes.

## O que é o projeto

Hacker.Toolbox é uma aplicação web ASP.NET Core MVC com Razor em .NET 10, com utilitários para desenvolvimento e marketing, encurtamento de URLs, autenticação ASP.NET Core Identity e administração por roles. SQLite é o provedor padrão; PostgreSQL está preparado em um overlay separado.

O agente desktop `Agent/RubinotTunnelAgent/` é um projeto WinForms/.NET 8 separado, voltado ao encaminhamento TCP. A solução raiz `HackerToolbox.slnx` agrupa o agente, o site, as migrations PostgreSQL e os testes; `Site/HackerToolbox.slnx` permanece disponível para o site e seus projetos associados.

## Shopee: estado atual e direção

- **Implementado:** painel `/Shopee`, protegido pela role `Admin`, que consulta produtos ativos da loja em modo somente leitura via Shopee Seller Open Platform v2.
- **Configuração pendente:** Partner ID/Key, Shop ID e Access Token são configurados pelo operador via User Secrets, variáveis de ambiente ou secret manager; não os peça no chat nem os grave em arquivos versionados.
- **Limite atual:** OAuth, persistência e renovação automática de tokens ainda não foram implementados. Consulte [docs/SHOPEE.md](docs/SHOPEE.md).
- **Roadmap, ainda não implementado:** dashboards de vendas e comissões, consultas e geração/acesso a links de afiliado e, depois, avaliação de pedidos, estoque, preços e indicadores.
- Seller Open Platform e Shopee Affiliate Open API são APIs distintas. Confirme documentação oficial, permissões e origem dos dados antes de implementar cada funcionalidade; não misture autenticação ou contratos.

## Redes sociais: direção futura

Instagram, Pinterest, WhatsApp e TikTok poderão ser avaliados para gestão de links, métricas e automações futuras. Analise cada plataforma separadamente e priorize APIs oficiais, permissões, escopos, aprovações e limites. Para WhatsApp, distinga a WhatsApp Business Platform/Cloud API do aplicativo pessoal. Não implemente scraping, contorno de limites ou ações externas sem consentimento e autorização explícita.

## Ordem de trabalho

1. Preserve alterações locais, dados, banco e infraestrutura existente; evite refatorações não relacionadas.
2. Conclua e valide a integração de leitura de produtos Shopee com configuração segura.
3. Confirme documentação e acesso da conta antes de selecionar Seller API ou Affiliate API para vendas, comissões e links.
4. Implemente integrações sociais uma plataforma por vez, conforme capacidades oficiais confirmadas.
5. Teste assinatura, autenticação, paginação, erros e autorização com credenciais/dados fictícios; não altere produtos ou publique/envie mensagens reais sem autorização explícita.

Consulte [AGENTS.md](AGENTS.md) para os padrões completos, restrições de segurança e comandos de build/teste.
