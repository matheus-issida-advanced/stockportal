namespace StockPortal.Models;

/// <summary>
/// Texto do contrato de uma assinatura: prazo do contrato / frequência de cobrança.
/// Exemplos: "Anual/Mensal" (contrato de 1 ano, cobrado mensalmente), "Anual/Anual",
/// "Mensal/Mensal", "Trienal/Anual", "Perpétuo". Valor que não for reconhecido aparece como
/// veio da API, para nada ficar escondido.
/// </summary>
public static class ContractLabel
{
    /// <summary>Texto curto para a tabela, ex.: "Anual/Mensal".</summary>
    public static string Short(string? offerType, string? termDuration, string? billingFrequency)
    {
        if (IsAzurePlan(offerType))
            return "Consumo";

        var term = Term(termDuration);
        var billing = Billing(billingFrequency, forCombination: true);

        if (IsPerpetual(offerType, term, billingFrequency))
            return "Perpétuo";

        if (term is null && billing is null)
            return "—";

        return $"{term ?? "Sem prazo"}/{billing ?? "—"}";
    }

    /// <summary>Texto por extenso para o balão ao passar o mouse.</summary>
    public static string Long(string? offerType, string? termDuration, string? billingFrequency)
    {
        if (IsAzurePlan(offerType))
            return "Azure Plan: consumo faturado mensalmente, sem prazo de contrato";

        var term = Term(termDuration);
        var billing = Billing(billingFrequency, forCombination: false);

        if (IsPerpetual(offerType, term, billingFrequency))
            return "Licença perpétua: pagamento único, sem prazo de contrato";

        var termText = term is null ? "sem prazo de contrato" : $"contrato {term.ToLowerInvariant()}";
        return billing is null ? termText : $"{termText}, cobrança {billing.ToLowerInvariant()}";
    }

    private static bool IsAzurePlan(string? offerType) =>
        string.Equals(offerType?.Trim(), "azureplan", StringComparison.OrdinalIgnoreCase);

    private static bool IsPerpetual(string? offerType, string? term, string? billingFrequency) =>
        string.Equals(offerType?.Trim(), "perpetualsoftware", StringComparison.OrdinalIgnoreCase)
        || (term is null && Normalize(billingFrequency) == "onetime");

    /// <summary>Prazo do contrato; null = sem prazo.</summary>
    private static string? Term(string? termDuration)
    {
        var key = Normalize(termDuration);
        return key switch
        {
            "" or "noterm" or "none" or "notapplicable" => null,
            "onemonth" or "1month" or "month" or "monthly" => "Mensal",
            "oneyear" or "1year" or "year" or "annual" or "yearly" or "12months" => "Anual",
            "twoyear" or "twoyears" or "2year" or "2years" => "2 anos",
            "threeyear" or "threeyears" or "3year" or "3years" or "triennial" or "36months" => "Trienal",
            "fouryear" or "fouryears" or "4year" or "4years" => "4 anos",
            "fiveyear" or "fiveyears" or "5year" or "5years" => "5 anos",
            _ => termDuration!.Trim()
        };
    }

    /// <summary>Frequência de cobrança; null = não informada.</summary>
    private static string? Billing(string? billingFrequency, bool forCombination)
    {
        var key = Normalize(billingFrequency);
        return key switch
        {
            "" => null,
            "monthly" or "month" => "Mensal",
            "annual" or "yearly" or "year" => "Anual",
            "triennial" or "threeyear" or "threeyears" => "Trienal",
            "onetime" => forCombination ? "Única" : "Pagamento único",
            _ => billingFrequency!.Trim()
        };
    }

    private static string Normalize(string? value) =>
        new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
