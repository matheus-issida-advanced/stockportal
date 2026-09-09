using StockPortal.Models;

namespace StockPortal.Services;

public interface IStockDataProvider
{
    Task<List<CustomerDto>> GetCustomersAsync();
    Task<List<SubscriptionDto>> GetSubscriptionsAsync();
    Task<List<SubscriptionDto>> GetSubscriptionsByCustomerAsync(string customerId);
    Task<DashboardMetrics> GetDashboardMetricsAsync();
    Task<List<PartnerDto>> GetPartnersAsync();
    Task<List<CustomerDto>> GetPartnerCustomersAsync(string partnerId);
}