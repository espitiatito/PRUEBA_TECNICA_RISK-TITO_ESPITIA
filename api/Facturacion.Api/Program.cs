var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "facturacion api");

app.Run();
