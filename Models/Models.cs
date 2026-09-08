namespace StockPortal.Models;

public class NamedValue
{
    public string Name { get; set; } = "";
}

public class Money
{
    public decimal Value { get; set; }
    public string Currency { get; set; } = "BRL";
}

public class CustomerDto
{
    public string Id { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string TaxId { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string AgreementStatus { get; set; } = "";
    public int ActiveSubscriptionsCount { get; set; }
}

public class SubscriptionDto
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string SubscriptionName { get; set; } = "";
    public string OfferType { get; set; } = "";
    public string Status { get; set; } = "";
    public int Quantity { get; set; }
    public int? AssignedLicenses { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BillingFrequency { get; set; } = "";
    public string TermDuration { get; set; } = "";
    public string AutoRenew { get; set; } = "";
    public Money RevenuePrice { get; set; } = new();

    public int DaysUntilRenewal => (EndDate - DateTime.UtcNow).Days;
    public bool IsRenewalSoon => Status == "active" && EndDate.Year < 9000 && DaysUntilRenewal <= 60 && DaysUntilRenewal >= 0;
}

public class DashboardMetrics
{
    public int TotalClientes { get; set; }
    public int TotalAssinaturasAtivas { get; set; }
    public decimal ReceitaMensalEstimada { get; set; }
    public int RenovacoesProximas60Dias { get; set; }
    public List<KeyValuePair<string, int>> AssinaturasPorTipo { get; set; } = new();
}
