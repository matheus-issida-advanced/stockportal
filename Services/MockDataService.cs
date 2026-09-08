using StockPortal.Models;

namespace StockPortal.Services;

/// <summary>
/// Dados de exemplo, no mesmo formato que a API real devolve, para visualizar
/// a plataforma sem precisar configurar client_id/client_secret ainda.
/// Troque "UseMockData": false no appsettings.json quando as credenciais reais
/// estiverem prontas.
/// </summary>
public class MockDataService : IStockDataProvider
{
    private static readonly List<CustomerDto> Customers = new()
    {
        new() { Id = "f14d5d35-0433-4c19-a0e7-d98d9f22321e", CompanyName = "UNIVERSAL PROCESS EQUIPAMENTOS INDUSTRIAIS LTDA", TaxId = "74.570.763/0001-04", City = "SAO PAULO", State = "sp", ContactName = "Maciel Chaves", ContactEmail = "sandra@upe.com.br", ContactPhone = "+551150989800", ActiveSubscriptionsCount = 1 },
        new() { Id = "f608f7ea-61eb-419c-92cc-1bda3919cce2", CompanyName = "LATITUDE SOLUCOES TECNOLOGICAS LTDA", TaxId = "47.325.407/0001-52", City = "GOIANIA", State = "Goiás", ContactName = "Leandro Faria", ContactEmail = "leandro.faria@digytectecnologia.com.br", ContactPhone = "(31) 3489-0703", ActiveSubscriptionsCount = 2 },
        new() { Id = "a1b2c3d4-0000-0000-0000-000000000001", CompanyName = "TECNOVA SISTEMAS DE INFORMATICA LTDA", TaxId = "22.111.333/0001-20", City = "CAMPINAS", State = "SP", ContactName = "Renata Souza", ContactEmail = "renata@tecnova.com.br", ContactPhone = "(19) 3222-1010", ActiveSubscriptionsCount = 3 },
        new() { Id = "a1b2c3d4-0000-0000-0000-000000000002", CompanyName = "GRUPO MERIDIAN CONSULTORIA LTDA", TaxId = "18.777.222/0001-45", City = "BELO HORIZONTE", State = "MG", ContactName = "Paulo Andrade", ContactEmail = "paulo@meridian.com.br", ContactPhone = "(31) 3555-8899", ActiveSubscriptionsCount = 1 },
    };

    private static readonly List<SubscriptionDto> Subscriptions = new()
    {
        new() { Id = "s1", CustomerId = "f14d5d35-0433-4c19-a0e7-d98d9f22321e", CustomerName = "UNIVERSAL PROCESS EQUIPAMENTOS INDUSTRIAIS LTDA", SubscriptionName = "Windows Server 2025 Remote Desktop Services - 1 Device CAL", OfferType = "perpetualsoftware", Status = "active", Quantity = 10, AssignedLicenses = null, StartDate = new DateTime(2026, 6, 2), EndDate = new DateTime(9999, 12, 31), BillingFrequency = "onetime", TermDuration = "noterm", AutoRenew = "CancelAtExpiration", RevenuePrice = new Money { Value = 1275.10m } },

        new() { Id = "s2", CustomerId = "f608f7ea-61eb-419c-92cc-1bda3919cce2", CustomerName = "LATITUDE SOLUCOES TECNOLOGICAS LTDA", SubscriptionName = "Microsoft 365 Business Premium", OfferType = "license", Status = "active", Quantity = 25, AssignedLicenses = 23, StartDate = new DateTime(2025, 11, 10), EndDate = DateTime.UtcNow.AddDays(35), BillingFrequency = "monthly", TermDuration = "oneyear", AutoRenew = "RenewToNewTerm", RevenuePrice = new Money { Value = 89.90m } },
        new() { Id = "s3", CustomerId = "f608f7ea-61eb-419c-92cc-1bda3919cce2", CustomerName = "LATITUDE SOLUCOES TECNOLOGICAS LTDA", SubscriptionName = "Azure Plan", OfferType = "azureplan", Status = "active", Quantity = 1, AssignedLicenses = null, StartDate = new DateTime(2026, 3, 24), EndDate = new DateTime(9999, 12, 31), BillingFrequency = "monthly", TermDuration = "noterm", AutoRenew = "CancelAtExpiration", RevenuePrice = new Money { Value = 3420.50m } },

        new() { Id = "s4", CustomerId = "a1b2c3d4-0000-0000-0000-000000000001", CustomerName = "TECNOVA SISTEMAS DE INFORMATICA LTDA", SubscriptionName = "Microsoft 365 Business Basic", OfferType = "license", Status = "active", Quantity = 60, AssignedLicenses = 59, StartDate = new DateTime(2026, 6, 30), EndDate = DateTime.UtcNow.AddDays(12), BillingFrequency = "monthly", TermDuration = "oneyear", AutoRenew = "RenewToNewTerm", RevenuePrice = new Money { Value = 33.79m } },
        new() { Id = "s5", CustomerId = "a1b2c3d4-0000-0000-0000-000000000001", CustomerName = "TECNOVA SISTEMAS DE INFORMATICA LTDA", SubscriptionName = "Azure Database for MySQL Flexible Server", OfferType = "azurereservation", Status = "active", Quantity = 8, AssignedLicenses = null, StartDate = new DateTime(2026, 3, 2), EndDate = new DateTime(2029, 3, 2), BillingFrequency = "monthly", TermDuration = "threeyears", AutoRenew = "CancelAtExpiration", RevenuePrice = new Money { Value = 1086.13m } },
        new() { Id = "s6", CustomerId = "a1b2c3d4-0000-0000-0000-000000000001", CustomerName = "TECNOVA SISTEMAS DE INFORMATICA LTDA", SubscriptionName = "Azure Information Protection Premium P1", OfferType = "license", Status = "deleted", Quantity = 0, AssignedLicenses = null, StartDate = new DateTime(2023, 3, 15), EndDate = new DateTime(2025, 8, 14), BillingFrequency = "monthly", TermDuration = "onemonth", AutoRenew = "CancelAtExpiration", RevenuePrice = new Money { Value = 15.45m } },

        new() { Id = "s7", CustomerId = "a1b2c3d4-0000-0000-0000-000000000002", CustomerName = "GRUPO MERIDIAN CONSULTORIA LTDA", SubscriptionName = "Microsoft 365 Business Standard", OfferType = "license", Status = "active", Quantity = 14, AssignedLicenses = 14, StartDate = new DateTime(2024, 10, 25), EndDate = DateTime.UtcNow.AddDays(58), BillingFrequency = "monthly", TermDuration = "oneyear", AutoRenew = "RenewToNewTerm", RevenuePrice = new Money { Value = 33.62m } },
    };

    public Task<List<CustomerDto>> GetCustomersAsync() => Task.FromResult(Customers);

    public Task<List<SubscriptionDto>> GetSubscriptionsAsync() => Task.FromResult(Subscriptions);

    public Task<List<SubscriptionDto>> GetSubscriptionsByCustomerAsync(string customerId) =>
        Task.FromResult(Subscriptions.Where(s => s.CustomerId == customerId).ToList());

    public Task<DashboardMetrics> GetDashboardMetricsAsync()
    {
        var active = Subscriptions.Where(s => s.Status == "active").ToList();

        return Task.FromResult(new DashboardMetrics
        {
            TotalClientes = Customers.Count,
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
        });
    }
}
