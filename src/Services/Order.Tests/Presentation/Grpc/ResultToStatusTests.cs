using Grpc.Core;
using Order.Domain;
using Order.Presentation.Grpc;
using Xunit;

namespace Order.Tests.Presentation.Grpc;

public class ResultToStatusTests
{
    [Theory]
    [InlineData(ErrorType.Validation, StatusCode.InvalidArgument)]
    [InlineData(ErrorType.NotFound, StatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, StatusCode.FailedPrecondition)]
    [InlineData(ErrorType.Violation, StatusCode.FailedPrecondition)]
    public void ThrowIfFailure_Failure_ThrowsMappedStatusWithMessage(ErrorType type, StatusCode expected)
    {
        var result = Result.Failure(new Error { Type = type, Message = "boom" });

        var exception = Assert.Throws<RpcException>(() => result.ThrowIfFailure());

        Assert.Equal(expected, exception.StatusCode);
        Assert.Equal("boom", exception.Status.Detail);
    }

    [Fact]
    public void ThrowIfFailure_Success_DoesNotThrow()
    {
        Result.Success().ThrowIfFailure();
    }

    [Fact]
    public void GetValueOrThrow_Success_ReturnsValue()
    {
        Assert.Equal(42, Result<int>.Success(42).GetValueOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_Failure_ThrowsMappedStatus()
    {
        var result = Result<int>.Failure(new Error { Type = ErrorType.NotFound, Message = "missing" });

        var exception = Assert.Throws<RpcException>(() => result.GetValueOrThrow());

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
}
