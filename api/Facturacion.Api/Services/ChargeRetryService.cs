using System.Collections.Concurrent;
using Facturacion.Api.Data;
using Facturacion.Api.Domain;
using Facturacion.Api.Dtos;
using Facturacion.Api.Gateway;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Api.Services;

public class ChargeRetryService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ConcurrencyLocks = new();

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
        var lockSemaphore = ConcurrencyLocks.GetOrAdd(chargeId, _ => new SemaphoreSlim(1, 1));

        // Intento de adquisición no bloqueante para reintentos concurrentes en el mismo milisegundo
        var acquired = await lockSemaphore.WaitAsync(0, cancellationToken);
        if (!acquired)
        {
            _logger.LogWarning("Doble envío detectado en el mismo instante para el cobro {ChargeId}", chargeId);
            return ApiResponse.Fail("Hay un reintento en curso para ese cobro.", BusinessErrorCodes.RetryInProgress);
        }

        try
        {
            var charge = await _db.Charges
                .Include(c => c.Attempts)
                .FirstOrDefaultAsync(c => c.Id == chargeId, cancellationToken);

            if (charge is null)
            {
                return ApiResponse.Fail("El cobro no existe", BusinessErrorCodes.ChargeNotFound);
            }

            // RF-4: Regla 1 - Cobro ya en estado Pagado o Anulado
            if (charge.Status == ChargeStatuses.Paid)
            {
                return ApiResponse.Fail("El cobro ya se encuentra pagado.", BusinessErrorCodes.AlreadyPaid);
            }

            if (charge.Status == ChargeStatuses.Canceled)
            {
                return ApiResponse.Fail("El cobro se encuentra anulado.", BusinessErrorCodes.AlreadyCanceled);
            }

            // RF-4: Regla 4 - Reintento en curso
            if (charge.Status == ChargeStatuses.Processing)
            {
                return ApiResponse.Fail("Hay un reintento en curso para ese cobro.", BusinessErrorCodes.RetryInProgress);
            }

            // RF-4: Regla 2 - Ya acumula 3 intentos fallidos
            if (charge.AttemptCount >= ChargePolicy.MaxRetryAttempts)
            {
                return ApiResponse.Fail($"Ya acumula {charge.AttemptCount} intentos fallidos; requiere escalamiento manual.", BusinessErrorCodes.MaxAttemptsExceeded);
            }

            // RF-4: Regla 3 - Motivo del fallo es TarjetaVencida
            if (charge.FailureReason == FailureReasons.TarjetaVencida)
            {
                return ApiResponse.Fail("El motivo del fallo es tarjeta vencida; primero hay que actualizar el medio de pago.", BusinessErrorCodes.CardExpired);
            }

            // RF-4: Regla 5 - El monto no coincide con el del cobro original
            if (request.Amount != charge.Amount)
            {
                return ApiResponse.Fail($"El monto enviado ({request.Amount}) no coincide con el cobro original ({charge.Amount}).", BusinessErrorCodes.AmountMismatch);
            }

            // Marcado transicional a Processing para blindaje persistente en base de datos
            charge.Status = ChargeStatuses.Processing;
            await _db.SaveChangesAsync(cancellationToken);

            var triggeredBy = string.IsNullOrWhiteSpace(request.TriggeredBy)
                ? TriggeredBy.Operaciones
                : request.TriggeredBy.Trim();

            var attempt = new ChargeAttempt
            {
                Id = Guid.NewGuid(),
                ChargeId = charge.Id,
                AttemptedAt = DateTime.UtcNow,
                TriggeredBy = triggeredBy
            };

            try
            {
                var result = await _gateway.ChargeAsync(
                    charge.Id,
                    charge.ExternalReference,
                    charge.Amount,
                    charge.Currency,
                    cancellationToken);

                attempt.Succeeded = result.Succeeded;
                attempt.GatewayMessage = result.Message;
                attempt.GatewayReference = result.Reference;
                charge.AttemptCount += 1;

                if (result.Succeeded)
                {
                    charge.Status = ChargeStatuses.Paid;
                    charge.FailureReason = null;
                }
                else
                {
                    charge.Status = ChargeStatuses.Failed;
                    charge.FailureReason = result.FailureReason ?? FailureReasons.ErrorInterno;
                }

                _db.ChargeAttempts.Add(attempt);
                await _db.SaveChangesAsync(cancellationToken);

                return result.Succeeded
                    ? ApiResponse.Ok("El cobro se realizó correctamente", ChargeMapper.ToListItem(charge))
                    : ApiResponse.Fail(result.Message, result.FailureReason);
            }
            catch (GatewayTimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout de la pasarela para el cobro {ChargeId}", charge.Id);

                attempt.Succeeded = false;
                attempt.GatewayMessage = ex.Message;
                charge.AttemptCount += 1;
                charge.Status = ChargeStatuses.Failed;
                charge.FailureReason = FailureReasons.TimeoutPasarela;

                _db.ChargeAttempts.Add(attempt);
                await _db.SaveChangesAsync(cancellationToken);

                return ApiResponse.Fail("La pasarela tardó demasiado; intenta de nuevo.", FailureReasons.TimeoutPasarela);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error imprevisto al procesar cobro {ChargeId}", charge.Id);

                attempt.Succeeded = false;
                attempt.GatewayMessage = "Fallo en la comunicación con la pasarela";
                charge.AttemptCount += 1;
                charge.Status = ChargeStatuses.Failed;
                charge.FailureReason = FailureReasons.ErrorInterno;

                _db.ChargeAttempts.Add(attempt);
                await _db.SaveChangesAsync(cancellationToken);

                return ApiResponse.Fail("Error interno de la pasarela; intenta de nuevo.", FailureReasons.ErrorInterno);
            }
        }
        finally
        {
            lockSemaphore.Release();
        }
    }
}
