using Grpc.Core;
using Order.Domain;

namespace Order.Presentation.Grpc;

public static class ResultToStatus
{
    public static T GetValueOrThrow<T>(this Result<T> result)
    {
        return result.IsSuccess ? result.Value : throw result.Error.ToRpcException();
    }

    public static void ThrowIfFailure(this Result result)
    {
        if (result.IsFailure)
            throw result.Error.ToRpcException();
    }

    private static RpcException ToRpcException(this Error error)
    {
        var code = error.Type switch
        {
            ErrorType.Validation => StatusCode.InvalidArgument,
            ErrorType.NotFound => StatusCode.NotFound,
            ErrorType.Conflict or ErrorType.Violation => StatusCode.FailedPrecondition,
            _ => StatusCode.Internal
        };

        return new RpcException(new Status(code, error.Message));
    }
}
