using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

var configuredPort = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(configuredPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{configuredPort}");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddHttpClient<BookServiceClient>(client =>
{
    var bookServiceUrl = builder.Configuration["BOOK_SERVICE_URL"] ?? "http://localhost:3001";
    client.BaseAddress = new Uri($"{bookServiceUrl.TrimEnd('/')}/");
});

builder.Services.AddSingleton(sp =>
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

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (exception is not null)
        {
            logger.LogError(exception, "Unexpected loan-service error.");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ErrorResponse("Belso szerverhiba tortent."));
    });
});

app.UseCors();
await app.Services.GetRequiredService<MongoDbContext>().EnsureIndexesAsync();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "loan-service" }));
LoanApi.Map(app);

await app.RunAsync();
