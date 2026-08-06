using Facturacion.Api.Domain;

namespace Facturacion.Api.Gateway;

// ---------------------------------------------------------------------------
// SIMULACION de la pasarela de pagos. Reemplaza al proveedor real mientras
// esperamos las credenciales de sandbox. Reproduce la latencia, los rechazos
// y los timeouts que vemos en produccion, con semilla fija para que el
// comportamiento sea repetible entre corridas.
//
// NO MODIFICAR esta clase: es el sustituto del sistema externo, no parte del
// dominio. Cualquier arreglo va del lado de quien la consume.
// ---------------------------------------------------------------------------
public class FakePaymentGateway : IPaymentGateway
{
    private const int LatencyMs = 1500;

    private readonly Random _random = new Random(20260806);
    private readonly List<GatewayLogEntry> _log = new();
    private readonly object _sync = new();

    private readonly ILogger<FakePaymentGateway> _logger;

    public FakePaymentGateway(ILogger<FakePaymentGateway> logger)
    {
        _logger = logger;
    }

    public async Task<GatewayChargeResult> ChargeAsync(Guid chargeId, string externalReference, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        double roll;
        string reason;
        string reference;

        lock (_sync)
        {
            roll = _random.NextDouble();
            reason = FailureReasons.All[_random.Next(FailureReasons.All.Length)];
            reference = "gw_" + _random.Next(100000, 999999).ToString();
        }

        _logger.LogInformation("pasarela: cobrando {Amount} {Currency} de {Reference}", amount, currency, externalReference);

        await Task.Delay(LatencyMs, cancellationToken);

        if (roll < 0.10)
        {
            Record(chargeId, externalReference, amount, "timeout", null);
            throw new GatewayTimeoutException($"la pasarela no respondio a tiempo para {externalReference}");
        }

        if (roll < 0.40)
        {
            Record(chargeId, externalReference, amount, "rechazado:" + reason, null);
            return new GatewayChargeResult(false, DescribeReason(reason), null, reason);
        }

        Record(chargeId, externalReference, amount, "aprobado", reference);
        return new GatewayChargeResult(true, "cobro aprobado", reference, null);
    }

    public IReadOnlyList<GatewayLogEntry> GetLog()
    {
        lock (_sync)
        {
            return _log.ToList();
        }
    }

    private void Record(Guid chargeId, string externalReference, decimal amount, string outcome, string? reference)
    {
        lock (_sync)
        {
            _log.Add(new GatewayLogEntry(DateTime.UtcNow, chargeId, externalReference, amount, outcome, reference));
        }
    }

    private static string DescribeReason(string reason) => reason switch
    {
        FailureReasons.TarjetaRechazada => "la tarjeta fue rechazada por el emisor",
        FailureReasons.FondosInsuficientes => "fondos insuficientes",
        FailureReasons.TarjetaVencida => "la tarjeta esta vencida",
        FailureReasons.TimeoutPasarela => "la pasarela tardo demasiado",
        _ => "error interno de la pasarela"
    };
}
