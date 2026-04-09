using System.Net;
using System.Net.Http.Json;

sealed class BookServiceClient
{
    private const int MaxReleaseAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly ILogger<BookServiceClient> _logger;

    public BookServiceClient(HttpClient httpClient, ILogger<BookServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ReserveBookResult> ReserveBookAsync(string bookId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => CreatePostRequest($"internal/books/{Uri.EscapeDataString(bookId)}/reserve"),
            "a könyv lefoglalása",
            bookId,
            maxAttempts: 1,
            retryTransientFailures: false,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ReserveBookResult(BookReservationStatus.NotFound, null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return new ReserveBookResult(BookReservationStatus.Unavailable, null);
        }

        response.EnsureSuccessStatusCode();
        var book = await response.Content.ReadFromJsonAsync<BookInventoryResponse>(cancellationToken: cancellationToken);
        return new ReserveBookResult(BookReservationStatus.Reserved, book);
    }

    public async Task<ReleaseBookResult> ReleaseBookAsync(string bookId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => CreatePostRequest($"internal/books/{Uri.EscapeDataString(bookId)}/release"),
            "a könyv felszabadítása",
            bookId,
            maxAttempts: MaxReleaseAttempts,
            retryTransientFailures: true,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ReleaseBookResult(BookReleaseStatus.NotFound);
        }

        response.EnsureSuccessStatusCode();
        return new ReleaseBookResult(BookReleaseStatus.Released);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        string operationName,
        string bookId,
        int maxAttempts,
        bool retryTransientFailures,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt += 1)
        {
            try
            {
                using var request = requestFactory();
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!retryTransientFailures || !ShouldRetry(response.StatusCode) || attempt == maxAttempts)
                {
                    return response;
                }

                _logger.LogWarning(
                    "A book-service átmeneti {StatusCode} állapottal válaszolt a(z) {Operation} közben. Újrapróbálás következik. Könyv: {BookId}, kísérlet: {Attempt}/{MaxAttempts}.",
                    (int)response.StatusCode,
                    operationName,
                    bookId,
                    attempt,
                    maxAttempts);

                response.Dispose();
            }
            catch (Exception exception) when (retryTransientFailures && IsTransientException(exception, cancellationToken) && attempt < maxAttempts)
            {
                lastException = exception;
                _logger.LogWarning(
                    exception,
                    "Átmeneti hiba történt a(z) {Operation} közben. Újrapróbálás következik. Könyv: {BookId}, kísérlet: {Attempt}/{MaxAttempts}.",
                    operationName,
                    bookId,
                    attempt,
                    maxAttempts);
            }

            if (retryTransientFailures && attempt < maxAttempts)
            {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
        }

        throw lastException ?? new HttpRequestException($"Nem sikerült befejezni a műveletet: {operationName}.");
    }

    private static HttpRequestMessage CreatePostRequest(string path) => new(HttpMethod.Post, path);

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }

    private static bool IsTransientException(Exception exception, CancellationToken cancellationToken)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException => !cancellationToken.IsCancellationRequested,
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            _ => false,
        };
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        return attempt switch
        {
            1 => TimeSpan.FromMilliseconds(250),
            2 => TimeSpan.FromMilliseconds(750),
            _ => TimeSpan.FromSeconds(1.5),
        };
    }
}
