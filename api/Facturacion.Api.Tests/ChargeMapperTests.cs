using Facturacion.Api.Domain;
using Facturacion.Api.Dtos;

namespace Facturacion.Api.Tests;

public class ChargeMapperTests
{
    private static Charge NuevoCobro()
    {
        var id = Guid.NewGuid();
        return new Charge
        {
            Id = id,
            CustomerId = "CUS-12345",
            CustomerName = "Ángela Muñoz Peñaloza",
            CustomerEmail = "angela.munoz@correo.co",
            SubscriptionId = "SUB-6188",
            ExternalReference = "REF-2026-0975",
            Amount = 149900.50m,
            Currency = "COP",
            DueDate = new DateTime(2026, 7, 28, 16, 45, 0),
            CreatedAt = new DateTime(2026, 7, 28, 2, 0, 0),
            Status = ChargeStatuses.Failed,
            FailureReason = FailureReasons.FondosInsuficientes,
            AttemptCount = 2,
            Attempts =
            {
                new ChargeAttempt
                {
                    Id = Guid.NewGuid(),
                    ChargeId = id,
                    AttemptedAt = new DateTime(2026, 7, 28, 17, 45, 0),
                    TriggeredBy = TriggeredBy.CobroNocturno,
                    Succeeded = false,
                    GatewayMessage = "fondos insuficientes"
                },
                new ChargeAttempt
                {
                    Id = Guid.NewGuid(),
                    ChargeId = id,
                    AttemptedAt = new DateTime(2026, 7, 29, 12, 5, 0),
                    TriggeredBy = TriggeredBy.Operaciones,
                    Succeeded = false,
                    GatewayMessage = "fondos insuficientes"
                }
            }
        };
    }

    [Fact]
    public void ToListItem_copia_los_datos_de_la_fila()
    {
        var cobro = NuevoCobro();

        var dto = ChargeMapper.ToListItem(cobro);

        Assert.Equal(cobro.Id, dto.Id);
        Assert.Equal("Ángela Muñoz Peñaloza", dto.CustomerName);
        Assert.Equal("REF-2026-0975", dto.ExternalReference);
        Assert.Equal(149900.50m, dto.Amount);
        Assert.Equal("COP", dto.Currency);
        Assert.Equal(FailureReasons.FondosInsuficientes, dto.FailureReason);
        Assert.Equal(2, dto.AttemptCount);
    }

    [Fact]
    public void ToDetail_devuelve_los_intentos_del_mas_reciente_al_mas_viejo()
    {
        var cobro = NuevoCobro();

        var dto = ChargeMapper.ToDetail(cobro);

        Assert.Equal(2, dto.Attempts.Count);
        Assert.Equal(new DateTime(2026, 7, 29, 12, 5, 0), dto.Attempts[0].AttemptedAt);
        Assert.Equal(TriggeredBy.Operaciones, dto.Attempts[0].TriggeredBy);
        Assert.Equal(new DateTime(2026, 7, 28, 17, 45, 0), dto.Attempts[1].AttemptedAt);
    }
}
