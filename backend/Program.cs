using ModelAggregator.Api.Adapters;
using ModelAggregator.Api.Services;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// --- Source Adapters (extensible: just add more registrations) ---
// Note: We add a standard User-Agent because some APIs (like Printables) require it.
var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

builder.Services.AddHttpClient<ThingiverseAdapter>(client => 
{
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
});

builder.Services.AddHttpClient<Cults3DAdapter>(client => 
{
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
});

builder.Services.AddHttpClient<MyMiniFactoryAdapter>(client => 
{
    // MMF Adapter sets its own, but we can set a fallback here or let it override.
    // The adapter code does: _http.DefaultRequestHeaders.Add("User-Agent", "3DSearchAggregator/1.0");
    // We'll leave it to the adapter to set its specific one if needed, or we can unify.
    // For now, let's just register it securely.
    client.DefaultRequestHeaders.Add("User-Agent", userAgent); 
});

builder.Services.AddHttpClient<PrintablesAdapter>(client => 
{
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
});

builder.Services.AddHttpClient<MakerWorldAdapter>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,application/json,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All,
    AllowAutoRedirect = true,
});

builder.Services.AddScoped<IModelSourceAdapter, ThingiverseAdapter>();
builder.Services.AddScoped<IModelSourceAdapter, Cults3DAdapter>();
builder.Services.AddScoped<IModelSourceAdapter, MyMiniFactoryAdapter>();
builder.Services.AddScoped<IModelSourceAdapter, PrintablesAdapter>();
builder.Services.AddScoped<IModelSourceAdapter, MakerWorldAdapter>();

// --- Services ---
builder.Services.AddScoped<IRandomSearchService, RandomSearchService>();
builder.Services.AddScoped<SearchService>();

// --- CORS (allow React frontend) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // Allow any origin for testing/deployment flexibility
        policy.AllowAnyOrigin() 
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Middleware ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowFrontend");
app.MapControllers();

try
{
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"CRITICAL: Application startup failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    throw;
}
