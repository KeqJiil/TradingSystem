using System.Net.Sockets;
using Confluent.Kafka;
using Microsoft.Data.SqlClient;
using Stock.Application.Exceptions;

namespace Stock.Infrastructure.Messaging.Publishers;

public static class RetryableErrors
{
    public static bool IsRetryable(Exception ex)
    {
        return ex switch
        {
            SqlException
            {
                Number: -2 or 1205 or 4060 or 4221 or 40197 or 40501 or 40613
                or 49918 or 49919 or 49920 or 233 or 10053 or 10054 or 10060
                or 921 or 922 or 923 or 924 or 926
            } => true,
            ReadModelNotFoundException => true,
            KafkaException kex => !kex.Error.IsFatal && IsRetryableKafkaCode(kex.Error.Code),
            TimeoutException => true,
            SocketException => true,
            IOException { InnerException: SocketException } => true,
            OperationCanceledException ocex when !ocex.CancellationToken.IsCancellationRequested => true,
            _ => false
        };
    }

    private static bool IsRetryableKafkaCode(ErrorCode code)
    {
        return code is
            ErrorCode.RequestTimedOut
            or ErrorCode.NotEnoughReplicas
            or ErrorCode.NotEnoughReplicasAfterAppend
            or ErrorCode.NetworkException
            or ErrorCode.Local_TimedOut
            or ErrorCode.Local_Transport
            or ErrorCode.Local_AllBrokersDown
            or ErrorCode.BrokerNotAvailable
            or ErrorCode.GroupLoadInProgress
            or ErrorCode.GroupCoordinatorNotAvailable
            or ErrorCode.NotCoordinatorForGroup
            or ErrorCode.RebalanceInProgress;
    }
}
