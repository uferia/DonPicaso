using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Modules.Sales.Features.Orders;

namespace Modules.Sales.Tests.Features.Orders;

/// <summary>
/// Program.cs registers JsonStringEnumConverter process-wide via
/// ConfigureHttpJsonOptions so PaymentMethod crosses the wire as "Cash" /
/// "Card" (matching the Angular payload types), not as its underlying
/// numeric value. These tests mirror the host's serializer configuration
/// to pin that boundary behavior.
/// </summary>
[TestClass]
public sealed class PaymentMethodSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record PaymentMethodHolder(PaymentMethod PaymentMethod);

    [TestMethod]
    public void PaymentMethod_CrossesTheWire_AsString()
    {
        var json = JsonSerializer.Serialize(new PaymentMethodHolder(PaymentMethod.Card), Options);

        json.Should().Contain("\"paymentMethod\":\"Card\"");
    }

    [TestMethod]
    public void PaymentMethod_ParsesFromWireString_Cash()
    {
        var holder = JsonSerializer.Deserialize<PaymentMethodHolder>(
            """{"paymentMethod":"Cash"}""", Options);

        holder.Should().NotBeNull();
        holder!.PaymentMethod.Should().Be(PaymentMethod.Cash);
    }
}
