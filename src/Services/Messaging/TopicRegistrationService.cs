using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging;

internal class TopicRegistrationService(IOptions<MessagingOptions> options, ILogger<TopicRegistrationService> logger, KafkaTopicRegister topicRegister)
    : IHostedService
{
    private readonly MessagingOptions _messagingOptions = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_messagingOptions.EnableTopicRegistration)
        {
            logger.LogInformation("Topic registration is disabled. Skipping topic registration.");
            return;
        }

        logger.LogInformation("Starting topic registration...");
        await topicRegister.RegisterTopicsAsync(cancellationToken);
        logger.LogInformation("Topic registration completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}