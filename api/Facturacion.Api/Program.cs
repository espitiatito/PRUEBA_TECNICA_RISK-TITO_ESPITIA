using Facturacion.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=facturacion.db";

// una sola instancia, asi no lo recreamos en cada request
builder.Services.AddDbContext<FacturacionDbContext>(
    options => options.UseSqlite(connectionString),
    ServiceLifetime.Singleton,
    ServiceLifetime.Singleton);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FacturacionDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}

app.MapGet("/", () => "facturacion api");

app.Run();
