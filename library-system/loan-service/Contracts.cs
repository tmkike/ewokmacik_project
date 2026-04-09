using System.Text.Json.Serialization;

record ErrorResponse(string Message);

sealed record ValidationErrorResponse(string Message, IReadOnlyList<string> Errors) : ErrorResponse(Message);

sealed record LoanCreateRequest(
    string? BookId,
    string? BorrowerName,
    string? BorrowerEmail,
    string? DueAt,
    string? Notes);

sealed record LoanUpdateRequest(
    string? BorrowerName,
    string? BorrowerEmail,
    string? DueAt,
    string? Notes);

sealed record LoanReturnRequest(string? ReturnedAt);

sealed record ValidatedLoanCreatePayload(
    string BookId,
    string BorrowerName,
    string BorrowerEmail,
    DateTime DueAt,
    string? Notes);

sealed record ValidatedLoanUpdatePayload(
    string BorrowerName,
    string BorrowerEmail,
    DateTime DueAt,
    string? Notes);

sealed record ValidatedLoanReturnPayload(DateTime ReturnedAt);

sealed record BookInventoryResponse(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string Author,
    int Year,
    string Genre,
    bool Available);

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

enum BookReservationStatus
{
    Reserved,
    NotFound,
    Unavailable,
}

sealed record ReserveBookResult(BookReservationStatus Status, BookInventoryResponse? Book);

enum BookReleaseStatus
{
    Released,
    NotFound,
}

sealed record ReleaseBookResult(BookReleaseStatus Status);
