using System.Text.Json;
using DataAccess;
using WebApi.Repos;
using WebApi.Services;

namespace WebApi.Configuration;

public static class AppConfiguration
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        services.AddEndpointsApiExplorer();
        services.AddOpenApi();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        services.AddSingleton<IMongoDbConnectionFactory, MongoDbConnectionFactory>();
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<ILoanService, LoanService>();

        return services;
    }

    public static IApplicationBuilder ConfigureApp(this IApplicationBuilder app)
    {
        app.UseCors();
        return app;
    }
}
