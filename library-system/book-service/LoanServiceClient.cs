using System.Net;
using System.Net.Http.Json;

sealed class LoanServiceClient
{
    private const int MaxGetAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly ILogger<LoanServiceClient> _logger;

    public LoanServiceClient(HttpClient httpClient, ILogger<LoanServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HashSet<string>> GetActiveLoanBookIdsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, "internal/loans/active-book-ids"),
            "az aktív kölcsönzésben lévő könyvek lekérése",
            subjectId: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var bookIds = await response.Content.ReadFromJsonAsync<string[]>(cancellationToken: cancellationToken) ?? [];

        return bookIds
            .Where(bookId => !string.IsNullOrWhiteSpace(bookId))
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<LoanResponse?> GetActiveLoanForBookAsync(string bookId, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, $"internal/loans/books/{Uri.EscapeDataString(bookId)}/active"),
            "az aktív kölcsönzés lekérése",
            bookId,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoanResponse>(cancellationToken: cancellationToken);
    }

    public async Task<bool> HasActiveLoanAsync(string bookId, CancellationToken cancellationToken)
    {
        return await GetActiveLoanForBookAsync(bookId, cancellationToken) is not null;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        string operationName,
        string? subjectId,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxGetAttempts; attempt += 1)
        {
            try
            {
                using var request = requestFactory();
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!ShouldRetry(response.StatusCode) || attempt == MaxGetAttempts)
                {
                    return response;
                }

                _logger.LogWarning(
                    "A loan-service kérés átmeneti {StatusCode} állapottal válaszolt a(z) {Operation} közben. Újrapróbálás következik. Könyv: {SubjectId}, kísérlet: {Attempt}/{MaxAttempts}.",
                    (int)response.StatusCode,
                    operationName,
                    subjectId ?? "(nincs)",
                    attempt,
                    MaxGetAttempts);

                response.Dispose();
            }
            catch (Exception exception) when (IsTransientException(exception, cancellationToken) && attempt < MaxGetAttempts)
            {
                lastException = exception;
                _logger.LogWarning(
                    exception,
                    "Átmeneti hiba történt a(z) {Operation} közben. Újrapróbálás következik. Könyv: {SubjectId}, kísérlet: {Attempt}/{MaxAttempts}.",
                    operationName,
                    subjectId ?? "(nincs)",
                    attempt,
                    MaxGetAttempts);
            }

            if (attempt < MaxGetAttempts)
            {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
        }

        throw lastException ?? new HttpRequestException($"Nem sikerült befejezni a műveletet: {operationName}.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path) => new(method, path);

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
