using Matcher.Core.Commands;
using Matcher.Core.Events;

namespace Matcher.Core;

public sealed class MatchingEngine
{
    public ReadOnlySpan<MatcherEvent> Process(in IncomingEvent cmd)
    {
        throw new NotImplementedException();
    }
}