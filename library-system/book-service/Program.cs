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
        Title = "Library System Book Service API",
        Version = "v1",
        Description = "A könyvkezelő mikroszerviz publikus API-ja.",
    });
});

builder.Services.AddHttpClient<LoanServiceClient>(client =>
{
    // A book-service ezen a belső címen éri el a kölcsönzési szolgáltatást.
    var loanServiceUrl = builder.Configuration["LOAN_SERVICE_URL"] ?? "http://localhost:3002";
    client.BaseAddress = new Uri($"{loanServiceUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(5);
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

    // A Mongo kapcsolatot egyszer hozzuk létre, és ugyanazt az adatbázis-példányt osztjuk meg.
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
            logger.LogError(exception, "Unexpected book-service error.");
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
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Book Service API v1");
});

// Az indexek induláskor készülnek el, így a lekérdezések már stabilabb sémára támaszkodhatnak.
await app.Services.GetRequiredService<MongoDbContext>().EnsureIndexesAsync();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "book-service" }))
    .WithTags("Health");
// Az összes könyvvel kapcsolatos publikus és belső végpont külön modulban marad.
BookApi.Map(app);

await app.RunAsync();
