namespace Facturacion.Api.Domain;

public class Charge
{
    public Guid Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "COP";

    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = ChargeStatuses.Pending;
    public string? FailureReason { get; set; }
    public int AttemptCount { get; set; }

    public List<ChargeAttempt> Attempts { get; set; } = new();
}
