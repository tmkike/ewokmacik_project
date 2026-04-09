using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi.Models;
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Library System Loan Service API",
        Version = "v1",
        Description = "A kölcsönzéskezelő mikroszerviz publikus API-ja.",
    });
});

builder.Services.AddHttpClient<BookServiceClient>(client =>
{
    // A loan-service ezen a belső címen kérdezi le vagy módosítja a könyvek állapotát.
    var bookServiceUrl = builder.Configuration["BOOK_SERVICE_URL"] ?? "http://localhost:3001";
    client.BaseAddress = new Uri($"{bookServiceUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<BookReleaseSyncService>();
builder.Services.AddHostedService<BookReleaseRetryBackgroundService>();

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["MONGODB_URI"]
        ?? configuration.GetConnectionString("MongoDb")
        ?? "mongodb://localhost:27017";
    var databaseName = configuration["MONGODB_DB"]
        ?? configuration["MongoDb:DatabaseName"]
        ?? "library";

    // A Mongo kapcsolatot egyszer hozzuk létre, és ugyanazt az adatbázis-példányt használjuk minden kérésnél.
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
        await context.Response.WriteAsJsonAsync(new ErrorResponse("Belső szerverhiba történt."));
    });
});

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Loan Service API v1");
});

// Az indexek induláskor készülnek el, így futás közben már nem kell ezzel foglalkozni.
await app.Services.GetRequiredService<MongoDbContext>().EnsureIndexesAsync();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "loan-service" }))
    .WithTags("Health");
// A kölcsönzési végpontok külön modulban maradnak, hogy a Program.cs rövid és áttekinthető legyen.
LoanApi.Map(app);

await app.RunAsync();
