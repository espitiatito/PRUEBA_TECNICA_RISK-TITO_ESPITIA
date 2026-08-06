namespace Facturacion.Api.Dtos;

public record ChargeListItemDto(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    string ExternalReference,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    string Status,
    string? FailureReason,
    int AttemptCount);

public record ChargeAttemptDto(
    Guid Id,
    DateTime AttemptedAt,
    string TriggeredBy,
    bool Succeeded,
    string? GatewayMessage,
    string? GatewayReference);

public record ChargeDetailDto(
    Guid Id,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    string SubscriptionId,
    string ExternalReference,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    DateTime CreatedAt,
    string Status,
    string? FailureReason,
    int AttemptCount,
    List<ChargeAttemptDto> Attempts);

public class RetryChargeRequest
{
    public decimal Amount { get; set; }
    public string? TriggeredBy { get; set; }
}

public record ApiResponse(bool Success, string Message, object? Data = null)
{
    public static ApiResponse Fail(string message) => new ApiResponse(false, message);

    public static ApiResponse Ok(string message, object? data = null) => new ApiResponse(true, message, data);
}
