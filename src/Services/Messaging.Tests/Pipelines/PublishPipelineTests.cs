using Xunit;

namespace Messaging.Tests.Pipelines;

public class PublishPipelineTests
{
    [Fact(Skip = "Fake terminal records args. Assert topic, key, messageId, payload and headers equal the context.")]
    public async Task RunPublish_WithoutMiddlewares_CallsTerminalWithContextValues()
    {
    }

    [Fact(Skip = "A, B, C publish probes. Assert before in registration order, terminal, after in reverse.")]
    public Task RunPublish_RunsMiddlewaresOutsideInAroundTerminal()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Middleware adds traceparent. Assert terminal receives it.")]
    public Task RunPublish_MiddlewareChangesHeaders_ChangedHeadersReachTerminal()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Middleware returns failed outcome without next. Assert terminal not called and outcome returned.")]
    public Task RunPublish_MiddlewareShortCircuits_TerminalNotCalled()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip =
        "Terminal returns failed outcome. Assert middlewares and caller see IsSuccessful false and the error message.")]
    public Task RunPublish_TerminalFails_OutcomeFlowsThroughMiddlewares()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Two scopes. Assert different middleware instances.")]
    public Task RunPublish_ScopedMiddleware_NewInstancePerScope()
    {
        return Task.CompletedTask;
    }
}