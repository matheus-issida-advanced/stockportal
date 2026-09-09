
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockPortal.Models;

public class NamedValue
{
    public string Name { get; set; } = "";
}

/// <summary>
/// A API do CloudCockpit às vezes devolve "currency" como texto simples
/// ("BRL") e às vezes como objeto (ex: {"name":"BRL"} ou {"code":"BRL"}).
/// Esse converter aceita os dois formatos sem quebrar o parsing.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString() ?? "";

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            foreach (var propName in new[] { "code", "name", "currencyCode", "iso", "symbol", "value" })
            {
                if (root.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString() ?? "";
            }
            return "";
        }

        if (reader.TokenType == JsonTokenType.Null)
            return "";

        reader.Skip();
        return "";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

public class Money
{
    public decimal Value { get; set; }

    [JsonConverter(typeof(FlexibleStringConverter))]
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

public class PartnerDto
{
    public string Id { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string TaxId { get; set; } = "";
    public int EndCustomerCount { get; set; }
    public int ActiveSubscriptionsCount { get; set; }
}

public class SubscriptionDto
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string ResellerId { get; set; } = "";
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
    public bool IsRenewalSoon => Status.Equals("active", StringComparison.OrdinalIgnoreCase)
                                 && EndDate.Year < 9000
                                 && DaysUntilRenewal <= 60
                                 && DaysUntilRenewal >= 0;
}

public class RevenueRankingItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal AnnualRevenue { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int CustomerCount { get; set; }
}

public class DashboardMetrics
{
    // Clientes finais que compram diretamente da Stock. Revendedores não entram aqui.
    public int TotalClientes { get; set; }

    // Clientes da Stock que também atuam como reseller no CloudCockpit.
    public int TotalRevendedores { get; set; }

    // Clientes finais atendidos pelos revendedores da Stock.
    public int TotalClientesDosRevendedores { get; set; }

    public int TotalAssinaturasAtivas { get; set; }
    public decimal ReceitaMensalEstimada { get; set; }
    public decimal ReceitaAnualEstimada { get; set; }
    public int RenovacoesProximas60Dias { get; set; }
    public List<KeyValuePair<string, decimal>> FaturamentoPorTipo { get; set; } = new();
    public List<RevenueRankingItem> FaturamentoRevendedores { get; set; } = new();
    public List<RevenueRankingItem> FaturamentoClientes { get; set; } = new();
}