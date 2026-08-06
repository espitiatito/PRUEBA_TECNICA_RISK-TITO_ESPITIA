using System.Text.Json;
using Facturacion.Api.Dtos;

namespace Facturacion.Api.Tests;

public class ApiResponseTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Fail_serializa_con_success_en_false_y_el_mensaje()
    {
        var json = JsonSerializer.Serialize(ApiResponse.Fail("fondos insuficientes"), Options);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("fondos insuficientes", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Ok_lleva_el_dato_en_data()
    {
        var respuesta = ApiResponse.Ok("el cobro se realizo correctamente", new { referencia = "gw_123456" });

        var json = JsonSerializer.Serialize(respuesta, Options);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("gw_123456", doc.RootElement.GetProperty("data").GetProperty("referencia").GetString());
    }
}
