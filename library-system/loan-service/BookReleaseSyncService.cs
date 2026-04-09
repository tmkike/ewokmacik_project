using MongoDB.Bson;
using MongoDB.Driver;

sealed class BookReleaseSyncService
{
    private const int MaxAttempts = 8;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);

    private readonly MongoDbContext _db;
    private readonly BookServiceClient _bookServiceClient;
    private readonly ILogger<BookReleaseSyncService> _logger;

    public BookReleaseSyncService(
        MongoDbContext db,
        BookServiceClient bookServiceClient,
        ILogger<BookReleaseSyncService> logger)
    {
        _db = db;
        _bookServiceClient = bookServiceClient;
        _logger = logger;
    }

    public async Task<bool> ReleaseOrQueueAsync(string bookId, string? loanId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bookServiceClient.ReleaseBookAsync(bookId, cancellationToken);

            if (result.Status == BookReleaseStatus.NotFound)
            {
                _logger.LogWarning(
                    "Book {BookId} was not found while processing release for loan {LoanId}. Treating the release as completed.",
                    bookId,
                    loanId ?? "(none)");
            }

            await RemovePendingReleaseAsync(bookId, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to release book {BookId} for loan {LoanId}. Queueing retry.",
                bookId,
                loanId ?? "(none)");

            await QueueReleaseRetryAsync(bookId, loanId, exception.Message, cancellationToken);
            return false;
        }
    }

    public async Task ProcessPendingReleasesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var pendingReleases = await _db.PendingBookReleases
            .Find(Builders<PendingBookReleaseDocument>.Filter.Lte(item => item.NextAttemptAt, now))
            .Sort(Builders<PendingBookReleaseDocument>.Sort.Ascending(item => item.NextAttemptAt))
            .Limit(20)
            .ToListAsync(cancellationToken);

        foreach (var pendingRelease in pendingReleases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await _bookServiceClient.ReleaseBookAsync(pendingRelease.BookId, cancellationToken);

                if (result.Status == BookReleaseStatus.NotFound)
                {
                    _logger.LogWarning(
                        "Book {BookId} disappeared before a queued release retry for loan {LoanId} completed.",
                        pendingRelease.BookId,
                        pendingRelease.LoanId ?? "(none)");
                }

                await _db.PendingBookReleases.DeleteOneAsync(
                    Builders<PendingBookReleaseDocument>.Filter.Eq(item => item.Id, pendingRelease.Id),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                var nextAttemptCount = pendingRelease.AttemptCount + 1;
                var nextAttemptAt = nextAttemptCount >= MaxAttempts
                    ? DateTime.MaxValue
                    : DateTime.UtcNow.Add(ComputeRetryDelay(nextAttemptCount));

                if (nextAttemptCount >= MaxAttempts)
                {
                    _logger.LogError(
                        exception,
                        "Queued release retry reached the maximum attempt count for book {BookId}. Manual intervention may be needed.",
                        pendingRelease.BookId);
                }
                else
                {
                    _logger.LogWarning(
                        exception,
                        "Queued release retry failed for book {BookId} on attempt {AttemptCount}.",
                        pendingRelease.BookId,
                        nextAttemptCount);
                }

                await _db.PendingBookReleases.UpdateOneAsync(
                    Builders<PendingBookReleaseDocument>.Filter.Eq(item => item.Id, pendingRelease.Id),
                    Builders<PendingBookReleaseDocument>.Update
                        .Set(item => item.AttemptCount, nextAttemptCount)
                        .Set(item => item.LastAttemptAt, DateTime.UtcNow)
                        .Set(item => item.LastError, exception.Message)
                        .Set(item => item.NextAttemptAt, nextAttemptAt)
                        .Set(item => item.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task QueueReleaseRetryAsync(
        string bookId,
        string? loanId,
        string lastError,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _db.PendingBookReleases.UpdateOneAsync(
            Builders<PendingBookReleaseDocument>.Filter.Eq(item => item.BookId, bookId),
            Builders<PendingBookReleaseDocument>.Update
                .SetOnInsert(item => item.Id, ObjectId.GenerateNewId())
                .SetOnInsert(item => item.BookId, bookId)
                .SetOnInsert(item => item.LoanId, loanId)
                .SetOnInsert(item => item.CreatedAt, now)
                .Inc(item => item.AttemptCount, 1)
                .Set(item => item.LastAttemptAt, now)
                .Set(item => item.LastError, lastError)
                .Set(item => item.NextAttemptAt, now.Add(TimeSpan.FromSeconds(5)))
                .Set(item => item.UpdatedAt, now),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    private Task RemovePendingReleaseAsync(string bookId, CancellationToken cancellationToken)
    {
        return _db.PendingBookReleases.DeleteOneAsync(
            Builders<PendingBookReleaseDocument>.Filter.Eq(item => item.BookId, bookId),
            cancellationToken);
    }

    private static TimeSpan ComputeRetryDelay(int attemptCount)
    {
        var seconds = Math.Min(Math.Pow(2, Math.Max(attemptCount, 1)), MaxRetryDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}

sealed class BookReleaseRetryBackgroundService : BackgroundService
{
    private readonly BookReleaseSyncService _bookReleaseSyncService;
    private readonly ILogger<BookReleaseRetryBackgroundService> _logger;

    public BookReleaseRetryBackgroundService(
        BookReleaseSyncService bookReleaseSyncService,
        ILogger<BookReleaseRetryBackgroundService> logger)
    {
        _bookReleaseSyncService = bookReleaseSyncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessOnceSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessOnceSafelyAsync(stoppingToken);
        }
    }

    private async Task ProcessOnceSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _bookReleaseSyncService.ProcessPendingReleasesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Pending book release processing failed.");
        }
    }
}
