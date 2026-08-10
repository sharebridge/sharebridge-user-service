using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Infrastructure;

public class DbRetryTests
{
    [Fact]
    public async Task Retries_transient_failure_then_succeeds()
    {
        var attempts = 0;
        var result = await DbRetry.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new TimeoutException("Timeout during reading attempt");
                }

                return Task.FromResult("ok");
            },
            options: new DataAccessOptions { RetryMaxAttempts = 3, RetryBaseDelayMs = 0 });

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }
}
