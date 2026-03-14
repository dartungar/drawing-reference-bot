var builder = Host.CreateApplicationBuilder(args);

builder.AddEnvironmentOverrides();
builder.Services.AddDrawingBotServices(builder.Configuration);

await builder.Build().RunAsync();
