using System.Net;
using System.Net.Http.Json;

sealed class BookServiceClient
{
    private readonly HttpClient _httpClient;

    public BookServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ReserveBookResult> ReserveBookAsync(string bookId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            $"internal/books/{Uri.EscapeDataString(bookId)}/reserve",
            content: null,
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

    public async Task ReleaseBookAsync(string bookId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            $"internal/books/{Uri.EscapeDataString(bookId)}/release",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
