using Facturacion.Api.Data;
using Facturacion.Api.Domain;
using Facturacion.Api.Dtos;
using Facturacion.Api.Gateway;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Api.Services;

public class ChargeRetryService
{
    private readonly FacturacionDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<ChargeRetryService> _logger;

    public ChargeRetryService(FacturacionDbContext db, IPaymentGateway gateway, ILogger<ChargeRetryService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<ApiResponse> RetryAsync(Guid chargeId, RetryChargeRequest request, CancellationToken cancellationToken = default)
    {
        var charge = await _db.Charges
            .Include(c => c.Attempts)
            .FirstOrDefaultAsync(c => c.Id == chargeId, cancellationToken);

        if (charge is null)
        {
            return ApiResponse.Fail("el cobro no existe");
        }

        var triggeredBy = string.IsNullOrWhiteSpace(request.TriggeredBy)
            ? TriggeredBy.Operaciones
            : request.TriggeredBy!;

        var attempt = new ChargeAttempt
        {
            Id = Guid.NewGuid(),
            ChargeId = charge.Id,
            AttemptedAt = DateTime.Now,
            TriggeredBy = triggeredBy
        };

        try
        {
            var result = await _gateway.ChargeAsync(
                charge.Id,
                charge.ExternalReference,
                request.Amount,
                charge.Currency,
                cancellationToken);

            attempt.Succeeded = result.Succeeded;
            attempt.GatewayMessage = result.Message;
            attempt.GatewayReference = result.Reference;

            charge.AttemptCount = charge.AttemptCount + 1;

            if (result.Succeeded)
            {
                charge.Status = "PAID";
                charge.FailureReason = null;
            }
            else
            {
                charge.FailureReason = result.FailureReason;
            }

            _db.ChargeAttempts.Add(attempt);
            await _db.SaveChangesAsync(cancellationToken);

            return result.Succeeded
                ? ApiResponse.Ok("el cobro se realizo correctamente", ChargeMapper.ToListItem(charge))
                : ApiResponse.Fail(result.Message);
        }
        catch (GatewayTimeoutException ex)
        {
            _logger.LogWarning(ex, "timeout de la pasarela para el cobro {ChargeId}", charge.Id);

            attempt.Succeeded = false;
            attempt.GatewayMessage = ex.Message;
            charge.AttemptCount = charge.AttemptCount + 1;
            charge.FailureReason = FailureReasons.TimeoutPasarela;

            _db.ChargeAttempts.Add(attempt);
            await _db.SaveChangesAsync(cancellationToken);

            return ApiResponse.Fail("la pasarela no respondio a tiempo, intenta de nuevo");
        }
    }
}
