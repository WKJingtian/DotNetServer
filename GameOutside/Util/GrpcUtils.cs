using Grpc.Core;
using Polly;
using Polly.Retry;

namespace GameOutside.Util;

public static class GrpcExtensions
{
    public static readonly AsyncRetryPolicy GrpcDefaultRetryPolicy = Policy
        .Handle((Func<RpcException, bool>)(e => e.StatusCode != StatusCode.NotFound))
        .WaitAndRetryAsync(2, x => TimeSpan.FromSeconds(Math.Pow(2, x)));
}
