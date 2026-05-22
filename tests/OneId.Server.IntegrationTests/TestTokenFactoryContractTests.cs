namespace OneId.Server.IntegrationTests;

public class TestTokenFactoryContractTests
{
    [Fact(Skip = "Wired in Epic 3 — remove Skip and make this pass in the licensing middleware story")]
    public void TestTokenFactory_ClaimShape_MatchesProductionITokenClaimsEnricher()
    {
        Assert.Fail("TestTokenFactory claim shape not yet validated against production ITokenClaimsEnricher — wire in Epic 3");
    }
}
