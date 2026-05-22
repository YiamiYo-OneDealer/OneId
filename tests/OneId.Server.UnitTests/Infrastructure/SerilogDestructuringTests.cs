using Serilog;
using Serilog.Core;
using Serilog.Events;
using OneId.Server.Infrastructure.Logging;

namespace OneId.Server.Tests.Infrastructure;

[Collection("Serilog")]
public class SerilogDestructuringTests
{
    private static (ILogger logger, List<LogEvent> events) CreateRedactingLogger()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataRedactionEnricher>()
            .WriteTo.Sink(new CollectingLogEventSink(events))
            .CreateLogger();
        return (logger, events);
    }

    [Fact]
    public void Password_IsRedacted_AndNotPresentAsPlaintext()
    {
        var (logger, events) = CreateRedactingLogger();

        logger.Information("Login attempt for {Email} with {Password}", "user@test.com", "SecretPassword123!");

        var evt = events.Single();
        var passwordProp = evt.Properties["Password"].ToString().Trim('"');
        Assert.Equal("[Redacted]", passwordProp);
        Assert.DoesNotContain("SecretPassword123!", evt.RenderMessage());
    }

    [Fact]
    public void AuthorizationBearerToken_IsRedacted_AndNotPresentAsPlaintext()
    {
        var (logger, events) = CreateRedactingLogger();

        logger.Information("Incoming request with {AuthorizationHeader}", "Bearer eyJhbGciOiJSUzI1NiJ9.test.signature");

        var evt = events.Single();
        var headerProp = evt.Properties["AuthorizationHeader"].ToString().Trim('"');
        Assert.Equal("[Redacted]", headerProp);
        Assert.DoesNotContain("Bearer", evt.RenderMessage());
        Assert.DoesNotContain("eyJhbGciOiJSUzI1NiJ9", evt.RenderMessage());
    }

    [Fact]
    public void ClientSecret_IsRedacted_AndNotPresentAsPlaintext()
    {
        var (logger, events) = CreateRedactingLogger();

        logger.Information("Client {ClientId} authenticated with {ClientSecret}", "my-client-id", "super-secret-client-value");

        var evt = events.Single();
        var secretProp = evt.Properties["ClientSecret"].ToString().Trim('"');
        Assert.Equal("[Redacted]", secretProp);
        Assert.DoesNotContain("super-secret-client-value", evt.RenderMessage());
    }
}

internal sealed class CollectingLogEventSink(List<LogEvent> events) : ILogEventSink
{
    public void Emit(LogEvent logEvent) => events.Add(logEvent);
}
