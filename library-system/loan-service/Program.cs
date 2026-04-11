var builder = WebApplication.CreateBuilder(args);

builder.ConfigureLoanService();

var app = builder.Build();

await app.ConfigureLoanServiceAsync();
await app.RunAsync();
