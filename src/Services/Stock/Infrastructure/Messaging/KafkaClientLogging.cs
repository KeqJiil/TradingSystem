using Confluent.Kafka;

namespace Stock.Infrastructure.Messaging;

internal static class KafkaClientLogging
{
    public static void LogError(ILogger logger, string clientId, Error error)
    {
        logger.Log(error.IsFatal ? LogLevel.Critical : LogLevel.Warning,
            "Kafka client {ClientId} error {Code}: {Reason}", clientId, error.Code, error.Reason);
    }

    public static void LogMessage(ILogger logger, LogMessage message)
    {
        logger.Log(ToLogLevel(message.Level), "librdkafka {ClientName} [{Facility}]: {Message}",
            message.Name, message.Facility, message.Message);
    }

    public static void LogCommitted(ILogger logger, string clientId, CommittedOffsets committed)
    {
        if (committed.Error.IsError)
        {
            logger.LogWarning("Kafka consumer {ClientId} failed to commit offsets: {Reason}",
                clientId, committed.Error.Reason);
            return;
        }

        foreach (var offset in committed.Offsets.Where(o => o.Error.IsError))
            logger.LogWarning("Kafka consumer {ClientId} failed to commit {TopicPartitionOffset}: {Reason}",
                clientId, offset.TopicPartitionOffset, offset.Error.Reason);
    }

    private static LogLevel ToLogLevel(SyslogLevel level)
    {
        return level switch
        {
            SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
            SyslogLevel.Error => LogLevel.Error,
            SyslogLevel.Warning => LogLevel.Warning,
            SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
            _ => LogLevel.Debug
        };
    }
}
