using Microsoft.AspNetCore.Diagnostics;

static class LoanApplicationBuilderExtensions
{
    public static async Task ConfigureLoanServiceAsync(this WebApplication app)
    {
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
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Loan Service API v1");
        });

        await app.Services.GetRequiredService<MongoDbContext>().EnsureIndexesAsync();

        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "loan-service" }))
            .WithTags("Health");
        LoanApi.Map(app);
    }
}
