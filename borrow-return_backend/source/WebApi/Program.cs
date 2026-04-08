using WebApi.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.ConfigureApp();
app.MapEndpoints();

await app.RunAsync();
