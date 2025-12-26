using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Vehicle Tracker API", 
        Version = "v1",
        Description = "API for tracking vehicles using Datatrack 247"
    });
});

// Build connection string from Database config section
var dbConfig = builder.Configuration.GetSection("Database");
var connectionString = BuildConnectionString(dbConfig);

if (!string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine($"Connecting to database: {dbConfig["Host"]}/{dbConfig["Database"]}");
    
    builder.Services.AddDbContext<TrackingDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(30);
        }));
    
    // Register repository
    builder.Services.AddScoped<ITrackingRepository, TrackingRepository>();
    
    // Register external vehicle sync service
    builder.Services.AddScoped<IExternalVehicleSyncService, ExternalVehicleSyncService>();
    
    // Register Datatrack background sync service
    builder.Services.AddHostedService<DatatrackSyncService>();
}
else
{
    Console.WriteLine("WARNING: No database configured. Running in API-only mode.");
}

// Register HttpClient for Datatrack service
builder.Services.AddHttpClient<IDatatrackService, DatatrackService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Also register DatatrackService without interface for direct access
builder.Services.AddHttpClient<DatatrackService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // Vite default
                "http://localhost:3000",  // CRA default
                "http://localhost:4173"   // Vite preview
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add response caching
builder.Services.AddResponseCaching();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vehicle Tracker API v1");
    });
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowReactApp");

app.UseResponseCaching();

app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

// Helper function to build connection string from config
static string BuildConnectionString(IConfigurationSection dbConfig)
{
    var host = dbConfig["Host"];
    if (string.IsNullOrEmpty(host)) return string.Empty;

    var port = dbConfig["Port"] ?? "5432";
    var database = dbConfig["Database"] ?? "postgres";
    var username = dbConfig["Username"] ?? "postgres";
    var password = dbConfig["Password"] ?? "";
    var pooling = dbConfig["Pooling"] ?? "true";
    var minPoolSize = dbConfig["MinPoolSize"] ?? "0";
    var maxPoolSize = dbConfig["MaxPoolSize"] ?? "100";
    var connectionLifetime = dbConfig["ConnectionLifetime"] ?? "0";
    var sslMode = dbConfig["SSLMode"] ?? "Prefer";

    return $"Host={host};Port={port};Database={database};Username={username};Password={password};" +
           $"Pooling={pooling};Minimum Pool Size={minPoolSize};Maximum Pool Size={maxPoolSize};" +
           $"Connection Lifetime={connectionLifetime};SSL Mode={sslMode}";
}
