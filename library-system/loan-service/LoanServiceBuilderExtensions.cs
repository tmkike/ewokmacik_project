using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

static class LoanServiceBuilderExtensions
{
    public static void ConfigureLoanService(this WebApplicationBuilder builder)
    {
        ConfigureHosting(builder);
        ConfigureApiServices(builder.Services);
        ConfigureBookServiceClient(builder.Services, builder.Configuration);
        ConfigureMongoDb(builder.Services);
    }

    private static void ConfigureHosting(WebApplicationBuilder builder)
    {
        var configuredPort = builder.Configuration["PORT"];
        if (!string.IsNullOrWhiteSpace(configuredPort))
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{configuredPort}");
        }
    }

    private static void ConfigureApiServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Library System Loan Service API",
                Version = "v1",
                Description = "A kolcsonzeskezelo mikroszerviz publikus API-ja.",
            });
        });
    }

    private static void ConfigureBookServiceClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<BookServiceClient>(client =>
        {
            var bookServiceUrl = configuration["BOOK_SERVICE_URL"] ?? "http://localhost:3001";
            client.BaseAddress = new Uri($"{bookServiceUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddSingleton<BookReleaseSyncService>();
        services.AddHostedService<BookReleaseRetryBackgroundService>();
    }

    private static void ConfigureMongoDb(IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration["MONGODB_URI"]
                ?? configuration.GetConnectionString("MongoDb")
                ?? "mongodb://localhost:27017";
            var databaseName = configuration["MONGODB_DB"]
                ?? configuration["MongoDb:DatabaseName"]
                ?? "library";

            return new MongoDbContext(new MongoClient(connectionString).GetDatabase(databaseName));
        });
    }
}
