using OneId.Server.Application.Common;

namespace OneId.Server.UnitTests.Application.Common;

public class TenantContextTests
{
    [Fact]
    public void TenantId_ThrowsInvalidOperationException_WhenNotInitialized()
    {
        var ctx = new TenantContext();

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.TenantId);
        Assert.Equal(
            "Tenant context not initialized — check middleware registration order in Program.cs",
            ex.Message);
    }

    [Fact]
    public void IsInitialized_ReturnsFalse_BeforeInitialize()
    {
        var ctx = new TenantContext();

        Assert.False(ctx.IsInitialized);
    }

    [Fact]
    public void TenantId_ReturnsCorrectGuid_AfterInitialize()
    {
        var ctx = new TenantContext();
        var expected = Guid.NewGuid();

        ctx.Initialize(expected);

        Assert.Equal(expected, ctx.TenantId);
    }

    [Fact]
    public void IsInitialized_ReturnsTrue_AfterInitialize()
    {
        var ctx = new TenantContext();

        ctx.Initialize(Guid.NewGuid());

        Assert.True(ctx.IsInitialized);
    }

    [Fact]
    public void Initialize_ThrowsArgumentException_WhenGuidIsEmpty()
    {
        var ctx = new TenantContext();

        var ex = Assert.Throws<ArgumentException>(() => ctx.Initialize(Guid.Empty));
        Assert.Equal("tenantId", ex.ParamName);
    }

    [Fact]
    public void Initialize_ThrowsInvalidOperationException_WhenCalledTwice()
    {
        var ctx = new TenantContext();
        ctx.Initialize(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => ctx.Initialize(Guid.NewGuid()));
    }
}
