using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

public sealed class GatewayFlowTests : IAsyncLifetime
{
    private readonly HttpClient _httpClient;

    public GatewayFlowTests()
    {
        var baseUrl = Environment.GetEnvironmentVariable("LIBRARY_BASE_URL") ?? "http://localhost:3000";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    [Fact]
    public async Task CanCreateAndReturnLoanThroughGateway()
    {
        var createdBook = await CreateBookAsync(new BookUpsertRequest(
            Title: $"Teszt könyv {Guid.NewGuid():N}",
            Author: "Automata Teszt",
            Year: 2026,
            Genre: "Teszt",
            Available: true));

        string? loanId = null;

        try
        {
            var createdLoan = await PostJsonAsync<LoanResponse>(
                "api/loans",
                new LoanCreateRequest(
                    createdBook.Id,
                    "Teszt Olvasó",
                    "teszt@example.com",
                    DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"),
                    "Integrációs teszt"));

            loanId = createdLoan.Id;

            Assert.Equal(createdBook.Id, createdLoan.BookId);
            Assert.Equal("active", createdLoan.Status);

            var borrowedBook = await GetJsonAsync<BookResponse>($"api/books/{createdBook.Id}");
            Assert.False(borrowedBook.Available);
            Assert.True(borrowedBook.HasActiveLoan);

            var returnedLoan = await PutJsonAsync<LoanResponse>(
                $"api/loans/{createdLoan.Id}/return",
                new LoanReturnRequest(null));

            Assert.Equal("returned", returnedLoan.Status);

            var releasedBook = await WaitForBookAsync(
                createdBook.Id,
                book => book.Available && !book.HasActiveLoan,
                timeout: TimeSpan.FromSeconds(12));

            Assert.True(releasedBook.Available);
            Assert.False(releasedBook.HasActiveLoan);
        }
        finally
        {
            if (loanId is not null)
            {
                await EnsureLoanReturnedAsync(loanId);
                await TryWaitForBookAvailabilityAsync(createdBook.Id);
            }

            await TryDeleteBookAsync(createdBook.Id);
        }
    }

    [Fact]
    public async Task CanFilterAndPageBooksThroughGateway()
    {
        var titlePrefix = $"Szűrő teszt {Guid.NewGuid():N}";
        var firstBook = await CreateBookAsync(new BookUpsertRequest(
            Title: $"{titlePrefix} A",
            Author: "Automata Teszt",
            Year: 2024,
            Genre: "Szűrő",
            Available: true));
        var secondBook = await CreateBookAsync(new BookUpsertRequest(
            Title: $"{titlePrefix} B",
            Author: "Automata Teszt",
            Year: 2025,
            Genre: "Szűrő",
            Available: true));
        var thirdBook = await CreateBookAsync(new BookUpsertRequest(
            Title: $"{titlePrefix} C",
            Author: "Automata Teszt",
            Year: 2026,
            Genre: "Szűrő",
            Available: false));

        try
        {
            var firstPage = await GetJsonAsync<BookListResponse>(
                $"api/books?title={Uri.EscapeDataString(titlePrefix)}&page=1&pageSize=2");
            var secondPage = await GetJsonAsync<BookListResponse>(
                $"api/books?title={Uri.EscapeDataString(titlePrefix)}&page=2&pageSize=2");
            var availableOnly = await GetJsonAsync<BookListResponse>(
                $"api/books?title={Uri.EscapeDataString(titlePrefix)}&available=true&page=1&pageSize=10");

            Assert.Equal(3, firstPage.TotalCount);
            Assert.Equal(2, firstPage.Items.Count);
            Assert.Equal(firstBook.Id, firstPage.Items[0].Id);
            Assert.Equal(secondBook.Id, firstPage.Items[1].Id);

            Assert.Single(secondPage.Items);
            Assert.Equal(thirdBook.Id, secondPage.Items[0].Id);

            Assert.Equal(2, availableOnly.TotalCount);
            Assert.All(availableOnly.Items, book => Assert.True(book.Available));
            Assert.DoesNotContain(availableOnly.Items, book => book.Id == thirdBook.Id);
        }
        finally
        {
            await TryDeleteBookAsync(firstBook.Id);
            await TryDeleteBookAsync(secondBook.Id);
            await TryDeleteBookAsync(thirdBook.Id);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    private async Task<BookResponse> CreateBookAsync(BookUpsertRequest request)
    {
        return await PostJsonAsync<BookResponse>("api/books", request);
    }

    private async Task<TResponse> GetJsonAsync<TResponse>(string relativeUrl)
    {
        using var response = await _httpClient.GetAsync(relativeUrl);
        response.EnsureSuccessStatusCode();

        return await ReadJsonAsync<TResponse>(response);
    }

    private async Task<TResponse> PostJsonAsync<TResponse>(string relativeUrl, object request)
    {
        using var response = await _httpClient.PostAsJsonAsync(relativeUrl, request);
        response.EnsureSuccessStatusCode();

        return await ReadJsonAsync<TResponse>(response);
    }

    private async Task<TResponse> PutJsonAsync<TResponse>(string relativeUrl, object request)
    {
        using var response = await _httpClient.PutAsJsonAsync(relativeUrl, request);
        response.EnsureSuccessStatusCode();

        return await ReadJsonAsync<TResponse>(response);
    }

    private async Task<BookResponse> WaitForBookAsync(
        string bookId,
        Func<BookResponse, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var book = await GetJsonAsync<BookResponse>($"api/books/{bookId}");

            if (predicate(book))
            {
                return book;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(400));
        }

        throw new TimeoutException($"A könyv állapota nem érte el időben a várt értéket. Könyv: {bookId}");
    }

    private async Task EnsureLoanReturnedAsync(string loanId)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/loans/{loanId}/return",
            new LoanReturnRequest(null));

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task TryWaitForBookAvailabilityAsync(string bookId)
    {
        try
        {
            await WaitForBookAsync(bookId, book => book.Available, timeout: TimeSpan.FromSeconds(8));
        }
        catch (TimeoutException)
        {
            // A takarításnál nem fedjük el az eredeti teszthibát egy későn lefutó release miatt.
        }
    }

    private async Task TryDeleteBookAsync(string bookId)
    {
        using var response = await _httpClient.DeleteAsync($"api/books/{bookId}");

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            await TryWaitForBookAvailabilityAsync(bookId);

            using var retryResponse = await _httpClient.DeleteAsync($"api/books/{bookId}");

            if (retryResponse.IsSuccessStatusCode || retryResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            retryResponse.EnsureSuccessStatusCode();
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private static async Task<TResponse> ReadJsonAsync<TResponse>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<TResponse>();

        if (payload is null)
        {
            throw new InvalidOperationException("A válasz üres volt, pedig JSON-t vártunk.");
        }

        return payload;
    }

    private sealed record BookUpsertRequest(
        string Title,
        string Author,
        int Year,
        string Genre,
        bool Available);

    private sealed record LoanCreateRequest(
        string BookId,
        string BorrowerName,
        string BorrowerEmail,
        string DueAt,
        string? Notes);

    private sealed record LoanReturnRequest(string? ReturnedAt);

    private sealed record BookResponse(
        [property: JsonPropertyName("_id")] string Id,
        string Title,
        string Author,
        int Year,
        string Genre,
        bool Available,
        bool HasActiveLoan);

    private sealed record BookListResponse(
        IReadOnlyList<BookResponse> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record LoanResponse(
        [property: JsonPropertyName("_id")] string Id,
        string BookId,
        string Status);
}
