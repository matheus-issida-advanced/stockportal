using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StockPortal.Models;

namespace StockPortal.Services;

// DTOs internos que espelham o shape retornado pela API do CloudCockpit.
// Não expor estes diretamente para o front-end.

internal class CustomerAssociationRaw
{
    public string? ResellerId { get; set; }
    public string Name { get; set; } = "";
}

internal class CustomerRaw
{
    public string Id { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? TaxId { get; set; }
    public string? InternalIdentifier { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public List<CustomerAssociationRaw> CustomerAssociations { get; set; } = new();
}

internal class ProviderResellerRaw
{
    public string ProviderInstanceId { get; set; } = "";
    public string ProviderResellerId { get; set; } = "";
}

internal class ResellerRaw
{
    public string Id { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? TaxId { get; set; }
    public string? InternalIdentifier { get; set; }
    public List<ProviderResellerRaw> ProviderResellers { get; set; } = new();
}

internal class SubscriptionRaw
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string? ResellerId { get; set; }
    public string? ProviderInstanceId { get; set; }
    public string SubscriptionName { get; set; } = "";
    public NamedValue OfferType { get; set; } = new();
    public NamedValue SubscriptionStatus { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Quantity { get; set; }
    public int? AssignedLicenses { get; set; }
    public Money RevenuePrice { get; set; } = new();
    public NamedValue BillingFrequency { get; set; } = new();
    public NamedValue TermDuration { get; set; } = new();
    public NamedValue AutoRenew { get; set; } = new();
    public string? InternalId { get; set; }
}

internal class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasNextPage { get; set; }
}

internal class InvoiceLineItemsFileRaw
{
    public string Status { get; set; } = "";
    public string? Location { get; set; }
}

internal class InvoiceRaw
{
    public string Id { get; set; } = "";
    public string ProviderInstanceId { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public decimal Subtotal { get; set; }
    public InvoiceLineItemsFileRaw? InvoiceLineItemsFile { get; set; }
}

/// <summary>
/// Uma linha do CSV de "line items" de uma fatura (Invoices/{id}/lineitems-file).
/// Essa é a fonte real de faturamento por cliente/revendedor no canal Microsoft -
/// Subscriptions.RevenuePrice não é confiável (vem zerado para consumo Azure).
/// </summary>
internal class InvoiceLineItemRaw
{
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerInternalId { get; set; } = "";
    public string? ResellerId { get; set; }
    public string ResellerName { get; set; } = "";
    public string ResellerInternalId { get; set; } = "";
    public string SubscriptionId { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductType { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public decimal SubtotalForReseller { get; set; }
    public decimal TotalForReseller { get; set; }
    public decimal SubtotalForCustomer { get; set; }
    public decimal TotalForCustomer { get; set; }
    public string BillingFrequency { get; set; } = "";

    // Data da fatura (invoice) de onde essa linha veio. Não vem no CSV em si -
    // é preenchida a partir dos metadados do invoice no momento da leitura.
    public DateTime InvoiceDate { get; set; }
}

internal class TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

public class CloudCockpitApiService : IStockDataProvider
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<CloudCockpitApiService> _logger;
    private readonly string _stockResellerId;
    // Provider Instance próprio da Stock (canal Generic/license). É esse valor,
    // não CNPJ ou ID de cliente, que define quem é "revendedor da Stock":
    // um registro em /v1/resellers cujo ProviderResellers contém esse id.
    private readonly string _stockProviderInstanceId;

    // Provider Instance do canal Microsoft (CSP). É nesse canal que o faturamento
    // real (Azure consumo + licenças NCE) vive - mas não em Subscriptions.RevenuePrice,
    // que vem zerado para consumo. A fonte correta são os CSVs de line items das
    // faturas tipo "onetime" desse provider instance. Ver FetchAllInvoiceLineItemsAsync.
    private readonly string _stockMicrosoftProviderInstanceId;

    private static string? _cachedToken;
    private static DateTime _tokenExpiry;

    // Os campos de cache são estáticos de propósito: o CloudCockpitApiService é
    // registrado via AddHttpClient<T>, que cria uma instância NOVA a cada
    // resolução (inclusive uma para a pré-renderização do Blazor Server e outra
    // para o circuito interativo que assume em seguida). Sem isso, cada
    // instância nova começa com cache vazio e refaz todas as chamadas de API -
    // dobrando (ou mais) o tempo de carregamento a cada visita de página.
    private static List<CustomerRaw>? _customersCache;
    private static List<SubscriptionRaw>? _subscriptionsCache;
    private static List<ResellerRaw>? _resellersCache;
    private static DateTime _cacheExpiry;
    private static TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static readonly SemaphoreSlim LoadLock = new(1, 1);

    public CloudCockpitApiService(HttpClient http, IConfiguration config, ILogger<CloudCockpitApiService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _stockResellerId = config["CloudCockpit:StockResellerId"] ?? "";
        _stockProviderInstanceId = config["CloudCockpit:StockProviderInstanceId"] ?? "";
        _stockMicrosoftProviderInstanceId = config["CloudCockpit:StockMicrosoftProviderInstanceId"] ?? "";

        var cacheDurationSeconds = config.GetValue<int?>("CloudCockpit:CacheDurationSeconds");
        if (cacheDurationSeconds is > 0)
            CacheDuration = TimeSpan.FromSeconds(cacheDurationSeconds.Value);
    }

    /// <summary>
    /// Retorna somente os clientes finais DIRETOS da Stock.
    /// Empresas que são clientes da Stock e também aparecem como Reseller são
    /// classificadas como revendedores e, portanto, não entram nesta lista.
    /// </summary>
    public async Task<List<CustomerDto>> GetCustomersAsync()
    {
        await EnsureDataLoadedAsync();

        var directCustomerIds = GetDirectStockCustomerIds();

        var activeSubscriptionsCount = _subscriptionsCache!
            .Where(s => directCustomerIds.Contains(s.CustomerId))
            .GroupBy(s => s.CustomerId)
            .ToDictionary(
                g => g.Key,
                g => g.Count(s => IsActive(s.SubscriptionStatus.Name)),
                StringComparer.OrdinalIgnoreCase);

        return _customersCache!
            .Where(c => directCustomerIds.Contains(c.Id))
            .Select(c => MapCustomer(c, activeSubscriptionsCount.GetValueOrDefault(c.Id, 0)))
            .OrderBy(c => c.CompanyName)
            .ToList();
    }

    /// <summary>
    /// Mantém a página de Assinaturas focada nas assinaturas diretas da Stock.
    /// O dashboard usa também as assinaturas dos revendedores internamente.
    ///
    /// IMPORTANTE: filtra por CustomerId pertencer ao conjunto de clientes
    /// diretos da Stock (via CustomerAssociations) - NÃO por
    /// Subscription.ResellerId. Já confirmamos com dados reais que esse campo
    /// pode vir inconsistente (ex: uma subscription da Latitude, cliente real
    /// da Stock, aparecia com ResellerId da Digytec em /v1/Subscriptions,
    /// enquanto a fatura real da Microsoft mostrava Stock). Filtrar por
    /// CustomerId evita perder assinaturas por causa dessa inconsistência.
    /// </summary>
    public async Task<List<SubscriptionDto>> GetSubscriptionsAsync()
    {
        await EnsureDataLoadedAsync();

        var directCustomerIds = GetDirectStockCustomerIds();

        var customerNames = _customersCache!
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CompanyName, StringComparer.OrdinalIgnoreCase);

        return _subscriptionsCache!
            .Where(s => directCustomerIds.Contains(s.CustomerId))
            .Select(s => MapSubscription(s, customerNames.GetValueOrDefault(s.CustomerId, "(desconhecido)")))
            .OrderByDescending(s => s.StartDate)
            .ToList();
    }

    public async Task<List<SubscriptionDto>> GetSubscriptionsByCustomerAsync(string customerId)
    {
        var all = await GetSubscriptionsAsync();
        return all.Where(s => IdEquals(s.CustomerId, customerId)).ToList();
    }

    /// <summary>
    /// Parceiros (revendedores) reais da Stock: registros em /v1/resellers cujo
    /// ProviderResellers contém o Provider Instance próprio da Stock (canal
    /// License/Generic).
    /// </summary>
    public async Task<List<PartnerDto>> GetPartnersAsync()
    {
        await EnsureDataLoadedAsync();

        var stockChannelResellers = _resellersCache!
            .Where(r => r.ProviderResellers.Any(pr => IdEquals(pr.ProviderInstanceId, _stockProviderInstanceId)))
            .ToList();
        var stockResellerIds = stockChannelResellers
            .Select(r => r.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var endCustomerCounts = _customersCache!
            .SelectMany(c => c.CustomerAssociations
                .Where(a => stockResellerIds.Contains(a.ResellerId ?? ""))
                .Select(a => new { ResellerId = a.ResellerId!, CustomerId = c.Id }))
            .GroupBy(x => x.ResellerId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.CustomerId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                StringComparer.OrdinalIgnoreCase);

        var activeSubscriptionCounts = _subscriptionsCache!
            .Where(s => stockResellerIds.Contains(s.ResellerId ?? "")
                     && IdEquals(s.ProviderInstanceId, _stockProviderInstanceId)
                     && IsActive(s.SubscriptionStatus.Name))
            .GroupBy(s => s.ResellerId ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return stockChannelResellers
            .Select(r => new PartnerDto
            {
                Id = r.Id,
                CompanyName = r.CompanyName.Trim(),
                TaxId = (r.TaxId ?? r.InternalIdentifier ?? "").Trim(),
                EndCustomerCount = endCustomerCounts.GetValueOrDefault(r.Id, 0),
                ActiveSubscriptionsCount = activeSubscriptionCounts.GetValueOrDefault(r.Id, 0)
            })
            .OrderBy(p => p.CompanyName)
            .ToList();
    }

    /// <summary>
    /// Clientes finais atendidos por um parceiro específico (associação via
    /// CustomerAssociations com o Id do parceiro).
    ///
    /// A contagem de assinaturas ativas é restrita ao canal License/Generic
    /// próprio da Stock (mesmo filtro usado em GetPartnersAsync) - sem isso,
    /// contaríamos também assinaturas Microsoft do parceiro que não têm
    /// nenhuma relação com a parceria dele com a Stock (ex: a Digytec tem
    /// negócio próprio de Microsoft CSP, completamente separado).
    /// </summary>
    public async Task<List<CustomerDto>> GetPartnerCustomersAsync(string partnerId)
    {
        await EnsureDataLoadedAsync();

        var partnerCustomers = _customersCache!
            .Where(c => c.CustomerAssociations.Any(a => IdEquals(a.ResellerId, partnerId)))
            .ToList();

        var activeSubscriptionsCount = _subscriptionsCache!
            .Where(s => IdEquals(s.ResellerId, partnerId) && IdEquals(s.ProviderInstanceId, _stockProviderInstanceId))
            .GroupBy(s => s.CustomerId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Count(s => IsActive(s.SubscriptionStatus.Name)),
                StringComparer.OrdinalIgnoreCase);

        return partnerCustomers
            .Select(c => MapCustomer(c, activeSubscriptionsCount.GetValueOrDefault(c.Id, 0)))
            .OrderBy(c => c.CompanyName)
            .ToList();
    }

    public async Task<DashboardMetrics> GetDashboardMetricsAsync()
    {
        await EnsureDataLoadedAsync();

        // 1) Clientes que possuem associação direta com a Stock.
        var stockCustomers = GetStockCustomerRaws();

        // 2) Revendedores REAIS da Stock: registros em /v1/resellers cujo
        // ProviderResellers contém o Provider Instance próprio da Stock
        // (canal Generic/license). Isso é o que a UI do CloudCockpit mostra
        // como "revenda abaixo da Stock" - não tem relação com CNPJ batendo
        // com clientes da Stock em outro canal.
        var stockChannelResellers = _resellersCache!
            .Where(r => r.ProviderResellers.Any(pr => IdEquals(pr.ProviderInstanceId, _stockProviderInstanceId)))
            .ToList();
        var stockResellerIds = stockChannelResellers
            .Select(r => r.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 3) Clientes finais diretos da Stock (grupos mutuamente exclusivos).
        var directCustomerIds = GetDirectStockCustomerIds();
        var directCustomers = stockCustomers
            .Where(c => directCustomerIds.Contains(c.Id))
            .ToList();

        // 4) Clientes atendidos pelos revendedores da Stock.
        var resellerEndCustomers = _customersCache!
            .Where(c => c.CustomerAssociations.Any(a => stockResellerIds.Contains(a.ResellerId ?? "")))
            .ToList();
        var resellerEndCustomerIds = resellerEndCustomers
            .Select(c => c.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assinaturas diretas da Stock em QUALQUER canal.
        // Faturamento aqui vem de Subscriptions.RevenuePrice - sabemos que esse
        // valor vem zerado para consumo Azure (canal Microsoft, "azureplan"),
        // mas por decisão do usuário voltamos a usar essa fonte por enquanto:
        // o endpoint de Invoices/lineitems-file retorna 403 mesmo com token de
        // usuário autenticado (chamado de suporte aberto para investigar isso).
        // IMPORTANTE: filtra só por directCustomerIds (associação do cliente),
        // NÃO por Subscription.ResellerId - já confirmamos com dados reais que
        // esse campo pode vir inconsistente (ex: assinatura da Universal
        // Process, cliente direto confirmado da Stock, aparecia com ResellerId
        // da Digytec). Mesmo critério já usado em GetCustomersAsync/GetSubscriptionsAsync.
        var directActive = _subscriptionsCache!
            .Where(s => directCustomerIds.Contains(s.CustomerId))
            .Where(s => IsActive(s.SubscriptionStatus.Name))
            .ToList();

        // Assinaturas dos parceiros dentro do canal License/Generic próprio da
        // Stock (providerInstanceId = 4399530b-...).
        var resellerActive = _subscriptionsCache!
            .Where(s => stockResellerIds.Contains(s.ResellerId ?? "")
                     && IdEquals(s.ProviderInstanceId, _stockProviderInstanceId))
            .Where(s => IsActive(s.SubscriptionStatus.Name))
            .ToList();

        var scopedSubscriptions = directActive.Concat(resellerActive).ToList();

        var customerNames = _customersCache!
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CompanyName.Trim(), StringComparer.OrdinalIgnoreCase);

        var resellerNames = stockChannelResellers
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CompanyName.Trim(), StringComparer.OrdinalIgnoreCase);

        var revenueByCustomer = directActive
            .GroupBy(s => s.CustomerId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RevenueRankingItem
            {
                Id = g.Key,
                Name = customerNames.GetValueOrDefault(g.Key, "(cliente desconhecido)"),
                AnnualRevenue = g.Sum(EstimateAnnualRevenue),
                ActiveSubscriptions = g.Count(),
                CustomerCount = 1
            })
            .OrderByDescending(x => x.AnnualRevenue)
            .ThenBy(x => x.Name)
            .ToList();

        var revenueByReseller = resellerActive
            .GroupBy(s => s.ResellerId ?? "", StringComparer.OrdinalIgnoreCase)
            .Select(g => new RevenueRankingItem
            {
                Id = g.Key,
                Name = resellerNames.GetValueOrDefault(g.Key, "(parceiro desconhecido)"),
                AnnualRevenue = g.Sum(EstimateAnnualRevenue),
                ActiveSubscriptions = g.Count(),
                CustomerCount = g.Select(s => s.CustomerId).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            })
            .OrderByDescending(x => x.AnnualRevenue)
            .ThenBy(x => x.Name)
            .ToList();

        var faturamentoPorTipo = scopedSubscriptions
            .GroupBy(s => string.IsNullOrWhiteSpace(s.OfferType.Name) ? "(outro)" : s.OfferType.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(EstimateAnnualRevenue)))
            .OrderByDescending(kv => kv.Value)
            .ToList();

        return new DashboardMetrics
        {
            TotalClientes = directCustomers.Count,
            TotalRevendedores = stockChannelResellers.Count,
            TotalClientesDosRevendedores = resellerEndCustomerIds.Count,
            TotalAssinaturasAtivas = scopedSubscriptions.Count,
            ReceitaMensalEstimada = scopedSubscriptions.Sum(EstimateMonthlyRevenue),
            ReceitaAnualEstimada = scopedSubscriptions.Sum(EstimateAnnualRevenue),
            RenovacoesProximas60Dias = scopedSubscriptions.Count(IsRenewalSoon),
            FaturamentoPorTipo = faturamentoPorTipo,
            FaturamentoRevendedores = revenueByReseller,
            FaturamentoClientes = revenueByCustomer
        };
    }

    // --- Classificação Stock / revendedores ---

    private List<CustomerRaw> GetStockCustomerRaws() => _customersCache!
        .Where(c => c.CustomerAssociations.Any(a => IdEquals(a.ResellerId, _stockResellerId)))
        .ToList();

    /// <summary>
    /// IDs dos clientes finais DIRETOS da Stock: têm associação com a Stock
    /// (CustomerAssociations) e não são, eles mesmos, um parceiro/revendedor
    /// do canal License da Stock.
    /// </summary>
    private HashSet<string> GetDirectStockCustomerIds()
    {
        var resellerIds = _resellersCache!
            .Where(r => r.ProviderResellers.Any(pr => IdEquals(pr.ProviderInstanceId, _stockProviderInstanceId)))
            .Select(r => r.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return GetStockCustomerRaws()
            .Where(c => !resellerIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // --- Receita mensal estimada ---

    /// <summary>
    /// Mantém o cálculo mensal que já existia no dashboard: considera apenas
    /// assinaturas ativas com cobrança mensal.
    /// </summary>
    /// <summary>
    /// IMPORTANTE: RevenuePrice.Value (x Quantity) é o preço do TERMO da
    /// assinatura (ex: o valor do compromisso de 1 ano), não o valor de uma
    /// parcela. "BillingFrequency" só diz como esse valor é faturado (ex:
    /// parcelado mensalmente), não em que período o preço está expresso.
    /// Confirmado com dados reais: assinaturas com Term "1 year" e billing
    /// "Monthly" têm RevenuePrice batendo com o valor ANUAL, não mensal -
    /// dividir por 12 aqui reproduz a parcela mensal real mostrada na tela.
    /// </summary>
    private static decimal EstimateMonthlyRevenue(SubscriptionRaw s)
    {
        var frequency = (s.BillingFrequency.Name ?? "").Trim().ToLowerInvariant();
        var baseValue = s.RevenuePrice.Value * Math.Max(s.Quantity, 1);

        return frequency switch
        {
            "monthly" => baseValue / 12m,
            "annual" => baseValue / 12m,
            "triennial" => baseValue / 36m,
            _ => 0m
        };
    }

    // --- Receita anual estimada ---

    /// <summary>
    /// Annualiza receita recorrente ativa e inclui compras avulsas (onetime)
    /// pelo valor integral, já que elas contam como faturamento do ano em que
    /// ocorreram (mesmo não sendo recorrentes).
    ///
    /// RevenuePrice.Value (x Quantity) já é o valor do termo/ano - ver nota em
    /// EstimateMonthlyRevenue. Por isso "monthly" e "annual" usam o valor
    /// direto (sem multiplicar por 12): o termo já É de 1 ano nesses casos.
    ///
    /// monthly   -> valor * quantidade (o termo já é anual)
    /// annual    -> valor * quantidade
    /// triennial -> valor * quantidade / 3 (termo de 3 anos, equivalente anual)
    /// onetime   -> valor * quantidade (sem dividir - é pagamento único, sem termo)
    /// </summary>
    private static decimal EstimateAnnualRevenue(SubscriptionRaw s)
    {
        var frequency = (s.BillingFrequency.Name ?? "").Trim().ToLowerInvariant();
        var baseValue = s.RevenuePrice.Value * Math.Max(s.Quantity, 1);

        return frequency switch
        {
            "monthly" => baseValue,
            "annual" => baseValue,
            "triennial" => baseValue / 3m,
            "onetime" => baseValue,
            _ => 0m
        };
    }

    private static bool IsRenewalSoon(SubscriptionRaw s)
    {
        if (!IsActive(s.SubscriptionStatus.Name) || s.EndDate.Year >= 9000)
            return false;

        var days = (s.EndDate - DateTime.UtcNow).Days;
        return days is >= 0 and <= 60;
    }

    private static bool IsActive(string? status) =>
        string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);

    private static bool IdEquals(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    // --- Carga e cache dos dados brutos ---

    private async Task EnsureDataLoadedAsync()
    {
        if (_customersCache != null &&
            _subscriptionsCache != null &&
            _resellersCache != null &&
            _cacheExpiry > DateTime.UtcNow)
            return;

        await LoadLock.WaitAsync();
        try
        {
            // Outra chamada concorrente pode ter carregado o cache enquanto
            // esperávamos o semáforo - confere de novo antes de refazer as
            // chamadas de API.
            if (_customersCache != null &&
                _subscriptionsCache != null &&
                _resellersCache != null &&
                _cacheExpiry > DateTime.UtcNow)
                return;

            var token = await GetTokenAsync();

            // Customers e Subscriptions já eram usados pelo projeto.
            // Resellers é necessário para separar cliente final x revendedor.
            _customersCache = await FetchAllPagesAsync<CustomerRaw>("customers", token);
            _subscriptionsCache = await FetchAllPagesAsync<SubscriptionRaw>("subscriptions", token);
            _resellersCache = await FetchAllPagesAsync<ResellerRaw>("resellers", token);

            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
        }
        finally
        {
            LoadLock.Release();
        }
    }

    private async Task<List<T>> FetchAllPagesAsync<T>(string resource, string token)
    {
        var result = new List<T>();
        var page = 1;
        bool hasNext;

        do
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.cloudcockpit.com/v1/{resource}?pageNumber={page}&pageSize=2000");

            request.Headers.Add("X-Tenant", _config["CloudCockpit:TenantDomain"]);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var pageResult = await response.Content.ReadFromJsonAsync<PagedResult<T>>();
            if (pageResult == null) break;

            result.AddRange(pageResult.Items);
            hasNext = pageResult.HasNextPage;
            page++;
        }
        while (hasNext);

        return result;
    }

    // --- Faturamento real via faturas (Invoices) do canal Microsoft ---

    /// <summary>
    /// Busca todas as faturas tipo "onetime" de um provider instance (todo o
    /// histórico disponível), baixa o CSV de line items de cada uma (via o
    /// endpoint /v1/Invoices/{id}/lineitems-file, que devolve uma URL assinada
    /// de curta duração) e retorna todas as linhas já parseadas.
    ///
    /// "onetime" é o único invoiceType que traz granularidade por
    /// cliente/revendedor/subscription nesse provider instance - "usage" só tem
    /// totais consolidados do canal inteiro (sem customerId/resellerId), e
    /// "license" não existe nesse provider instance.
    /// </summary>
    private async Task<List<InvoiceLineItemRaw>> FetchAllInvoiceLineItemsAsync(string providerInstanceId, string invoiceType)
    {
        var result = new List<InvoiceLineItemRaw>();
        if (string.IsNullOrWhiteSpace(providerInstanceId))
            return result;

        var token = await GetTokenAsync();
        var invoices = new List<InvoiceRaw>();
        var page = 1;
        bool hasNext;

        do
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.cloudcockpit.com/v1/Invoices?providerInstanceId={providerInstanceId}&invoiceType={invoiceType}&pageNumber={page}&pageSize=2000");
            request.Headers.Add("X-Tenant", _config["CloudCockpit:TenantDomain"]);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var pageResult = await response.Content.ReadFromJsonAsync<PagedResult<InvoiceRaw>>();
            if (pageResult == null) break;

            invoices.AddRange(pageResult.Items);
            hasNext = pageResult.HasNextPage;
            page++;
        }
        while (hasNext);

        _logger.LogInformation(
            "Invoices ({InvoiceType}, providerInstanceId={ProviderInstanceId}): {Count} faturas encontradas",
            invoiceType, providerInstanceId, invoices.Count);

        var linhasProcessadas = 0;
        foreach (var invoice in invoices)
        {
            // Fatura ainda não processada pela Microsoft/CloudCockpit - sem arquivo pra baixar.
            if (invoice.InvoiceLineItemsFile != null &&
                !string.Equals(invoice.InvoiceLineItemsFile.Status, "available", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId}: lineitems-file com status '{Status}' - pulando",
                    invoice.Id, invoice.InvoiceLineItemsFile.Status);
                continue;
            }

            var fileRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.cloudcockpit.com/v1/Invoices/{invoice.Id}/lineitems-file");
            fileRequest.Headers.Add("X-Tenant", _config["CloudCockpit:TenantDomain"]);
            fileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var fileResponse = await _http.SendAsync(fileRequest);
            if (!fileResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId}: GET lineitems-file retornou {StatusCode}",
                    invoice.Id, (int)fileResponse.StatusCode);
                continue;
            }

            var fileInfo = await fileResponse.Content.ReadFromJsonAsync<InvoiceLineItemsFileRaw>();
            if (fileInfo == null || string.IsNullOrWhiteSpace(fileInfo.Location))
            {
                _logger.LogWarning("Invoice {InvoiceId}: lineitems-file sem location", invoice.Id);
                continue;
            }

            // A location já vem com SAS token assinado (curta duração) - baixar
            // logo em seguida, sem header de Authorization (o blob storage não
            // aceita Bearer, a assinatura na própria URL já autentica).
            using var csvResponse = await _http.GetAsync(fileInfo.Location);
            if (!csvResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId}: download do CSV retornou {StatusCode}",
                    invoice.Id, (int)csvResponse.StatusCode);
                continue;
            }

            var csvContent = await csvResponse.Content.ReadAsStringAsync();
            var lines = ParseInvoiceLineItemsCsv(csvContent);
            foreach (var line in lines)
                line.InvoiceDate = invoice.InvoiceDate;

            result.AddRange(lines);
            linhasProcessadas += lines.Count;
        }

        _logger.LogInformation(
            "Invoices ({InvoiceType}): {LinhasProcessadas} linhas de line items carregadas no total",
            invoiceType, linhasProcessadas);

        return result;
    }

    /// <summary>
    /// Parser manual de CSV (delimitador ';', aspas duplas para campos com
    /// caracteres especiais) - suficiente para o formato exportado pelo
    /// CloudCockpit/Microsoft, sem precisar de dependência externa.
    /// </summary>
    private static List<InvoiceLineItemRaw> ParseInvoiceLineItemsCsv(string content)
    {
        var items = new List<InvoiceLineItemRaw>();
        var rows = ParseCsvRows(content, ';');
        if (rows.Count < 2)
            return items;

        var header = rows[0];
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
            columnIndex[header[i].Trim()] = i;

        string GetValue(string[] row, string column) =>
            columnIndex.TryGetValue(column, out var i) && i < row.Length ? row[i] : "";

        decimal GetDecimal(string[] row, string column)
        {
            var raw = GetValue(row, column).Trim();
            if (string.IsNullOrEmpty(raw))
                return 0m;
            return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out var value)
                ? value
                : 0m;
        }

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Length < 2)
                continue; // linha em branco no final do arquivo

            items.Add(new InvoiceLineItemRaw
            {
                CustomerId = GetValue(row, "CustomerId"),
                CustomerName = GetValue(row, "CustomerName"),
                CustomerInternalId = GetValue(row, "CustomerInternalId"),
                ResellerId = GetValue(row, "ResellerId"),
                ResellerName = GetValue(row, "ResellerName"),
                ResellerInternalId = GetValue(row, "ResellerInternalId"),
                SubscriptionId = GetValue(row, "SubscriptionId"),
                ProductName = GetValue(row, "ProductName"),
                ProductType = GetValue(row, "ProductType"),
                Subtotal = GetDecimal(row, "Subtotal"),
                Total = GetDecimal(row, "Total"),
                SubtotalForReseller = GetDecimal(row, "SubtotalForReseller"),
                TotalForReseller = GetDecimal(row, "TotalForReseller"),
                SubtotalForCustomer = GetDecimal(row, "SubtotalForCustomer"),
                TotalForCustomer = GetDecimal(row, "TotalForCustomer"),
                BillingFrequency = GetValue(row, "BillingFrequency")
            });
        }

        return items;
    }

    /// <summary>
    /// Parser RFC4180-ish: separa linhas/campos respeitando aspas duplas
    /// (inclusive com delimitador ou quebra de linha dentro do campo, e aspas
    /// escapadas como "").
    /// </summary>
    private static List<string[]> ParseCsvRows(string content, char delimiter)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        var i = 0;
        var n = content.Length;

        while (i < n)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < n && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
                continue;
            }

            if (c == '"') { inQuotes = true; i++; continue; }
            if (c == delimiter) { fields.Add(field.ToString()); field.Clear(); i++; continue; }
            if (c == '\r') { i++; continue; }
            if (c == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                rows.Add(fields.ToArray());
                fields = new List<string>();
                i++;
                continue;
            }

            field.Append(c);
            i++;
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields.ToArray());
        }

        return rows;
    }

    private async Task<string> GetTokenAsync()
    {
        if (_cachedToken != null && _tokenExpiry > DateTime.UtcNow)
            return _cachedToken;

        var response = await _http.PostAsync(
            _config["CloudCockpit:TokenUrl"],
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _config["CloudCockpit:ClientId"] ?? "",
                ["client_secret"] = _config["CloudCockpit:ClientSecret"] ?? "",
                ["scope"] = _config["CloudCockpit:Scope"] ?? "",
                ["grant_type"] = "client_credentials"
            }));

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        _cachedToken = token!.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);
        return _cachedToken;
    }

    // --- Mapeamento bruto -> DTO público ---

    private static CustomerDto MapCustomer(CustomerRaw c, int activeSubs) => new()
    {
        Id = c.Id,
        CompanyName = c.CompanyName.Trim(),
        TaxId = (c.TaxId ?? c.InternalIdentifier ?? "").Trim(),
        City = c.City ?? "",
        State = c.State ?? "",
        ContactName = $"{c.FirstName} {c.LastName}".Trim(),
        ContactEmail = c.Email ?? "",
        ContactPhone = c.Phone ?? "",
        ActiveSubscriptionsCount = activeSubs
    };

    private static SubscriptionDto MapSubscription(SubscriptionRaw s, string customerName) => new()
    {
        Id = s.Id,
        CustomerId = s.CustomerId,
        ResellerId = s.ResellerId ?? "",
        CustomerName = customerName,
        SubscriptionName = s.SubscriptionName,
        OfferType = s.OfferType.Name,
        Status = s.SubscriptionStatus.Name,
        Quantity = s.Quantity,
        AssignedLicenses = s.AssignedLicenses,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        BillingFrequency = s.BillingFrequency.Name,
        TermDuration = s.TermDuration.Name,
        AutoRenew = s.AutoRenew.Name,
        RevenuePrice = s.RevenuePrice
    };
}