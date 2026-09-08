using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StockPortal.Models;

namespace StockPortal.Services;

// DTOs internos que espelham o shape exato retornado pela API do CloudCockpit.
// Não expor estes diretamente pro front-end - sempre mapear para os DTOs em Models/.

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

internal class SubscriptionRaw
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string? ResellerId { get; set; }
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

internal class TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

public class CloudCockpitApiService : IStockDataProvider
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly string _stockResellerId;

    private string? _cachedToken;
    private DateTime _tokenExpiry;

    private List<CustomerRaw>? _customersCache;
    private List<SubscriptionRaw>? _subscriptionsCache;
    private DateTime _cacheExpiry;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(45);

    public CloudCockpitApiService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
        _stockResellerId = config["CloudCockpit:StockResellerId"] ?? "";
    }

    public async Task<List<CustomerDto>> GetCustomersAsync()
    {
        await EnsureDataLoadedAsync();

        var subsByCustomer = _subscriptionsCache!
            .Where(s => s.ResellerId == _stockResellerId)
            .GroupBy(s => s.CustomerId)
            .ToDictionary(g => g.Key, g => g.Count(s => s.SubscriptionStatus.Name == "active"));

        return _customersCache!
            .Where(c => c.CustomerAssociations.Any(a => a.ResellerId == _stockResellerId))
            .Select(c => MapCustomer(c, subsByCustomer.GetValueOrDefault(c.Id, 0)))
            .OrderBy(c => c.CompanyName)
            .ToList();
    }

    public async Task<List<SubscriptionDto>> GetSubscriptionsAsync()
    {
        await EnsureDataLoadedAsync();

        var customerNames = _customersCache!.ToDictionary(c => c.Id, c => c.CompanyName);

        return _subscriptionsCache!
            .Where(s => s.ResellerId == _stockResellerId)
            .Select(s => MapSubscription(s, customerNames.GetValueOrDefault(s.CustomerId, "(desconhecido)")))
            .OrderByDescending(s => s.StartDate)
            .ToList();
    }

    public async Task<List<SubscriptionDto>> GetSubscriptionsByCustomerAsync(string customerId)
    {
        var all = await GetSubscriptionsAsync();
        return all.Where(s => s.CustomerId == customerId).ToList();
    }

    public async Task<DashboardMetrics> GetDashboardMetricsAsync()
    {
        var customers = await GetCustomersAsync();
        var subscriptions = await GetSubscriptionsAsync();
        var active = subscriptions.Where(s => s.Status == "active").ToList();

        return new DashboardMetrics
        {
            TotalClientes = customers.Count,
            TotalAssinaturasAtivas = active.Count,
            ReceitaMensalEstimada = active
                .Where(s => s.BillingFrequency == "monthly")
                .Sum(s => s.RevenuePrice.Value * Math.Max(s.Quantity, 1)),
            RenovacoesProximas60Dias = active.Count(s => s.IsRenewalSoon),
            AssinaturasPorTipo = active
                .GroupBy(s => s.OfferType)
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .OrderByDescending(kv => kv.Value)
                .ToList()
        };
    }

    // --- Carga e cache dos dados brutos ---

    private async Task EnsureDataLoadedAsync()
    {
        if (_customersCache != null && _subscriptionsCache != null && _cacheExpiry > DateTime.UtcNow)
            return;

        var token = await GetTokenAsync();
        _customersCache = await FetchAllPagesAsync<CustomerRaw>("customers", token);
        _subscriptionsCache = await FetchAllPagesAsync<SubscriptionRaw>("subscriptions", token);
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
    }

    private async Task<List<T>> FetchAllPagesAsync<T>(string resource, string token)
    {
        var result = new List<T>();
        var page = 1;
        bool hasNext;

        do
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
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

    private async Task<string> GetTokenAsync()
    {
        if (_cachedToken != null && _tokenExpiry > DateTime.UtcNow)
            return _cachedToken;

        var response = await _http.PostAsync(_config["CloudCockpit:TokenUrl"],
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
