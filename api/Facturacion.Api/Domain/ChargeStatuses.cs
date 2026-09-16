namespace Facturacion.Api.Domain;

public static class ChargeStatuses
{
    public const string Pending = "pending";
    public const string Failed = "failed";
    public const string Paid = "paid";
    public const string Canceled = "canceled";
    public const string Processing = "processing";

    public static readonly string[] All = { Pending, Failed, Paid, Canceled, Processing };
}

public static class FailureReasons
{
    public const string TarjetaRechazada = "tarjeta_rechazada";
    public const string FondosInsuficientes = "fondos_insuficientes";
    public const string TarjetaVencida = "tarjeta_vencida";
    public const string TimeoutPasarela = "timeout_pasarela";
    public const string ErrorInterno = "error_interno";

    public static readonly string[] All =
    {
        TarjetaRechazada, FondosInsuficientes, TarjetaVencida, TimeoutPasarela, ErrorInterno
    };
}

public static class TriggeredBy
{
    public const string CobroNocturno = "cobro_nocturno";
    public const string Operaciones = "operaciones";
}

public static class ChargePolicy
{
    public const int MaxRetryAttempts = 3;
}

public static class BusinessErrorCodes
{
    public const string AlreadyPaid = "ALREADY_PAID";
    public const string AlreadyCanceled = "ALREADY_CANCELED";
    public const string MaxAttemptsExceeded = "MAX_ATTEMPTS_EXCEEDED";
    public const string CardExpired = "CARD_EXPIRED";
    public const string RetryInProgress = "RETRY_IN_PROGRESS";
    public const string AmountMismatch = "AMOUNT_MISMATCH";
    public const string ChargeNotFound = "CHARGE_NOT_FOUND";
}
