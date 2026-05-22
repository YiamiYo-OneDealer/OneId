using Xunit;

namespace OneId.Server.Tests.Infrastructure;

public class DevSigningKeyStabilityTest
{
    [Fact(Skip = "Wired in Epic 2 — remove Skip when OpenIddict signing key is configured")]
    public async Task SigningKey_IsFileBased_AndSurvivesAppRestart()
    {
        // Epic 2 Story 2.1 must remove this Skip attribute and make this test pass.
        // Test must assert:
        //   1. Signing key file exists at: keys/dev-signing.key
        //   2. A token signed before a WebApplicationFactory restart validates
        //      successfully after restart (proves key is file-based, not ephemeral)
        await Task.CompletedTask;
        Assert.Fail("DevSigningKeyStabilityTest not yet wired — implement in Epic 2 Story 2.1");
    }
}
