using StockPortal.Models;

namespace StockPortal.Services;

/// <summary>
/// Dados de exemplo para visualizar a plataforma sem configurar credenciais.
/// O mock reproduz a separação usada no dashboard:
/// - clientes finais diretos da Stock;
/// - revendedores da Stock;
/// - clientes finais atendidos pelos revendedores.
/// </summary>
public class MockDataService : IStockDataProvider
{
    private const string StockId = "stock-reseller";
    private const string DigytecId = "reseller-digytec";
    private const string MspId = "reseller-msp";

    private static readonly List<CustomerDto> DirectCustomers = new()
    {
        new() { Id = "c1", CompanyName = "TECNOVA SISTEMAS DE INFORMATICA LTDA", TaxId = "22.111.333/0001-20", City = "CAMPINAS", State = "SP", ContactName = "Renata Souza", ContactEmail = "renata@tecnova.com.br", ActiveSubscriptionsCount = 2 },
        new() { Id = "c2", CompanyName = "GRUPO MERIDIAN CONSULTORIA LTDA", TaxId = "18.777.222/0001-45", City = "BELO HORIZONTE", State = "MG", ContactName = "Paulo Andrade", ContactEmail = "paulo@meridian.com.br", ActiveSubscriptionsCount = 1 },
        new() { Id = "c3", CompanyName = "ALPHA CLOUD SERVICOS LTDA", TaxId = "11.222.333/0001-44", City = "SAO PAULO", State = "SP", ContactName = "Ana Lima", ContactEmail = "ana@alphacloud.com.br", ActiveSubscriptionsCount = 1 }
    };

    private static readonly Dictionary<string, string> ResellerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [DigytecId] = "DIGYTEC TECNOLOGIA",
        [MspId] = "MSP MOREIRA E CAPPONI TECNOLOGIA LTDA"
    };

    private static readonly Dictionary<string, string> AllCustomerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c1"] = "TECNOVA SISTEMAS DE INFORMATICA LTDA",
        ["c2"] = "GRUPO MERIDIAN CONSULTORIA LTDA",
        ["c3"] = "ALPHA CLOUD SERVICOS LTDA",
        ["r-c1"] = "LATITUDE SOLUCOES TECNOLOGICAS LTDA",
        ["r-c2"] = "UNIVERSAL PROCESS EQUIPAMENTOS INDUSTRIAIS LTDA",
        ["r-c3"] = "CLIENTE FINAL MSP LTDA"
    };

    private static readonly List<SubscriptionDto> AllSubscriptions = new()
    {
        // Venda direta Stock
        new() { Id = "s1", CustomerId = "c1", ResellerId = StockId, CustomerName = AllCustomerNames["c1"], SubscriptionName = "Microsoft 365 Business Premium", OfferType = "license", Status = "active", Quantity = 25, StartDate = new DateTime(2026, 1, 1), EndDate = DateTime.UtcNow.AddDays(35), BillingFrequency = "monthly", TermDuration = "oneyear", RevenuePrice = new Money { Value = 89.90m } },
        new() { Id = "s2", CustomerId = "c1", ResellerId = StockId, CustomerName = AllCustomerNames["c1"], SubscriptionName = "Azure Plan", OfferType = "azureplan", Status = "active", Quantity = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(9999, 12, 31), BillingFrequency = "monthly", TermDuration = "noterm", RevenuePrice = new Money { Value = 3420.50m } },
        new() { Id = "s3", CustomerId = "c2", ResellerId = StockId, CustomerName = AllCustomerNames["c2"], SubscriptionName = "Microsoft 365 Business Standard", OfferType = "license", Status = "active", Quantity = 14, StartDate = new DateTime(2026, 1, 1), EndDate = DateTime.UtcNow.AddDays(58), BillingFrequency = "monthly", TermDuration = "oneyear", RevenuePrice = new Money { Value = 33.62m } },
        new() { Id = "s4", CustomerId = "c3", ResellerId = StockId, CustomerName = AllCustomerNames["c3"], SubscriptionName = "Windows Server Subscription", OfferType = "license", Status = "active", Quantity = 4, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1), BillingFrequency = "annual", TermDuration = "oneyear", RevenuePrice = new Money { Value = 1800m } },

        // Clientes finais da Digytec
        new() { Id = "s5", CustomerId = "r-c1", ResellerId = DigytecId, CustomerName = AllCustomerNames["r-c1"], SubscriptionName = "Microsoft 365 E3", OfferType = "license", Status = "active", Quantity = 40, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1), BillingFrequency = "monthly", TermDuration = "oneyear", RevenuePrice = new Money { Value = 180m } },
        new() { Id = "s6", CustomerId = "r-c2", ResellerId = DigytecId, CustomerName = AllCustomerNames["r-c2"], SubscriptionName = "Azure Plan", OfferType = "azureplan", Status = "active", Quantity = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(9999, 12, 31), BillingFrequency = "monthly", TermDuration = "noterm", RevenuePrice = new Money { Value = 5600m } },

        // Cliente final MSP
        new() { Id = "s7", CustomerId = "r-c3", ResellerId = MspId, CustomerName = AllCustomerNames["r-c3"], SubscriptionName = "Microsoft 365 Business Basic", OfferType = "license", Status = "active", Quantity = 18, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1), BillingFrequency = "annual", TermDuration = "oneyear", RevenuePrice = new Money { Value = 420m } }
    };

    public Task<List<CustomerDto>> GetCustomersAsync()
    {
        var result = DirectCustomers
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                CompanyName = c.CompanyName,
                TaxId = c.TaxId,
                City = c.City,
                State = c.State,
                ContactName = c.ContactName,
                ContactEmail = c.ContactEmail,
                ContactPhone = c.ContactPhone,
                AgreementStatus = c.AgreementStatus,
                ActiveSubscriptionsCount = AllSubscriptions.Count(s =>
                    s.CustomerId == c.Id && s.ResellerId == StockId &&
                    s.Status.Equals("active", StringComparison.OrdinalIgnoreCase)),
                AnnualRevenue = AllSubscriptions
                    .Where(s => s.CustomerId == c.Id && s.ResellerId == StockId &&
                                s.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                    .Sum(EstimateAnnualRevenue)
            })
            .OrderBy(c => c.CompanyName)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<List<SubscriptionDto>> GetSubscriptionsAsync() =>
        Task.FromResult(AllSubscriptions.Where(s => s.ResellerId == StockId).ToList());

    public Task<List<SubscriptionDto>> GetSubscriptionsByCustomerAsync(string customerId) =>
        Task.FromResult(AllSubscriptions
            .Where(s => s.ResellerId == StockId && s.CustomerId == customerId)
            .ToList());

    public Task<List<PartnerDto>> GetPartnersAsync()
    {
        var reseller = AllSubscriptions
            .Where(s => s.Status.Equals("active", StringComparison.OrdinalIgnoreCase) && ResellerNames.ContainsKey(s.ResellerId))
            .ToList();

        var partners = ResellerNames
            .Select(kv => new PartnerDto
            {
                Id = kv.Key,
                CompanyName = kv.Value,
                TaxId = "",
                EndCustomerCount = reseller.Where(s => s.ResellerId == kv.Key).Select(s => s.CustomerId).Distinct().Count(),
                ActiveSubscriptionsCount = reseller.Count(s => s.ResellerId == kv.Key),
                AnnualRevenue = reseller.Where(s => s.ResellerId == kv.Key).Sum(EstimateAnnualRevenue)
            })
            .OrderBy(p => p.CompanyName)
            .ToList();

        return Task.FromResult(partners);
    }

    public Task<List<CustomerDto>> GetPartnerCustomersAsync(string partnerId)
    {
        var result = AllSubscriptions
            .Where(s => s.ResellerId == partnerId)
            .Select(s => s.CustomerId)
            .Distinct()
            .Select(id => new CustomerDto
            {
                Id = id,
                CompanyName = AllCustomerNames.GetValueOrDefault(id, id),
                ActiveSubscriptionsCount = AllSubscriptions.Count(s =>
                    s.CustomerId == id && s.ResellerId == partnerId &&
                    s.Status.Equals("active", StringComparison.OrdinalIgnoreCase)),
                AnnualRevenue = AllSubscriptions
                    .Where(s => s.CustomerId == id && s.ResellerId == partnerId &&
                                s.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                    .Sum(EstimateAnnualRevenue)
            })
            .OrderBy(c => c.CompanyName)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<DashboardMetrics> GetDashboardMetricsAsync()
    {
        var active = AllSubscriptions
            .Where(s => s.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var direct = active.Where(s => s.ResellerId == StockId).ToList();
        var reseller = active.Where(s => ResellerNames.ContainsKey(s.ResellerId)).ToList();

        var metrics = new DashboardMetrics
        {
            TotalClientes = DirectCustomers.Count,
            TotalRevendedores = ResellerNames.Count,
            TotalClientesDosRevendedores = reseller.Select(s => s.CustomerId).Distinct().Count(),
            TotalAssinaturasAtivas = active.Count,
            ReceitaMensalEstimada = active.Sum(EstimateMonthlyRevenue),
            ReceitaAnualEstimada = active.Sum(EstimateAnnualRevenue),
            RenovacoesProximas60Dias = active.Count(s => s.IsRenewalSoon),
            FaturamentoPorTipo = active
                .GroupBy(s => s.OfferType)
                .Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(EstimateAnnualRevenue)))
                .OrderByDescending(x => x.Value)
                .ToList(),
            FaturamentoClientes = direct
                .GroupBy(s => s.CustomerId)
                .Select(g => new RevenueRankingItem
                {
                    Id = g.Key,
                    Name = AllCustomerNames[g.Key],
                    AnnualRevenue = g.Sum(EstimateAnnualRevenue),
                    ActiveSubscriptions = g.Count(),
                    CustomerCount = 1
                })
                .OrderByDescending(x => x.AnnualRevenue)
                .ToList(),
            FaturamentoRevendedores = reseller
                .GroupBy(s => s.ResellerId)
                .Select(g => new RevenueRankingItem
                {
                    Id = g.Key,
                    Name = ResellerNames[g.Key],
                    AnnualRevenue = g.Sum(EstimateAnnualRevenue),
                    ActiveSubscriptions = g.Count(),
                    CustomerCount = g.Select(s => s.CustomerId).Distinct().Count()
                })
                .OrderByDescending(x => x.AnnualRevenue)
                .ToList()
        };

        return Task.FromResult(metrics);
    }

    private static decimal EstimateMonthlyRevenue(SubscriptionDto s)
    {
        var baseValue = s.RevenuePrice.Value * Math.Max(s.Quantity, 1);
        return s.BillingFrequency.Trim().ToLowerInvariant() switch
        {
            "monthly" => baseValue / 12m,
            "annual" => baseValue / 12m,
            "triennial" => baseValue / 36m,
            _ => 0m
        };
    }

    private static decimal EstimateAnnualRevenue(SubscriptionDto s)
    {
        var baseValue = s.RevenuePrice.Value * Math.Max(s.Quantity, 1);
        return s.BillingFrequency.Trim().ToLowerInvariant() switch
        {
            "monthly" => baseValue,
            "annual" => baseValue,
            "triennial" => baseValue / 3m,
            "onetime" => baseValue,
            _ => 0m
        };
    }
}