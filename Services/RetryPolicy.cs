using Microsoft.Data.SqlClient;

namespace SalesMetrics.Services
{
    public class RetryPolicy
    {
        private readonly int _maxRetries = 3;
        private readonly TimeSpan _baseDelay = TimeSpan.FromMilliseconds(500);
        private readonly ILogger? _logger;

        public RetryPolicy(ILogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (SqlException ex) when (attempt < _maxRetries && IsTransientError(ex))
                {
                    attempt++;
                    var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

                    _logger?.LogWarning("Database operation failed (attempt {Attempt}/{MaxRetries}): {Error}. Retrying in {DelayMs}ms",
                        attempt, _maxRetries, ex.Message, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Database operation failed after {Attempt} attempts", attempt);
                    throw;
                }
            }
        }

        private static bool IsTransientError(SqlException ex)
        {
            // Common transient error codes that should be retried
            var transientErrorNumbers = new[]
            {
                2,      // Timeout
                53,     // Network-related
                121,    // Semaphore timeout
                233,    // Connection initialization error
                10053,  // Connection broken
                10054,  // Connection reset
                10060,  // Network timeout
                40197,  // Service busy
                40501,  // Service unavailable
                40613,  // Database unavailable
                49918,  // Cannot process request
                49919,  // Cannot process create or update request
                49920   // Cannot process request
            };

            return transientErrorNumbers.Contains(ex.Number);
        }
    }
}