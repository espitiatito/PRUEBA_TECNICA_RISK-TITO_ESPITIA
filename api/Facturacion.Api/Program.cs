using Facturacion.Api.Data;
using Facturacion.Api.Domain;
using Facturacion.Api.Dtos;
using Facturacion.Api.Gateway;
using Facturacion.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=facturacion.db";

// FacturacionDbContext registrado con ciclo de vida Scoped (por solicitud de concurrencia y seguridad de hilos)
builder.Services.AddDbContext<FacturacionDbContext>(options =>
    options.UseSqlite(connectionString));

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

// RF-1: Bandeja de cobros paginada en servidor con filtros y ordenamiento
app.MapGet("/api/charges", async (
    FacturacionDbContext db,
    string? status,
    string? failureReason,
    DateTime? from,
    DateTime? to,
    string? search,
    int page = 1,
    int pageSize = 20,
    string? sortBy = null,
    string? sortDir = null,
    CancellationToken cancellationToken = default) =>
{
    if (page < 1) page = 1;
    if (pageSize < 1) pageSize = 20;
    if (pageSize > 100) pageSize = 100;

    IQueryable<Charge> query = db.Charges.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(status))
    {
        var s = status.Trim().ToLowerInvariant();
        query = query.Where(c => c.Status.ToLower() == s);
    }

    if (!string.IsNullOrWhiteSpace(failureReason))
    {
        var r = failureReason.Trim();
        query = query.Where(c => c.FailureReason == r);
    }

    if (from.HasValue)
    {
        var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
        query = query.Where(c => c.DueDate >= fromUtc);
    }

    if (to.HasValue)
    {
        var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
        query = query.Where(c => c.DueDate <= toUtc);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(c => c.CustomerName.ToLower().Contains(term)
                              || c.ExternalReference.ToLower().Contains(term)
                              || c.CustomerEmail.ToLower().Contains(term));
    }

    // Ordenamiento por monto o fecha
    var isDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
    query = sortBy?.ToLowerInvariant() switch
    {
        "monto" or "amount" => isDesc ? query.OrderByDescending(c => c.Amount) : query.OrderBy(c => c.Amount),
        "fecha" or "date" or "duedate" => isDesc ? query.OrderByDescending(c => c.DueDate) : query.OrderBy(c => c.DueDate),
        _ => isDesc ? query.OrderBy(c => c.DueDate) : query.OrderByDescending(c => c.DueDate)
    };

    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(c => ChargeMapper.ToListItem(c))
        .ToListAsync(cancellationToken);

    return Results.Ok(new PagedResult<ChargeListItemDto>(items, totalCount, page, pageSize));
});

// RF-2: Detalle del cobro con historial de intentos
app.MapGet("/api/charges/{id:guid}", async (
    Guid id,
    FacturacionDbContext db,
    CancellationToken cancellationToken) =>
{
    var charge = await db.Charges
        .AsNoTracking()
        .Include(c => c.Attempts)
        .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    if (charge is null)
    {
        return Results.NotFound(new { message = $"No se encontró el cobro con id {id}" });
    }

    return Results.Ok(ChargeMapper.ToDetail(charge));
});

app.MapPost("/api/charges/{id:guid}/retry", async (
    Guid id,
    RetryChargeRequest? request,
    ChargeRetryService service,
    CancellationToken cancellationToken) =>
{
    if (request is null || request.Amount <= 0)
    {
        return Results.BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Petición inválida",
            Detail = "El monto del reintento debe ser mayor a cero.",
            Extensions = { ["errorCode"] = BusinessErrorCodes.InvalidAmount }
        });
    }

    var response = await service.RetryAsync(id, request, cancellationToken);
    if (!response.Success)
    {
        if (response.ErrorCode == BusinessErrorCodes.InvalidAmount)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Monto inválido",
                Detail = response.Message,
                Extensions = { ["errorCode"] = response.ErrorCode }
            });
        }
        if (response.ErrorCode == BusinessErrorCodes.ChargeNotFound)
        {
            return Results.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Cobro no encontrado",
                Detail = response.Message
            });
        }

        if (response.ErrorCode == BusinessErrorCodes.RetryInProgress)
        {
            return Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Reintento en curso",
                Detail = response.Message,
                Extensions = { ["errorCode"] = response.ErrorCode }
            });
        }

        if (response.ErrorCode != null)
        {
            return Results.UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Regla de negocio no satisfecha",
                Detail = response.Message,
                Extensions = { ["errorCode"] = response.ErrorCode }
            });
        }

        return Results.BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error en el cobro",
            Detail = response.Message
        });
    }

    return Results.Ok(response);
});

app.MapGet("/api/_dev/gateway-log", (IPaymentGateway gateway) => Results.Ok(gateway.GetLog()));

app.Run();
