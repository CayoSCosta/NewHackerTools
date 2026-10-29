# Integração Shopee Open Platform

O painel `/Shopee` consulta somente produtos ativos (`NORMAL`) pela API oficial v2. A consulta usa `v2.product.get_item_list` para paginação e `v2.product.get_item_base_info` para nome, SKU, preço e estoque disponível. Não há chamadas de escrita.

## Credenciais

Configure estes valores fora de `appsettings*.json` e do código:

| Configuração | Uso |
| --- | --- |
| `Shopee:PartnerId` | ID de parceiro fornecido no console Open Platform |
| `Shopee:PartnerKey` | Chave privada do parceiro |
| `Shopee:ShopId` | ID da loja autorizada |
| `Shopee:AccessToken` | Token de acesso emitido após autorizar a loja |

Em desenvolvimento, use User Secrets:

```powershell
dotnet user-secrets set "Shopee:PartnerId" "<partner-id>" --project Site
dotnet user-secrets set "Shopee:PartnerKey" "<partner-key>" --project Site
dotnet user-secrets set "Shopee:ShopId" "<shop-id>" --project Site
dotnet user-secrets set "Shopee:AccessToken" "<access-token>" --project Site
```

Em produção, forneça as chaves correspondentes por variáveis de ambiente (`Shopee__PartnerId`, `Shopee__PartnerKey`, `Shopee__ShopId` e `Shopee__AccessToken`) ou por um gerenciador de secrets. Não coloque valores reais em arquivos versionados. O token de acesso documentado tem validade de quatro horas; esta primeira entrega não automatiza autorização, persistência nem renovação do token. Ele precisa ser emitido e atualizado pelo fluxo autorizado da Shopee.

## Assinatura, consultas e limites

Cada requisição é assinada com HMAC-SHA256 sobre a concatenação sem separadores `partner_id + api_path + timestamp + access_token + shop_id`, usando `PartnerKey`. O endpoint de produtos ativos usa páginas de até 50 itens; esse tamanho também respeita o limite de 50 IDs de `get_item_base_info`. O cliente serializa `item_id_list` no formato de array documentado, limita as respostas a 2 MiB, aplica timeout de 20 segundos e espaça chamadas sequenciais em pelo menos um segundo. Respostas de erro, corpo remoto, query string e credenciais não são enviados à view ou aos logs.

Referências oficiais consultadas:

- [Product API — get_item_list](https://open.shopee.com/documents/v2/v2.product.get_item_list?version=2)
- [Product API — get_item_base_info](https://open.shopee.com/documents/v2/v2.product.get_item_base_info?version=2)
- [OpenAPI 2.0 Overview — signing](https://open.shopee.com/documents?module=87&type=2&id=58&version=2)

O catálogo e a autorização disponíveis para a aplicação dependem das permissões e da loja configuradas no console Shopee Open Platform. A API pode aplicar limites adicionais por app/loja; em caso de limitação, a tela orienta tentar novamente mais tarde.

## Próximos passos planejados (não implementados)

O painel poderá evoluir para dashboards de vendas e comissões, consulta/geração de links de afiliado e indicadores. Esses dados podem pertencer à **Shopee Affiliate Open API**, distinta da API de gestão da loja usada pela consulta de produtos atual. Antes de desenvolver cada recurso, confirmar na documentação oficial vigente os endpoints, autenticação, campos, limites e permissões concedidas à conta.

Pedidos, estoque e preços da loja também são ideias futuras; qualquer implementação deve começar em modo de leitura e permanecer restrita a operações oficiais confirmadas. A presença de uma área planejada no dashboard não significa que a operação já esteja integrada.

Integrações futuras com Instagram, Pinterest, WhatsApp, TikTok ou outras redes devem ser avaliadas individualmente com as APIs oficiais, escopos e aprovações necessários. Automação de publicação ou comunicação só deve ser implementada quando permitida pela plataforma, respeitando consentimento e limites; não automatizar o WhatsApp pessoal nem contornar mecanismos das plataformas.


## Affiliate Open API (em andamento)

Endpoint: `https://open-api.affiliate.shopee.com.br/graphql` (POST, JSON). Rota administrativa: `/ShopeeAffiliate` (role `Admin`).

**Confirmado na documentação oficial (Authentication):** cabeçalho `Authorization: SHA256 Credential={AppId}, Timestamp={unix}, Signature={hex minúsculo}`, com `Signature = SHA256(AppId + Timestamp + Payload + Secret)`, onde `Payload` é o corpo JSON exato (`{"query":"..."}`). Tolerância de relógio: 10 minutos. O vetor oficial (AppId=123456, Secret=demo, Timestamp=1577836800) é validado nos testes.

**Configuração (separada da Seller API):**

```powershell
dotnet user-secrets set "ShopeeAffiliate:AppId" "<APP_ID>" --project Site/HackerToolbox.csproj
dotnet user-secrets set "ShopeeAffiliate:AppSecret" "<APP_SECRET>" --project Site/HackerToolbox.csproj
```

Em produção: variáveis `ShopeeAffiliate__AppId` e `ShopeeAffiliate__AppSecret`.

**Implementado:** assinatura, cliente tipado (`ShopeeAffiliateApiClient`), botão "Testar autenticação" (query `shopeeOfferV2` com `limit: 1`, somente leitura; `brandOffer`/offerV1 foi descontinuada pela Shopee, erro 10010), validador de domínios e geração de link curto (`generateShortLink`).

### Geração de link (`generateShortLink`)

Mutation usada (confirmada no manual `docs/shopee manual/Get Short Link.txt`: `originUrl` String! obrigatório, `subIds` [String] opcional com até 5, retorno `shortLink` String!; o tipo do input não é nomeado no manual):

```graphql
mutation { generateShortLink(input: {originUrl: "...", subIds: ["whatsapp_grupo"]}) { shortLink } }
```

- URL: somente `https` em `shopee.com.br`, `www.shopee.com.br` e `s.shopee.com.br`; sem credenciais na URL.
- SubIds: opcionais, até 5, separados por vírgula na tela; cada um com 1–50 caracteres `A-Z a-z 0-9 _ -` (limites locais conservadores, não confirmados oficialmente). Sugestão de origem: `whatsapp_grupo`.
- Os valores são validados e emitidos como literais de string JSON-escapados, não por variáveis GraphQL, porque o nome do tipo de entrada não está confirmado. O corpo enviado é exatamente o assinado.
- A resposta deve ter `data.generateShortLink.shortLink` como URL `https` válida; caso contrário, é tratada como resposta inválida.
- Como testar: `/ShopeeAffiliate`, colar o link do produto e clicar em "Gerar link de afiliado".

**Confirmado (Request and Response):** POST JSON em endpoint único, corpo `{"query","operationName"?,"variables"?}`, resposta HTTP 200 com `data`/`errors`. Códigos: 10000 erro de sistema, 10010 erro de parsing da requisição, 10020 autenticação (assinatura incorreta ou expirada), 10030 limite de tráfego, 11000 erro de negócio (`extensions.code`/`extensions.message`).`n`n**Ainda não confirmado oficialmente:** nome/tipo do input para variáveis, limites reais de SubIds, domínios de links curtos aceitos como `originUrl`, limites numéricos de requisição (apenas o código 10030 é documentado). A suíte usa apenas respostas simuladas; a primeira chamada real ainda precisa ser feita.

**Pendente (não confirmado):** operação GraphQL de geração de link (nome, argumentos, SubIds, resposta), códigos de erro além de `10020` (Invalid Authorization Header, observado no endpoint) e limites de requisição. A geração de link só será implementada após a documentação dessa operação ser confirmada.
