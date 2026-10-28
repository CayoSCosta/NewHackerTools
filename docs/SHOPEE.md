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
