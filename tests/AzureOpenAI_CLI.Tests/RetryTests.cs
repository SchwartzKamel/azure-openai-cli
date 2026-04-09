using Azure;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for the exponential backoff retry helper (Program.WithRetryAsync).
///
/// Validates that:
///   1. Successful operations return immediately without retry.
///   2. Transient 429 (rate-limit) errors trigger retries up to the max.
///   3. Transient 5xx (server error) errors trigger retries up to the max.
///   4. Non-transient errors (4xx except 429) propagate immediately.
///   5. The retry count is respected — exceeding maxRetries throws.
///   6. Cancellation via CancellationToken is honoured during backoff.
///   7. Exponential backoff timing follows the 2^(attempt-1) formula.
///   8. Recovery after transient failures returns the correct value.
/// </summary>
public class RetryTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a RequestFailedException with the given HTTP status code.
    /// </summary>
    private static RequestFailedException MakeRequestFailed(int status, string message = "test error")
        => new RequestFailedException(status, message);

    /// <summary>
    /// Builds a Func that fails N times with the given status, then succeeds.
    /// Tracks call count via the returned counter reference.
    /// </summary>
    private static (Func<Task<string>> Operation, Func<int> GetCallCount) FailThenSucceed(
        int failCount, int failStatus, string successValue = "OK")
    {
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            if (callCount <= failCount)
                throw MakeRequestFailed(failStatus);
            return Task.FromResult(successValue);
        };
        return (op, () => callCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1. Success on first call — no retries
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_SucceedsFirstAttempt_ReturnsImmediately()
    {
        // Arrange — operation always succeeds
        int callCount = 0;
        Func<Task<int>> op = () => { callCount++; return Task.FromResult(42); };

        // Act
        int result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert — called exactly once, correct value
        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task WithRetryAsync_SucceedsFirstAttempt_StringResult()
    {
        // Arrange
        Func<Task<string>> op = () => Task.FromResult("hello world");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert
        Assert.Equal("hello world", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. Retries on HTTP 429 (rate limit)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_429ThenSuccess_RetriesAndReturns()
    {
        // Arrange — fail once with 429, then succeed
        var (op, getCount) = FailThenSucceed(1, 429, "recovered");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert — retried once, then succeeded
        Assert.Equal("recovered", result);
        Assert.Equal(2, getCount());
    }

    [Fact]
    public async Task WithRetryAsync_Multiple429s_RetriesUpToMax()
    {
        // Arrange — fail twice with 429, succeed on 3rd call
        var (op, getCount) = FailThenSucceed(2, 429, "finally");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert — called 3 times total (2 failures + 1 success)
        Assert.Equal("finally", result);
        Assert.Equal(3, getCount());
    }

    [Fact]
    public async Task WithRetryAsync_429ExceedsMaxRetries_Throws()
    {
        // Arrange — always fail with 429, maxRetries = 2
        Func<Task<string>> op = () => throw MakeRequestFailed(429);

        // Act & Assert — after 2 retries (3 total attempts), should throw
        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 2));
        Assert.Equal(429, ex.Status);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. Retries on 5xx (server errors)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task WithRetryAsync_5xxThenSuccess_RetriesAndReturns(int status)
    {
        // Arrange — fail once with server error, then succeed
        var (op, getCount) = FailThenSucceed(1, status, $"recovered-{status}");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert
        Assert.Equal($"recovered-{status}", result);
        Assert.Equal(2, getCount());
    }

    [Fact]
    public async Task WithRetryAsync_500ExceedsMaxRetries_Throws()
    {
        // Arrange — always fail with 500
        Func<Task<string>> op = () => throw MakeRequestFailed(500);

        // Act & Assert — exhausts retries and propagates
        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 1));
        Assert.Equal(500, ex.Status);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. Non-transient errors propagate immediately (no retry)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task WithRetryAsync_NonTransientStatus_ThrowsImmediately(int status)
    {
        // Arrange — fail with a non-transient status
        int callCount = 0;
        Func<Task<string>> op = () => { callCount++; throw MakeRequestFailed(status); };

        // Act & Assert — should throw on first attempt, no retries
        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(status, ex.Status);
        Assert.Equal(1, callCount); // Called only once — no retry
    }

    [Fact]
    public async Task WithRetryAsync_Status499_DoesNotRetry()
    {
        // Arrange — 499 is < 500 and not 429, so no retry
        int callCount = 0;
        Func<Task<string>> op = () => { callCount++; throw MakeRequestFailed(499); };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(499, ex.Status);
        Assert.Equal(1, callCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 5. Non-RequestFailedException errors propagate immediately
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_GenericException_PropagatesImmediately()
    {
        // Arrange — throw a plain exception, not a RequestFailedException
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            throw new InvalidOperationException("something broke");
        };

        // Act & Assert — should NOT be caught by the retry handler
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task WithRetryAsync_ArgumentException_PropagatesImmediately()
    {
        // Arrange
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            throw new ArgumentException("bad arg");
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(1, callCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 6. maxRetries = 0 means no retries at all
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_MaxRetriesZero_SuccessReturnsNormally()
    {
        // Arrange
        Func<Task<string>> op = () => Task.FromResult("no-retry-needed");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 0);

        // Assert
        Assert.Equal("no-retry-needed", result);
    }

    [Fact]
    public async Task WithRetryAsync_MaxRetriesZero_TransientErrorThrowsImmediately()
    {
        // Arrange — maxRetries = 0 means attempt < 0 is never true after first fail
        int callCount = 0;
        Func<Task<string>> op = () => { callCount++; throw MakeRequestFailed(429); };

        // Act & Assert — no retries, throws on first failure
        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 0));
        Assert.Equal(429, ex.Status);
        Assert.Equal(1, callCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 7. Cancellation during backoff delay
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_CancelledDuringBackoff_ThrowsOperationCancelled()
    {
        // Arrange — always fail with 429, cancel immediately
        using var cts = new CancellationTokenSource();
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            // Cancel after first failure so the backoff delay is interrupted
            cts.Cancel();
            throw MakeRequestFailed(429);
        };

        // Act & Assert — the Task.Delay during backoff should throw OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Program.WithRetryAsync(op, maxRetries: 3, ct: cts.Token));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task WithRetryAsync_PreCancelledToken_ThrowsOnFirstTransientError()
    {
        // Arrange — token is already cancelled before we even start
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            throw MakeRequestFailed(500);
        };

        // Act & Assert — after the first failure, Task.Delay with cancelled token throws
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Program.WithRetryAsync(op, maxRetries: 3, ct: cts.Token));
        Assert.Equal(1, callCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 8. Exponential backoff timing validation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_BackoffTimingIsExponential()
    {
        // Arrange — fail twice with 429, succeed on 3rd
        // Expected backoff: attempt 1 → 2^0 = 1s, attempt 2 → 2^1 = 2s
        // Use maxRetries: 3 so it can actually recover
        // We can't check exact timing easily, but we can verify the delays
        // are at least exponentially growing by timing the retries
        var timestamps = new List<DateTime>();
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            timestamps.Add(DateTime.UtcNow);
            if (callCount <= 2)
                throw MakeRequestFailed(429);
            return Task.FromResult("done");
        };

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string result = await Program.WithRetryAsync(op, maxRetries: 3);
        sw.Stop();

        // Assert — succeeded after retries
        Assert.Equal("done", result);
        Assert.Equal(3, callCount);
        // Total time should be at least 1s + 2s = 3s (backoff delays)
        Assert.True(sw.Elapsed.TotalSeconds >= 2.5,
            $"Expected at least 2.5s of backoff, actual: {sw.Elapsed.TotalSeconds:F1}s");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 9. Mixed error scenarios
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_TransientThenNonTransient_PropagatesNonTransient()
    {
        // Arrange — first call: 503 (retryable), second call: 401 (not retryable)
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            if (callCount == 1)
                throw MakeRequestFailed(503);
            throw MakeRequestFailed(401);
        };

        // Act & Assert — retries the 503, then the 401 propagates immediately
        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(401, ex.Status);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task WithRetryAsync_AlternatingTransientErrors_RetriesAll()
    {
        // Arrange — alternating 429 and 502, then success
        int callCount = 0;
        Func<Task<string>> op = () =>
        {
            callCount++;
            if (callCount == 1) throw MakeRequestFailed(429);
            if (callCount == 2) throw MakeRequestFailed(502);
            return Task.FromResult("survived");
        };

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert — both transient errors were retried
        Assert.Equal("survived", result);
        Assert.Equal(3, callCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 10. Return type generics
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_ReturnsCorrectGenericType_Int()
    {
        // Arrange & Act
        int result = await Program.WithRetryAsync(() => Task.FromResult(999), maxRetries: 1);

        // Assert
        Assert.Equal(999, result);
    }

    [Fact]
    public async Task WithRetryAsync_ReturnsCorrectGenericType_Bool()
    {
        // Arrange & Act
        bool result = await Program.WithRetryAsync(() => Task.FromResult(true), maxRetries: 1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task WithRetryAsync_ReturnsCorrectGenericType_ComplexObject()
    {
        // Arrange
        var expected = new { Name = "test", Value = 42 };
        Func<Task<object>> op = () => Task.FromResult<object>(expected);

        // Act
        object result = await Program.WithRetryAsync(op, maxRetries: 1);

        // Assert
        Assert.Same(expected, result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 11. Boundary: exactly at maxRetries limit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_ExactlyMaxRetriesThenSuccess_Succeeds()
    {
        // Arrange — fail exactly maxRetries times, then succeed on the next call
        const int maxRetries = 3;
        var (op, getCount) = FailThenSucceed(maxRetries, 500, "just-in-time");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: maxRetries);

        // Assert — called maxRetries+1 times (3 failures + 1 success)
        Assert.Equal("just-in-time", result);
        Assert.Equal(maxRetries + 1, getCount());
    }

    [Fact]
    public async Task WithRetryAsync_OneMoreThanMaxRetries_Throws()
    {
        // Arrange — fail maxRetries+1 times (one more than allowed)
        const int maxRetries = 2;
        var (op, getCount) = FailThenSucceed(maxRetries + 1, 500, "never reached");

        // Act & Assert — the 3rd failure (attempt index 2 == maxRetries) is not caught
        var ex = await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: maxRetries));
        Assert.Equal(500, ex.Status);
        // Called maxRetries+1 times: attempts 0,1,2 where attempt 2 == maxRetries so filter fails
        Assert.Equal(maxRetries + 1, getCount());
    }

    // ═══════════════════════════════════════════════════════════════════
    // 12. Edge: status code boundaries
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WithRetryAsync_Status500_IsRetried()
    {
        // Arrange — exactly 500 is the boundary for server errors
        var (op, getCount) = FailThenSucceed(1, 500, "ok");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert
        Assert.Equal("ok", result);
        Assert.Equal(2, getCount());
    }

    [Fact]
    public async Task WithRetryAsync_Status499_IsNotRetried()
    {
        // Arrange — 499 is just below the 500 threshold
        int callCount = 0;
        Func<Task<string>> op = () => { callCount++; throw MakeRequestFailed(499); };

        // Act & Assert — not retried
        await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task WithRetryAsync_Status429_IsRetried()
    {
        // Arrange — 429 is the only 4xx that should be retried
        var (op, getCount) = FailThenSucceed(1, 429, "ok");

        // Act
        string result = await Program.WithRetryAsync(op, maxRetries: 3);

        // Assert
        Assert.Equal("ok", result);
        Assert.Equal(2, getCount());
    }

    [Fact]
    public async Task WithRetryAsync_Status428_IsNotRetried()
    {
        // Arrange — 428 is adjacent to 429 but should NOT be retried
        int callCount = 0;
        Func<Task<string>> op = () => { callCount++; throw MakeRequestFailed(428); };

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task WithRetryAsync_Status430_IsNotRetried()
    {
        // Arrange — 430 is adjacent to 429 but should NOT be retried
        int callCount = 0;
        Func<Task<string>> op = () => { callCount++; throw MakeRequestFailed(430); };

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(
            () => Program.WithRetryAsync(op, maxRetries: 3));
        Assert.Equal(1, callCount);
    }
}
