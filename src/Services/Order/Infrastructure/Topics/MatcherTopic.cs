namespace Order.Infrastructure.Topics;

public enum MatcherTopic
{
    ExecutionReports
}

public static class MatcherTopicExtensions
{
    public static string Name(this MatcherTopic topic) => topic switch
    {
        MatcherTopic.ExecutionReports => "matcher-execution-reports-topic",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
