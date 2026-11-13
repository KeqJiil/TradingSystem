using Messaging.Abstractions;
using Order.Domain;
using Order.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class ResultToOutcomeTests
{
    [Fact]
    public void ToConsumeOutcome_Success_ReturnsSuccess()
    {
        var outcome = Result.Success().ToConsumeOutcome();

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Null(outcome.Reason);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.Violation)]
    public void ToConsumeOutcome_NonRetryableError_ReturnsDeadLetterWithErrorMessage(ErrorType type)
    {
        var outcome = Result.Failure(new Error { Type = type, Message = "invalid" }).ToConsumeOutcome();

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        Assert.Equal("invalid", outcome.Reason);
    }

    [Theory]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.NotFound)]
    public void ToConsumeOutcome_RetryableError_ReturnsRetryWithErrorMessage(ErrorType type)
    {
        var outcome = Result.Failure(new Error { Type = type, Message = "failed" }).ToConsumeOutcome();

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
        Assert.Equal("failed", outcome.Reason);
    }
}
