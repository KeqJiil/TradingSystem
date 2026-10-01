namespace Messaging.Abstractions;

public static class MessagingHeaders
{
    public const string MessageId = "message-id";
    public const string EventType = "event-type";
    public const string CorrelationId = "correlation-id";
    public const string CausationId = "causation-id";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";

    public const string Attempt = "attempt";
    public const string FirstFailureAt = "first-failure-at";
    public const string OriginalTopic = "original-topic";

    public const string ExceptionType = "exception-type";
    public const string ExceptionMessage = "exception-message";
    public const string ExceptionStackTrace = "exception-stacktrace";
}