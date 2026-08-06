using Facturacion.Api.Domain;

namespace Facturacion.Api.Data;

public static class DbSeeder
{
    private static readonly (string Name, string Email)[] Customers =
    {
        ("Carlos Andrés Bermúdez", "carlos.bermudez@correo.co"),
        ("Laura Sofía Restrepo", "laura.restrepo@correo.co"),
        ("Juan Pablo Gutiérrez", "jp.gutierrez@correo.co"),
        ("María Fernanda Ospina", "mf.ospina@correo.co"),
        ("Diego Alejandro Cárdenas", "diego.cardenas@correo.co"),
        ("Valentina Ríos", "valentina.rios@correo.co"),
        ("Andrés Felipe Zapata", "af.zapata@correo.co"),
        ("Catalina Hernández", "catalina.hernandez@correo.co"),
        ("Santiago Villalba", "santiago.villalba@correo.co"),
        ("Daniela Quintero", "daniela.quintero@correo.co"),
        ("Jorge Iván Muñoz", "jorge.munoz@correo.co"),
        ("Paula Andrea Salazar", "paula.salazar@correo.co"),
        ("Sebastián Peña", "sebastian.pena@correo.co"),
        ("Lina Marcela Torres", "lina.torres@correo.co"),
        ("Óscar Mauricio Rincón", "oscar.rincon@correo.co"),
        ("Natalia Betancur", "natalia.betancur@correo.co"),
        ("Camilo Ernesto Pardo", "camilo.pardo@correo.co"),
        ("Angélica Duarte", "angelica.duarte@correo.co"),
        ("Ricardo Núñez", "ricardo.nunez@correo.co"),
        ("Manuela Escobar", "manuela.escobar@correo.co"),
        ("Julián Camilo Arias", "julian.arias@correo.co"),
        ("Sara Isabel Mejía", "sara.mejia@correo.co"),
        ("Mauricio Lozano", "mauricio.lozano@correo.co"),
        ("Tatiana Galvis", "tatiana.galvis@correo.co"),
        ("Felipe Antonio Cifuentes", "felipe.cifuentes@correo.co"),
        ("Carolina Amaya", "carolina.amaya@correo.co"),
        ("Néstor Julio Beltrán", "nestor.beltran@correo.co"),
        ("Alejandra Pineda", "alejandra.pineda@correo.co"),
        ("Esteban Cadena", "esteban.cadena@correo.co"),
        ("Diana Marcela Rueda", "diana.rueda@correo.co"),
        ("Fabián Correa", "fabian.correa@correo.co"),
        ("Luisa Fernanda Ávila", "luisa.avila@correo.co"),
        ("Hernán Darío Agudelo", "hernan.agudelo@correo.co"),
        ("Melissa Vanegas", "melissa.vanegas@correo.co"),
        ("Cristian Camilo Rojas", "cristian.rojas@correo.co"),
        ("Adriana Milena Suárez", "adriana.suarez@correo.co"),
        ("Wilson Alberto Marín", "wilson.marin@correo.co"),
        ("Juliana Castaño", "juliana.castano@correo.co"),
        ("Mateo Sanmiguel", "mateo.sanmiguel@correo.co"),
        ("Gloria Inés Cuervo", "gloria.cuervo@correo.co")
    };

    private static readonly decimal[] Amounts =
    {
        29900m, 39900m, 45900m, 49900m, 59900m, 69900m, 79900m, 89900m,
        119900m, 129900m, 159900m, 189900m, 219900m, 249900m, 299900m,
        349900m, 420000m, 490000m, 560000m, 620000m, 750000m, 890000m
    };

    private static readonly string[] GatewayFailureMessages =
    {
        "la tarjeta fue rechazada por el emisor",
        "fondos insuficientes",
        "la tarjeta esta vencida",
        "la pasarela tardo demasiado",
        "error interno de la pasarela"
    };

    public static void Seed(FacturacionDbContext db)
    {
        if (db.Charges.Any())
        {
            return;
        }

        var rnd = new Random(20260806);
        var today = DateTime.Now.Date;
        var charges = new List<Charge>();

        // el volumen del ultimo mes y medio
        var plan = new List<string>();
        for (var i = 0; i < 77; i++) plan.Add(ChargeStatuses.Failed);
        for (var i = 0; i < 18; i++) plan.Add(ChargeStatuses.Pending);
        for (var i = 0; i < 10; i++) plan.Add(ChargeStatuses.Paid);
        for (var i = 0; i < 5; i++) plan.Add(ChargeStatuses.Canceled);

        // mezcla para que el listado no salga agrupado por estado
        for (var i = plan.Count - 1; i > 0; i--)
        {
            var j = rnd.Next(i + 1);
            (plan[i], plan[j]) = (plan[j], plan[i]);
        }

        var consecutive = 1000;
        foreach (var status in plan)
        {
            consecutive++;
            var customer = Customers[rnd.Next(Customers.Length)];
            var dueDate = today.AddDays(-rnd.Next(0, 45)).AddHours(rnd.Next(6, 22)).AddMinutes(rnd.Next(0, 60));

            var charge = new Charge
            {
                Id = Guid.NewGuid(),
                CustomerId = "CUS-" + rnd.Next(10000, 99999),
                CustomerName = customer.Name,
                CustomerEmail = customer.Email,
                SubscriptionId = "SUB-" + rnd.Next(3000, 9999),
                ExternalReference = "REF-2026-" + consecutive,
                Amount = Amounts[rnd.Next(Amounts.Length)],
                Currency = "COP",
                DueDate = dueDate,
                CreatedAt = DateTime.Now,
                Status = status
            };

            if (status == ChargeStatuses.Failed)
            {
                var attempts = rnd.Next(1, 4);
                var reason = FailureReasons.All[rnd.Next(FailureReasons.All.Length)];
                charge.AttemptCount = attempts;
                charge.FailureReason = reason;
                for (var i = 0; i < attempts; i++)
                {
                    charge.Attempts.Add(NewAttempt(charge, dueDate.AddHours(i * 8 + 1), TriggeredBy.CobroNocturno, false, reason));
                }
            }
            else if (status == ChargeStatuses.Paid)
            {
                charge.AttemptCount = 1;
                charge.Attempts.Add(NewAttempt(charge, dueDate.AddMinutes(12), TriggeredBy.CobroNocturno, true, null, rnd));
            }
            else if (status == ChargeStatuses.Canceled)
            {
                charge.AttemptCount = 1;
                charge.FailureReason = FailureReasons.TarjetaRechazada;
                charge.Attempts.Add(NewAttempt(charge, dueDate.AddMinutes(20), TriggeredBy.CobroNocturno, false, FailureReasons.TarjetaRechazada));
            }

            charges.Add(charge);
        }

        // casos que veniamos arrastrando de la migracion de la pasarela anterior
        charges.AddRange(LegacyCharges(today, rnd));

        db.Charges.AddRange(charges);
        db.SaveChanges();
    }

    private static IEnumerable<Charge> LegacyCharges(DateTime today, Random rnd)
    {
        var tresIntentos = new[]
        {
            ("Gustavo Adolfo Ramírez", "gustavo.ramirez@correo.co", "CUS-40118", "SUB-5521", "REF-2026-0881", 249900m, FailureReasons.FondosInsuficientes, 11),
            ("Sandra Milena Cortés", "sandra.cortes@correo.co", "CUS-40233", "SUB-5602", "REF-2026-0902", 129900m, FailureReasons.TarjetaRechazada, 19),
            ("Álvaro Enrique Solano", "alvaro.solano@correo.co", "CUS-40507", "SUB-5744", "REF-2026-0918", 620000m, FailureReasons.ErrorInterno, 27)
        };

        foreach (var (name, email, customerId, subscriptionId, reference, amount, reason, daysAgo) in tresIntentos)
        {
            var due = today.AddDays(-daysAgo).AddHours(9).AddMinutes(30);
            var charge = new Charge
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CustomerName = name,
                CustomerEmail = email,
                SubscriptionId = subscriptionId,
                ExternalReference = reference,
                Amount = amount,
                Currency = "COP",
                DueDate = due,
                CreatedAt = DateTime.Now,
                Status = ChargeStatuses.Failed,
                FailureReason = reason,
                AttemptCount = 3
            };
            charge.Attempts.Add(NewAttempt(charge, due.AddHours(1), TriggeredBy.CobroNocturno, false, reason));
            charge.Attempts.Add(NewAttempt(charge, due.AddHours(9), TriggeredBy.CobroNocturno, false, reason));
            charge.Attempts.Add(NewAttempt(charge, due.AddHours(26), TriggeredBy.Operaciones, false, reason));
            yield return charge;
        }

        var vencidas = new[]
        {
            ("Rubén Darío Chaparro", "ruben.chaparro@correo.co", "CUS-41190", "SUB-5810", "REF-2026-0931", 89900m, 8),
            ("Yenny Paola Bohórquez", "yenny.bohorquez@correo.co", "CUS-41244", "SUB-5877", "REF-2026-0944", 349900m, 14)
        };

        foreach (var (name, email, customerId, subscriptionId, reference, amount, daysAgo) in vencidas)
        {
            var due = today.AddDays(-daysAgo).AddHours(14);
            var charge = new Charge
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CustomerName = name,
                CustomerEmail = email,
                SubscriptionId = subscriptionId,
                ExternalReference = reference,
                Amount = amount,
                Currency = "COP",
                DueDate = due,
                CreatedAt = DateTime.Now,
                Status = ChargeStatuses.Failed,
                FailureReason = FailureReasons.TarjetaVencida,
                AttemptCount = 2
            };
            charge.Attempts.Add(NewAttempt(charge, due.AddHours(1), TriggeredBy.CobroNocturno, false, FailureReasons.TarjetaVencida));
            charge.Attempts.Add(NewAttempt(charge, due.AddHours(25), TriggeredBy.CobroNocturno, false, FailureReasons.TarjetaVencida));
            yield return charge;
        }

        var anulado = new Charge
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUS-41902",
            CustomerName = "Héctor Fabio Quiñones",
            CustomerEmail = "hector.quinones@correo.co",
            SubscriptionId = "SUB-5921",
            ExternalReference = "REF-2026-0957",
            Amount = 59900m,
            Currency = "COP",
            DueDate = today.AddDays(-6).AddHours(11),
            CreatedAt = DateTime.Now,
            Status = ChargeStatuses.Canceled,
            FailureReason = FailureReasons.TarjetaRechazada,
            AttemptCount = 1
        };
        anulado.Attempts.Add(NewAttempt(anulado, anulado.DueDate.AddMinutes(40), TriggeredBy.Operaciones, false, FailureReasons.TarjetaRechazada));
        yield return anulado;

        var reintentoManual = today.AddDays(-22).AddHours(10).AddMinutes(14);

        var primero = new Charge
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUS-42010",
            CustomerName = "Nelson Eduardo Piedrahíta",
            CustomerEmail = "nelson.piedrahita@correo.co",
            SubscriptionId = "SUB-6044",
            ExternalReference = "REF-2026-0417",
            Amount = 189900m,
            Currency = "COP",
            DueDate = today.AddDays(-22).AddHours(8),
            CreatedAt = DateTime.Now,
            Status = "Paid",
            AttemptCount = 1
        };
        primero.Attempts.Add(NewAttempt(primero, reintentoManual, TriggeredBy.Operaciones, true, null, rnd));
        yield return primero;

        var segundo = new Charge
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUS-42010",
            CustomerName = "Nelson Eduardo Piedrahíta",
            CustomerEmail = "nelson.piedrahita@correo.co",
            SubscriptionId = "SUB-6044",
            ExternalReference = "REF-2026-0417",
            Amount = 189900m,
            Currency = "COP",
            DueDate = today.AddDays(-22).AddHours(8),
            CreatedAt = DateTime.Now,
            Status = "Paid",
            AttemptCount = 1
        };
        segundo.Attempts.Add(NewAttempt(segundo, reintentoManual.AddMinutes(7), TriggeredBy.Operaciones, true, null, rnd));
        yield return segundo;

        var medianoche = new Charge
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUS-42311",
            CustomerName = "Liliana Marcela Ochoa",
            CustomerEmail = "liliana.ochoa@correo.co",
            SubscriptionId = "SUB-6120",
            ExternalReference = "REF-2026-0968",
            Amount = 79900m,
            Currency = "COP",
            DueDate = today.AddDays(-3),
            CreatedAt = DateTime.Now,
            Status = ChargeStatuses.Failed,
            FailureReason = FailureReasons.TimeoutPasarela,
            AttemptCount = 1
        };
        medianoche.Attempts.Add(NewAttempt(medianoche, medianoche.DueDate.AddMinutes(3), TriggeredBy.CobroNocturno, false, FailureReasons.TimeoutPasarela));
        yield return medianoche;

        var conDecimales = new Charge
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUS-42488",
            CustomerName = "Ángela Muñoz Peñaloza",
            CustomerEmail = "angela.munoz@correo.co",
            SubscriptionId = "SUB-6188",
            ExternalReference = "REF-2026-0975",
            Amount = 149900.50m,
            Currency = "COP",
            DueDate = today.AddDays(-9).AddHours(16).AddMinutes(45),
            CreatedAt = DateTime.Now,
            Status = ChargeStatuses.Failed,
            FailureReason = FailureReasons.FondosInsuficientes,
            AttemptCount = 2
        };
        conDecimales.Attempts.Add(NewAttempt(conDecimales, conDecimales.DueDate.AddHours(1), TriggeredBy.CobroNocturno, false, FailureReasons.FondosInsuficientes));
        conDecimales.Attempts.Add(NewAttempt(conDecimales, conDecimales.DueDate.AddHours(20), TriggeredBy.CobroNocturno, false, FailureReasons.FondosInsuficientes));
        yield return conDecimales;

        var otroDecimal = new Charge
        {
            Id = Guid.NewGuid(),
            CustomerId = "CUS-42501",
            CustomerName = "Iván Camilo Bustamante",
            CustomerEmail = "ivan.bustamante@correo.co",
            SubscriptionId = "SUB-6203",
            ExternalReference = "REF-2026-0981",
            Amount = 89950.75m,
            Currency = "COP",
            DueDate = today.AddDays(-17).AddHours(19).AddMinutes(5),
            CreatedAt = DateTime.Now,
            Status = ChargeStatuses.Failed,
            FailureReason = FailureReasons.TarjetaRechazada,
            AttemptCount = 1
        };
        otroDecimal.Attempts.Add(NewAttempt(otroDecimal, otroDecimal.DueDate.AddMinutes(50), TriggeredBy.CobroNocturno, false, FailureReasons.TarjetaRechazada));
        yield return otroDecimal;
    }

    private static ChargeAttempt NewAttempt(Charge charge, DateTime attemptedAt, string triggeredBy, bool succeeded, string? reason, Random? rnd = null)
    {
        var message = succeeded
            ? "cobro aprobado"
            : GatewayFailureMessages[Array.IndexOf(FailureReasons.All, reason ?? FailureReasons.ErrorInterno)];

        return new ChargeAttempt
        {
            Id = Guid.NewGuid(),
            ChargeId = charge.Id,
            AttemptedAt = attemptedAt,
            TriggeredBy = triggeredBy,
            Succeeded = succeeded,
            GatewayMessage = message,
            GatewayReference = succeeded ? "gw_" + (rnd?.Next(100000, 999999) ?? 100000) : null
        };
    }
}
