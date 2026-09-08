using StockPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<CloudCockpitApiService>();

// Troque para "false" em appsettings.json quando as credenciais reais
// do CloudCockpit estiverem configuradas (client_id / client_secret).
var useMockData = builder.Configuration.GetValue<bool>("UseMockData", true);

if (useMockData)
{
    builder.Services.AddSingleton<IStockDataProvider, MockDataService>();
}
else
{
    builder.Services.AddScoped<IStockDataProvider>(sp =>
        sp.GetRequiredService<CloudCockpitApiService>());
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<StockPortal.App>()
    .AddInteractiveServerRenderMode();

app.Run();
