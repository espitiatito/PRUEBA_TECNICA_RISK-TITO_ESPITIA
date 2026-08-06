using Facturacion.Api.Data;
using Facturacion.Api.Dtos;
using Facturacion.Api.Gateway;
using Facturacion.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=facturacion.db";

// una sola instancia, asi no lo recreamos en cada request
builder.Services.AddDbContext<FacturacionDbContext>(
    options => options.UseSqlite(connectionString),
    ServiceLifetime.Singleton,
    ServiceLifetime.Singleton);

builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
builder.Services.AddScoped<ChargeRetryService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors("web");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FacturacionDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}

app.MapGet("/api/charges", (
    FacturacionDbContext db,
    string? status,
    string? failureReason,
    DateTime? from,
    DateTime? to,
    int page = 1,
    int pageSize = 20,
    string? sortBy = null,
    string? sortDir = null) =>
{
    var items = db.Charges.Include(c => c.Attempts).ToList()
        .Where(c => status == null || c.Status == status)
        .Where(c => failureReason == null || c.FailureReason == failureReason)
        .Where(c => from == null || c.DueDate >= from)
        .Where(c => to == null || c.DueDate <= to)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(ChargeMapper.ToListItem)
        .ToList();

    return Results.Ok(items);
});

// TODO: mover esto a un controlador cuando esto crezca un poco mas
app.MapGet("/api/charges/{id:guid}", async (Guid id, FacturacionDbContext db) =>
{
    var charge = db.Charges.FirstAsync(c => c.Id == id).Result;

    charge.Attempts = await db.ChargeAttempts
        .Where(a => a.ChargeId == id)
        .ToListAsync();

    return Results.Ok(ChargeMapper.ToDetail(charge));
});

app.MapPost("/api/charges/{id:guid}/retry", async (
    Guid id,
    RetryChargeRequest request,
    ChargeRetryService service,
    CancellationToken cancellationToken) =>
{
    var response = await service.RetryAsync(id, request, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/_dev/gateway-log", (IPaymentGateway gateway) => Results.Ok(gateway.GetLog()));

app.Run();
