namespace Messaging.Dapper.Transactions;

public sealed class CommitOutcomeUnknownException(Exception innerException)
    : Exception("The commit failed after it was sent; the transaction may or may not have been committed.",
        innerException);
