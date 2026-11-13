using System.Security.Claims;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Order.Presentation.Grpc;

public static class ServerCallContextExtensions
{
    public static Guid GetUserId(this ServerCallContext context)
    {
        var subject = context.GetHttpContext().User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out var userId)
            ? userId
            : throw new RpcException(new Status(StatusCode.Unauthenticated, "Token has no valid user id."));
    }
}
