namespace Facturacion.Api.Gateway;

public interface IPaymentGateway
{
    Task<GatewayChargeResult> ChargeAsync(Guid chargeId, string externalReference, decimal amount, string currency, CancellationToken cancellationToken = default);

    IReadOnlyList<GatewayLogEntry> GetLog();
}

public record GatewayChargeResult(bool Succeeded, string Message, string? Reference, string? FailureReason);

public record GatewayLogEntry(DateTime Timestamp, Guid ChargeId, string ExternalReference, decimal Amount, string Outcome, string? Reference);

public class GatewayTimeoutException : Exception
{
    public GatewayTimeoutException(string message) : base(message)
    {
    }
}
