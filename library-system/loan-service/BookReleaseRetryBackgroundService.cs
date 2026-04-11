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
