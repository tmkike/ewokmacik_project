using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Driver;

record ErrorResponse(string Message);

sealed record ValidationErrorResponse(string Message, IReadOnlyList<string> Errors) : ErrorResponse(Message);

sealed record BookUpsertRequest(string? Title, string? Author, int? Year, string? Genre, bool? Available);

sealed record BookAvailabilityRequest(bool? Available);

sealed record ValidatedBookPayload(string Title, string Author, int Year, string Genre, bool Available);

sealed record BookQueryFilterResult(FilterDefinition<BsonDocument> Filter, string? ErrorMessage);

sealed record BookInventoryResponse(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string Author,
    int Year,
    string Genre,
    bool Available);

sealed record BookResponse(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string Author,
    int Year,
    string Genre,
    bool Available,
    bool HasActiveLoan = false);

sealed record BookDetailResponse(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string Author,
    int Year,
    string Genre,
    bool Available,
    bool HasActiveLoan,
    LoanResponse? ActiveLoan);

sealed record LoanResponse(
    [property: JsonPropertyName("_id")] string Id,
    string BookId,
    string BookTitle,
    string BookAuthor,
    string BorrowerName,
    string? BorrowerEmail,
    string? Notes,
    DateTime LoanedAt,
    DateTime? DueAt,
    DateTime? ReturnedAt,
    string Status,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);
