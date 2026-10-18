using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.EFCore.Persistence;

internal static class SequenceExtensions
{
    public static PropertyBuilder<long> UseOrderedSequence(this PropertyBuilder<long> property, string sequenceName)
    {
        var model = property.Metadata.DeclaringType.Model;
        var sequence = model.FindSequence(sequenceName) ?? model.AddSequence(sequenceName);
        sequence.IncrementBy = 1;

        return property.UseHiLo(sequenceName);
    }
}
