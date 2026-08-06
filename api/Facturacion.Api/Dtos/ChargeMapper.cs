using Facturacion.Api.Domain;

namespace Facturacion.Api.Dtos;

public static class ChargeMapper
{
    public static ChargeListItemDto ToListItem(Charge charge) => new ChargeListItemDto(
        charge.Id,
        charge.CustomerName,
        charge.CustomerEmail,
        charge.ExternalReference,
        charge.Amount,
        charge.Currency,
        charge.DueDate,
        charge.Status,
        charge.FailureReason,
        charge.AttemptCount);

    public static ChargeAttemptDto ToAttempt(ChargeAttempt attempt) => new ChargeAttemptDto(
        attempt.Id,
        attempt.AttemptedAt,
        attempt.TriggeredBy,
        attempt.Succeeded,
        attempt.GatewayMessage,
        attempt.GatewayReference);

    public static ChargeDetailDto ToDetail(Charge charge) => new ChargeDetailDto(
        charge.Id,
        charge.CustomerId,
        charge.CustomerName,
        charge.CustomerEmail,
        charge.SubscriptionId,
        charge.ExternalReference,
        charge.Amount,
        charge.Currency,
        charge.DueDate,
        charge.CreatedAt,
        charge.Status,
        charge.FailureReason,
        charge.AttemptCount,
        charge.Attempts
            .OrderByDescending(a => a.AttemptedAt)
            .Select(ToAttempt)
            .ToList());
}
