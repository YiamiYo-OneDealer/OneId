using Microsoft.Extensions.Logging;
using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Email;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DEV EMAIL] To: {To} | Subject: {Subject} | Body: {Body}",
            to, subject, body);
        return Task.CompletedTask;
    }
}
