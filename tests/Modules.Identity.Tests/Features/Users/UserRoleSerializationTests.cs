using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Tests.Features.Users;

/// <summary>
/// Program.cs registers JsonStringEnumConverter process-wide via
/// ConfigureHttpJsonOptions. That registration was added for PaymentMethod
/// but it is process-wide, so it also widened the pre-existing Identity
/// endpoints' contract: UserRole now binds/serializes as "Staff" etc.
/// instead of its underlying numeric value. These tests mirror the host's
/// serializer configuration to pin that boundary behavior.
/// </summary>
[TestClass]
public sealed class UserRoleSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record UserRoleHolder(UserRole Role);

    [TestMethod]
    public void UserRole_CrossesTheWire_AsString()
    {
        var json = JsonSerializer.Serialize(new UserRoleHolder(UserRole.Staff), Options);

        json.Should().Contain("\"role\":\"Staff\"");
    }

    [TestMethod]
    public void UserRole_ParsesFromWireString_BranchManager()
    {
        var holder = JsonSerializer.Deserialize<UserRoleHolder>(
            """{"role":"BranchManager"}""", Options);

        holder.Should().NotBeNull();
        holder!.Role.Should().Be(UserRole.BranchManager);
    }
}
