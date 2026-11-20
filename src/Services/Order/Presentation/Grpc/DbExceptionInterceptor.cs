using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Order.Presentation.Grpc;

public class DbExceptionInterceptor : Interceptor
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RpcException(new Status(StatusCode.Aborted, "Concurrent modification."));
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, "Already exists."));
        }
    }

    private static bool IsDuplicateKey(Exception exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
            if (inner is SqlException { Number: PrimaryKeyViolation or UniqueIndexViolation })
                return true;

        return false;
    }
}
