using MongoDB.Bson;
using MongoDB.Driver;

static class LoanDocumentMapper
{
    public static FilterDefinition<LoanDocument> BookIdentifierFilter(ObjectId bookId)
    {
        return Builders<LoanDocument>.Filter.Eq(loan => loan.BookId, bookId);
    }

    public static LoanDocument CreateLoanDocument(BookInventoryResponse book, ObjectId bookId, ValidatedLoanCreatePayload payload)
    {
        var now = DateTime.UtcNow;

        return new LoanDocument
        {
            Id = ObjectId.GenerateNewId(),
            BookId = bookId,
            BookTitle = book.Title,
            BookAuthor = book.Author,
            BorrowerName = payload.BorrowerName,
            BorrowerEmail = payload.BorrowerEmail,
            Notes = payload.Notes,
            LoanedAt = now,
            DueAt = LoanDateRules.ToUtcDateTime(payload.DueDate),
            ReturnedAt = null,
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static LoanResponse MapLoanResponse(LoanDocument loan)
    {
        return new LoanResponse(
            loan.Id.ToString(),
            loan.BookId.ToString(),
            loan.BookTitle,
            loan.BookAuthor,
            loan.BorrowerName,
            loan.BorrowerEmail,
            loan.Notes,
            loan.LoanedAt,
            loan.DueAt,
            loan.ReturnedAt,
            loan.Status,
            loan.CreatedAt,
            loan.UpdatedAt);
    }

    public static string GetFlexibleId(LoanDocument loan, string fieldName)
    {
        return fieldName switch
        {
            "_id" => loan.Id.ToString(),
            "bookId" => loan.BookId.ToString(),
            _ => string.Empty,
        };
    }
}
