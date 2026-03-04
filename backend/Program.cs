using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using ModelAggregator.Api.Adapters;
using ModelAggregator.Api.Data;
using ModelAggregator.Api.Services;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// --- Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Password Hashing ---
builder.Services.AddScoped<IPasswordHasher<ModelAggregator.Api.Data.Entities.User>, PasswordHasher<ModelAggregator.Api.Data.Entities.User>>();

// --- Authentication (JWT) ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthService>();

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
// Collect origins from appsettings.json
var configOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

// Also support a simple comma-separated CORS_ORIGINS env var (easiest to set on Render)
var envOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS")
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

// Merge all origins, always include localhost for dev
var allowedOrigins = configOrigins
    .Concat(envOrigins)
    .Concat(new[] { "http://localhost:5173" })
    .Select(o => o.TrimEnd('/'))   // normalize: strip trailing slashes
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

Log.Information("CORS allowed origins: {Origins}", string.Join(", ", allowedOrigins));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Auto-apply migrations on startup ---
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// --- Middleware ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Simple endpoint to keep the Render service awake
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

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
