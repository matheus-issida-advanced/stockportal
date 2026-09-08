# Portal Stock — protótipo

Plataforma Blazor Server no estilo CloudCockpit, para exibir aos clientes da
revenda **Stock** seus próprios dados (clientes, assinaturas e um dashboard),
seguindo a paleta de cores da Advanced.

## Como rodar

Requer .NET 8 SDK instalado.

```bash
cd StockPortal
dotnet run
```

Abra o endereço mostrado no terminal (geralmente `https://localhost:5001` ou similar).

Por padrão o projeto roda com **dados de exemplo (mock)**, sem precisar de
nenhuma credencial do CloudCockpit — é só rodar e navegar.

## Ligar com a API real do CloudCockpit

1. Rotacione o `client_secret` exposto anteriormente no Postman (se ainda não
   fez isso) e gere um novo, dedicado a esta integração.
2. Preencha `appsettings.json`:
   ```json
   "UseMockData": false,
   "CloudCockpit": {
     "ClientId": "SEU_CLIENT_ID",
     "ClientSecret": "SEU_CLIENT_SECRET",
     ...
   }
   ```
   (Em produção, mova `ClientId`/`ClientSecret` para o Key Vault ou variáveis
   de ambiente — nunca deixe em texto puro no appsettings.json versionado.)
3. Rode de novo. O `CloudCockpitApiService` assume o lugar do mock automaticamente.

## O que está incluído

- **Dashboard** (`/`) — total de clientes, assinaturas ativas, receita mensal
  estimada, alertas de renovação em 60 dias, e um gráfico simples de barras
  por tipo de oferta.
- **Clientes** (`/clientes`) — lista com busca por nome/CNPJ.
- **Detalhe do cliente** (`/clientes/{id}`) — dados cadastrais + assinaturas.
- **Assinaturas** (`/assinaturas`) — todas as assinaturas da Stock, com busca
  e filtro por status.

## O que ainda falta (propositalmente fora desta v1)

- **Autenticação** — combinado que ficaria de fora por enquanto.
- **Faturamento (Invoices)** — pausado até o suporte do CloudCockpit
  confirmar os valores aceitos pelo parâmetro `invoiceType`.
- **Histórico de pedidos (Orders)** — endpoint validado e funcionando, mas
  ainda não tem dados reais nos clientes testados; fica fácil de plugar
  depois seguindo o mesmo padrão de `GetSubscriptionsByCustomerAsync`.
- **Margens/preço de custo** (`ListPrice`, `ErpPrice`, `CustomMargin`) — os
  dados já vêm da API mas não são expostos no front-end até você decidir se a
  Stock deve vê-los.
