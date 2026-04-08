using System.Net;
using System.Net.Http.Json;

sealed class LoanServiceClient
{
    private readonly HttpClient _httpClient;

    public LoanServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HashSet<string>> GetActiveLoanBookIdsAsync(CancellationToken cancellationToken)
    {
        var bookIds = await _httpClient.GetFromJsonAsync<string[]>("internal/loans/active-book-ids", cancellationToken) ?? [];

        return bookIds
            .Where(bookId => !string.IsNullOrWhiteSpace(bookId))
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<LoanResponse?> GetActiveLoanForBookAsync(string bookId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"internal/loans/books/{Uri.EscapeDataString(bookId)}/active",
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
}
