using Facturacion.Api.Data;
using Facturacion.Api.Domain;
using Facturacion.Api.Dtos;
using Facturacion.Api.Gateway;
using Facturacion.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Facturacion.Api.Tests;

public class StubPaymentGateway : IPaymentGateway
{
    public Func<Guid, string, decimal, string, Task<GatewayChargeResult>>? Handler { get; set; }
    public int CallCount { get; private set; }

    public Task<GatewayChargeResult> ChargeAsync(Guid chargeId, string externalReference, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (Handler != null)
        {
            return Handler(chargeId, externalReference, amount, currency);
        }
        return Task.FromResult(new GatewayChargeResult(true, "aprobado", "gw_test_123", null));
    }

    public IReadOnlyList<GatewayLogEntry> GetLog() => Array.Empty<GatewayLogEntry>();
}

public class ChargeRetryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FacturacionDbContext _db;
    private readonly StubPaymentGateway _gateway;
    private readonly ChargeRetryService _service;

    public ChargeRetryServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FacturacionDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new FacturacionDbContext(options);
        _db.Database.EnsureCreated();

        _gateway = new StubPaymentGateway();
        _service = new ChargeRetryService(_db, _gateway, NullLogger<ChargeRetryService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Charge CreateCharge(
        string status = ChargeStatuses.Failed,
        string? failureReason = FailureReasons.FondosInsuficientes,
        int attemptCount = 1,
        decimal amount = 50000m)
    {
        var charge = new Charge
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUS-100",
            CustomerName = "Test User",
            CustomerEmail = "test@correo.co",
            SubscriptionId = "SUB-100",
            ExternalReference = "REF-TEST-01",
            Amount = amount,
            Currency = "COP",
            DueDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Status = status,
            FailureReason = failureReason,
            AttemptCount = attemptCount
        };
        _db.Charges.Add(charge);
        _db.SaveChanges();
        return charge;
    }

    [Fact]
    public async Task Regla1_Reintento_CobroYaPagado_RechazaConCodigoDistinguible()
    {
        var charge = CreateCharge(status: ChargeStatuses.Paid);

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = charge.Amount });

        Assert.False(result.Success);
        Assert.Equal(BusinessErrorCodes.AlreadyPaid, result.ErrorCode);
        Assert.Equal(0, _gateway.CallCount); // No debe llamar a pasarela
    }

    [Fact]
    public async Task Regla1_Reintento_CobroAnulado_RechazaConCodigoDistinguible()
    {
        var charge = CreateCharge(status: ChargeStatuses.Canceled);

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = charge.Amount });

        Assert.False(result.Success);
        Assert.Equal(BusinessErrorCodes.AlreadyCanceled, result.ErrorCode);
        Assert.Equal(0, _gateway.CallCount);
    }

    [Fact]
    public async Task Regla2_Reintento_Acumula3Intentos_RechazaRequiereEscalamiento()
    {
        var charge = CreateCharge(attemptCount: 3);

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = charge.Amount });

        Assert.False(result.Success);
        Assert.Equal(BusinessErrorCodes.MaxAttemptsExceeded, result.ErrorCode);
        Assert.Equal(0, _gateway.CallCount);
    }

    [Fact]
    public async Task Regla3_Reintento_TarjetaVencida_RechazaActualizarMedioDePago()
    {
        var charge = CreateCharge(failureReason: FailureReasons.TarjetaVencida, attemptCount: 1);

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = charge.Amount });

        Assert.False(result.Success);
        Assert.Equal(BusinessErrorCodes.CardExpired, result.ErrorCode);
        Assert.Equal(0, _gateway.CallCount);
    }

    [Fact]
    public async Task Regla4_Reintento_ReintentoEnCurso_Rechaza()
    {
        var charge = CreateCharge(status: ChargeStatuses.Processing);

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = charge.Amount });

        Assert.False(result.Success);
        Assert.Equal(BusinessErrorCodes.RetryInProgress, result.ErrorCode);
        Assert.Equal(0, _gateway.CallCount);
    }

    [Fact]
    public async Task Regla5_Reintento_MontoNoCoincide_Rechaza()
    {
        var charge = CreateCharge(amount: 50000m);

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = 99999m });

        Assert.False(result.Success);
        Assert.Equal(BusinessErrorCodes.AmountMismatch, result.ErrorCode);
        Assert.Equal(0, _gateway.CallCount);
    }

    [Fact]
    public async Task Reintento_Exitoso_ActualizaEstadoPagadoEIncrementaIntentos()
    {
        var charge = CreateCharge(amount: 45900m, attemptCount: 1);
        _gateway.Handler = (id, @ref, amount, curr) =>
            Task.FromResult(new GatewayChargeResult(true, "aprobado", "gw_ok_999", null));

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = 45900m });

        Assert.True(result.Success);
        Assert.Equal(1, _gateway.CallCount);

        var updated = await _db.Charges.Include(c => c.Attempts).FirstAsync(c => c.Id == charge.Id);
        Assert.Equal(ChargeStatuses.Paid, updated.Status);
        Assert.Null(updated.FailureReason);
        Assert.Equal(2, updated.AttemptCount);
        Assert.Single(updated.Attempts);
        Assert.True(updated.Attempts[0].Succeeded);
        Assert.Equal("gw_ok_999", updated.Attempts[0].GatewayReference);
    }

    [Fact]
    public async Task Reintento_GatewayTimeout_CapturaExcepcionYRegistraIntento()
    {
        var charge = CreateCharge(amount: 79900m, attemptCount: 1);
        _gateway.Handler = (id, @ref, amount, curr) =>
            throw new GatewayTimeoutException("la pasarela tardo demasiado");

        var result = await _service.RetryAsync(charge.Id, new RetryChargeRequest { Amount = 79900m });

        Assert.False(result.Success);
        Assert.Equal(FailureReasons.TimeoutPasarela, result.ErrorCode);

        var updated = await _db.Charges.Include(c => c.Attempts).FirstAsync(c => c.Id == charge.Id);
        Assert.Equal(ChargeStatuses.Failed, updated.Status);
        Assert.Equal(FailureReasons.TimeoutPasarela, updated.FailureReason);
        Assert.Equal(2, updated.AttemptCount);
        Assert.Single(updated.Attempts);
        Assert.False(updated.Attempts[0].Succeeded);
    }
}

