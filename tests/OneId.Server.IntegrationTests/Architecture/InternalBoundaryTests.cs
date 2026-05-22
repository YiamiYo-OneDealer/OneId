using NetArchTest.Rules;

namespace OneId.Server.IntegrationTests.Architecture
{
    public class InternalBoundaryTests
    {
        [Fact]
        public void InternalAdminContext_MustOnlyBeUsedInsideApplicationInternal()
        {
            var assembly = typeof(OneId.Server.Application.Common.InternalAdminContext).Assembly;

            var result = Types.InAssembly(assembly)
                .That()
                .DoNotResideInNamespaceStartingWith("OneId.Server.Application.Internal")
                .ShouldNot()
                .HaveDependencyOnAny("OneId.Server.Application.Common.InternalAdminContext")
                .GetResult();

            if (!result.IsSuccessful)
            {
                var violators = string.Join(", ", result.FailingTypes.Select(t => t.FullName));
                Assert.Fail($"InternalAdminContext leaked outside Application.Internal: {violators}");
            }
        }

        [Fact]
        public void ViolatingController_Using_InternalAdminContext_IsDetectedByRule()
        {
            // The test assembly itself contains a deliberate violation (ViolatingTenantController below).
            var testAssembly = System.Reflection.Assembly.GetExecutingAssembly();

            var result = Types.InAssembly(testAssembly)
                .That()
                .DoNotResideInNamespaceStartingWith("OneId.Server.Application.Internal")
                .ShouldNot()
                .HaveDependencyOnAny("OneId.Server.Application.Common.InternalAdminContext")
                .GetResult();

            // MUST fail — test assembly contains ViolatingTenantController
            Assert.False(result.IsSuccessful,
                "Expected the rule to fail because ViolatingTenantController depends on InternalAdminContext " +
                "from outside Application.Internal — rule has no teeth if this passes.");
        }

        [Fact]
        public void IMemoryCache_MustOnlyBeReferencedInsideInfrastructureCaching()
        {
            var assembly = typeof(OneId.Server.Application.Common.ICacheService).Assembly;

            var result = Types.InAssembly(assembly)
                .That()
                .DoNotResideInNamespaceStartingWith("OneId.Server.Infrastructure.Caching")
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.Extensions.Caching.Memory.IMemoryCache")
                .GetResult();

            if (!result.IsSuccessful)
            {
                var violators = string.Join(", ", result.FailingTypes.Select(t => t.FullName));
                Assert.Fail($"IMemoryCache leaked outside Infrastructure.Caching: {violators}");
            }
        }
    }
}

// Deliberate violation fixture — proves InternalAdminContext boundary rule has teeth.
// Lives in the TEST assembly in a wrong namespace so Test 2 (ViolatingController_Using_InternalAdminContext_IsDetectedByRule) detects it.
namespace OneId.Server.Application.Tenant.Controllers.TestViolation
{
    internal sealed class ViolatingTenantController(OneId.Server.Application.Common.InternalAdminContext context)
    {
        private readonly OneId.Server.Application.Common.InternalAdminContext _context = context;
    }
}
