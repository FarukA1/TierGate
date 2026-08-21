using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TierGate.Core.RateLimiting;

/// <summary>
/// Rate-limiting and quota tracking backed by Azure Table Storage. Bucket boundaries come
/// from <see cref="RateLimitWindow"/>; ETag optimistic concurrency handles concurrent callers
/// safely. After <see cref="MaxRetries"/> ETag conflicts, fails closed — denies the request
/// rather than allowing an unmetered one through.
/// </summary>
public sealed class TableStorageRateLimitStore : IRateLimitStore
{
    private const string CountColumn = "Count";
    private const int MaxRetries = 6;

    private readonly TableClient _table;
    private readonly ILogger<TableStorageRateLimitStore> _logger;
    private readonly SemaphoreSlim _tableReadyGate = new(1, 1);
    private bool _tableReady;

    public TableStorageRateLimitStore(
        string connectionString,
        string tableName = "TierGateCounters",
        ILogger<TableStorageRateLimitStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _logger = logger ?? NullLogger<TableStorageRateLimitStore>.Instance;
        _table = new TableClient(connectionString, tableName);
    }

    // Table creation moved out of the constructor — a blocking network call there could tie up a
    // thread during DI construction, and any failure there wasn't reachable via StoreUnavailable.
    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        if (_tableReady) return;
        await _tableReadyGate.WaitAsync(cancellationToken);
        try
        {
            if (_tableReady) return;
            await _table.CreateIfNotExistsAsync(cancellationToken);
            _tableReady = true;
        }
        finally
        {
            _tableReadyGate.Release();
        }
    }

    public async Task<RateLimitResult> TryConsumeAsync(
        string subjectKey, RateLimitWindow window, int limit, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var (partitionKey, rowKey) = BuildKeys(subjectKey, window, now);
        var resetsAt = window.GetResetsAt(now);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);

                TableEntity entity;
                ETag etag;

                try
                {
                    var response = await _table.GetEntityAsync<TableEntity>(
                        partitionKey, rowKey, cancellationToken: cancellationToken);
                    entity = response.Value;
                    etag = entity.ETag;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    var newEntity = new TableEntity(partitionKey, rowKey) { [CountColumn] = 1 };
                    try
                    {
                        await _table.AddEntityAsync(newEntity, cancellationToken);
                        return RateLimitResult.Allow(limit - 1, limit, resetsAt);
                    }
                    catch (RequestFailedException addEx) when (addEx.Status == 409)
                    {
                        continue; // another caller created the row first — retry as an update
                    }
                }

                var currentCount = entity.GetInt32(CountColumn) ?? 0;
                if (currentCount >= limit)
                    return RateLimitResult.Deny(limit, resetsAt);

                entity[CountColumn] = currentCount + 1;
                await _table.UpdateEntityAsync(entity, etag, TableUpdateMode.Replace, cancellationToken);
                return RateLimitResult.Allow(limit - (currentCount + 1), limit, resetsAt);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                _logger.LogDebug(ex, "Counter ETag conflict for {PartitionKey}/{RowKey}, attempt {Attempt}.",
                    partitionKey, rowKey, attempt + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(10, 30) * (attempt + 1)), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Table Storage request failed for {PartitionKey}/{RowKey}.",
                    partitionKey, rowKey);
                return RateLimitResult.Unavailable(limit);
            }
        }

        _logger.LogWarning(
            "Counter update failed after {MaxRetries} retries for {PartitionKey}/{RowKey}. Denying to fail closed.",
            MaxRetries, partitionKey, rowKey);
        return RateLimitResult.Deny(limit, resetsAt);
    }

    public async Task<int> GetCurrentUsageAsync(
        string subjectKey, RateLimitWindow window, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);
        var (partitionKey, rowKey) = BuildKeys(subjectKey, window, DateTimeOffset.UtcNow);
        try
        {
            var response = await _table.GetEntityAsync<TableEntity>(
                partitionKey, rowKey, cancellationToken: cancellationToken);
            return response.Value.GetInt32(CountColumn) ?? 0;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    public async Task SeedUsageAsync(
        string subjectKey, RateLimitWindow window, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return;
        await EnsureTableExistsAsync(cancellationToken);
        var (partitionKey, rowKey) = BuildKeys(subjectKey, window, DateTimeOffset.UtcNow);
        var entity = new TableEntity(partitionKey, rowKey) { [CountColumn] = count };
        await _table.AddEntityAsync(entity, cancellationToken);
    }

    public async Task ReconcileUsageAsync(
        string subjectKey, RateLimitWindow window, int authoritativeCount, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);
        var (partitionKey, rowKey) = BuildKeys(subjectKey, window, DateTimeOffset.UtcNow);
        var entity = new TableEntity(partitionKey, rowKey) { [CountColumn] = authoritativeCount };
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    private static (string PartitionKey, string RowKey) BuildKeys(
        string subjectKey, RateLimitWindow window, DateTimeOffset now)
    {
        // Partition on a prefix of the subject key to spread load across partitions while
        // keeping all of one subject's windows queryable together.
        var partitionKey = subjectKey.Length > 8 ? subjectKey[..8] : subjectKey;
        var rowKey = $"{subjectKey}:{window.GetBucketKey(now)}";
        return (partitionKey, rowKey);
    }
}
