namespace Facturacion.Api.Domain;

public class ChargeAttempt
{
    public Guid Id { get; set; }
    public Guid ChargeId { get; set; }

    public DateTime AttemptedAt { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;

    public bool Succeeded { get; set; }
    public string? GatewayMessage { get; set; }
    public string? GatewayReference { get; set; }

    public Charge? Charge { get; set; }
}
