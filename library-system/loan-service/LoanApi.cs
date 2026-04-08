using System.Globalization;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

static class LoanApi
{
    public static void Map(WebApplication app)
    {
        var loans = app.MapGroup("/api/loans");
        loans.MapGet("/", GetLoansAsync);
        loans.MapGet("/active", GetActiveLoansAsync);
        loans.MapPost("/", CreateLoanAsync);
        loans.MapPut("/{id}", UpdateLoanAsync);
        loans.MapPut("/{id}/return", ReturnLoanAsync);

        var internalLoans = app.MapGroup("/internal/loans");
        internalLoans.MapGet("/active-book-ids", GetActiveLoanBookIdsAsync);
        internalLoans.MapGet("/books/{bookId}/active", GetActiveLoanForBookAsync);
    }

    private static async Task<IResult> GetLoansAsync(MongoDbContext db, CancellationToken cancellationToken)
    {
        var loans = await db.Loans
            .Find(Builders<BsonDocument>.Filter.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("loanedAt"))
            .ToListAsync(cancellationToken);

        return Results.Ok(loans.Select(MapLoanResponse));
    }

    private static async Task<IResult> GetActiveLoansAsync(string? bookId, MongoDbContext db, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("status", "active");

        if (!string.IsNullOrWhiteSpace(bookId))
        {
            if (!ObjectId.TryParse(bookId, out var parsedBookId))
            {
                return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
            }

            filter = Builders<BsonDocument>.Filter.And(filter, BookIdentifierFilter(parsedBookId));
        }

        var loans = await db.Loans
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("loanedAt"))
            .ToListAsync(cancellationToken);

        return Results.Ok(loans.Select(MapLoanResponse));
    }

    private static async Task<IResult> CreateLoanAsync(
        LoanCreateRequest? request,
        MongoDbContext db,
        BookServiceClient bookServiceClient,
        CancellationToken cancellationToken)
    {
        var validationResult = ValidateLoanCreatePayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen kolcsonzesi adatok.", validationResult.Errors));
        }

        var payload = validationResult.Payload!;

        if (!ObjectId.TryParse(payload.BookId, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        var reservationResult = await bookServiceClient.ReserveBookAsync(payload.BookId, cancellationToken);

        if (reservationResult.Status == BookReservationStatus.NotFound)
        {
            return Results.NotFound(new ErrorResponse("A konyv nem talalhato."));
        }

        if (reservationResult.Status == BookReservationStatus.Unavailable)
        {
            return Results.Conflict(new ErrorResponse("A konyv jelenleg nem kolcsonozheto, mert mar ki van adva vagy nem elerheto."));
        }

        var reservedBook = reservationResult.Book!;
        var loanDocument = CreateLoanDocument(reservedBook, bookId, payload);

        try
        {
            await db.Loans.InsertOneAsync(loanDocument, cancellationToken: cancellationToken);
            return Results.Json(MapLoanResponse(loanDocument), statusCode: StatusCodes.Status201Created);
        }
        catch (MongoWriteException error) when (error.WriteError?.Code == 11000)
        {
            await bookServiceClient.ReleaseBookAsync(payload.BookId, cancellationToken);
            return Results.Conflict(new ErrorResponse("Ehhez a konyvhoz mar tartozik aktiv kolcsonzes."));
        }
        catch
        {
            await bookServiceClient.ReleaseBookAsync(payload.BookId, cancellationToken);
            throw;
        }
    }

    private static async Task<IResult> UpdateLoanAsync(string id, LoanUpdateRequest? request, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var loanId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen kolcsonzesazonosito."));
        }

        var validationResult = ValidateLoanUpdatePayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen kolcsonzesi adatok.", validationResult.Errors));
        }

        var existingLoan = await db.Loans
            .Find(Builders<BsonDocument>.Filter.Eq("_id", loanId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingLoan is null)
        {
            return Results.NotFound(new ErrorResponse("A kolcsonzes nem talalhato."));
        }

        if (!string.Equals(GetString(existingLoan, "status"), "active", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new ErrorResponse("Csak aktiv kolcsonzes modosithato."));
        }

        var payload = validationResult.Payload!;
        var loanedAt = GetDate(existingLoan, "loanedAt") ?? DateTime.UtcNow;

        if (payload.DueAt < loanedAt)
        {
            return Results.BadRequest(new ErrorResponse("A hatarido nem lehet korabbi, mint a kolcsonzes datuma."));
        }

        BsonValue notesValue = payload.Notes is null ? BsonNull.Value : new BsonString(payload.Notes);

        var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", loanId),
                Builders<BsonDocument>.Filter.Eq("status", "active")),
            Builders<BsonDocument>.Update
                .Set("borrowerName", payload.BorrowerName)
                .Set("borrowerEmail", payload.BorrowerEmail)
                .Set("notes", notesValue)
                .Set("dueAt", payload.DueAt)
                .Set("updatedAt", DateTime.UtcNow),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updatedLoan is null
            ? Results.Conflict(new ErrorResponse("A kolcsonzes idokozben lezarult."))
            : Results.Ok(MapLoanResponse(updatedLoan));
    }

    private static async Task<IResult> ReturnLoanAsync(
        string id,
        LoanReturnRequest? request,
        MongoDbContext db,
        BookServiceClient bookServiceClient,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var loanId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen kolcsonzesazonosito."));
        }

        var validationResult = ValidateLoanReturnPayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen visszahozasi adatok.", validationResult.Errors));
        }

        var existingLoan = await db.Loans
            .Find(Builders<BsonDocument>.Filter.Eq("_id", loanId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingLoan is null)
        {
            return Results.NotFound(new ErrorResponse("A kolcsonzes nem talalhato."));
        }

        if (!string.Equals(GetString(existingLoan, "status"), "active", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new ErrorResponse("A kolcsonzes mar le van zarva."));
        }

        var payload = validationResult.Payload!;
        var loanedAt = GetDate(existingLoan, "loanedAt") ?? DateTime.UtcNow;

        if (payload.ReturnedAt < loanedAt)
        {
            return Results.BadRequest(new ErrorResponse("A visszahozas datuma nem lehet korabbi, mint a kolcsonzes idopontja."));
        }

        var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", loanId),
                Builders<BsonDocument>.Filter.Eq("status", "active")),
            Builders<BsonDocument>.Update
                .Set("returnedAt", payload.ReturnedAt)
                .Set("status", "returned")
                .Set("updatedAt", DateTime.UtcNow),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (updatedLoan is null)
        {
            return Results.Conflict(new ErrorResponse("A kolcsonzes idokozben lezarult."));
        }

        await bookServiceClient.ReleaseBookAsync(GetFlexibleId(existingLoan, "bookId"), cancellationToken);
        return Results.Ok(MapLoanResponse(updatedLoan));
    }

    private static async Task<IResult> GetActiveLoanBookIdsAsync(MongoDbContext db, CancellationToken cancellationToken)
    {
        var activeLoans = await db.Loans
            .Find(Builders<BsonDocument>.Filter.Eq("status", "active"))
            .Project(Builders<BsonDocument>.Projection.Include("bookId"))
            .ToListAsync(cancellationToken);

        var bookIds = activeLoans
            .Select(loan => GetFlexibleId(loan, "bookId"))
            .Where(bookId => !string.IsNullOrWhiteSpace(bookId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Results.Ok(bookIds);
    }

    private static async Task<IResult> GetActiveLoanForBookAsync(string bookId, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(bookId, out var parsedBookId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        var activeLoan = await db.Loans
            .Find(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("status", "active"),
                BookIdentifierFilter(parsedBookId)))
            .Sort(Builders<BsonDocument>.Sort.Descending("loanedAt"))
            .FirstOrDefaultAsync(cancellationToken);

        return activeLoan is null
            ? Results.NotFound(new ErrorResponse("Nincs aktiv kolcsonzes a konyvhoz."))
            : Results.Ok(MapLoanResponse(activeLoan));
    }

    private static (ValidatedLoanCreatePayload? Payload, string[] Errors) ValidateLoanCreatePayload(LoanCreateRequest? request)
    {
        if (request is null)
        {
            return (null, ["A keres torzsenek JSON objektumnak kell lennie."]);
        }

        var errors = new List<string>();
        var bookId = request.BookId?.Trim() ?? string.Empty;
        var borrowerName = NormalizeRequiredString(request.BorrowerName);
        var borrowerEmail = NormalizeRequiredString(request.BorrowerEmail);
        var notes = NormalizeOptionalString(request.Notes);

        if (string.IsNullOrWhiteSpace(bookId)) errors.Add("A bookId megadasa kotelezo.");
        if (borrowerName is null) errors.Add("A kolcsonzo neve kotelezo.");
        if (borrowerEmail is null) errors.Add("A kolcsonzo e-mail cime kotelezo.");
        else if (!IsValidEmail(borrowerEmail)) errors.Add("A kolcsonzo e-mail cime ervenytelen.");
        if (string.IsNullOrWhiteSpace(request.DueAt)) errors.Add("A hatarido megadasa kotelezo.");
        if (!TryParseClientDate(request.DueAt, out var dueAt)) errors.Add("A visszahozasi hatarido ervenytelen.");
        if (dueAt is not null && dueAt.Value < DateTime.UtcNow) errors.Add("A hatarido nem lehet korabbi, mint a kolcsonzes datuma.");

        return errors.Count > 0
            ? (null, errors.ToArray())
            : (new ValidatedLoanCreatePayload(bookId, borrowerName!, borrowerEmail!, dueAt!.Value, notes), []);
    }

    private static (ValidatedLoanUpdatePayload? Payload, string[] Errors) ValidateLoanUpdatePayload(LoanUpdateRequest? request)
    {
        if (request is null)
        {
            return (null, ["A keres torzsenek JSON objektumnak kell lennie."]);
        }

        var errors = new List<string>();
        var borrowerName = NormalizeRequiredString(request.BorrowerName);
        var borrowerEmail = NormalizeRequiredString(request.BorrowerEmail);
        var notes = NormalizeOptionalString(request.Notes);

        if (borrowerName is null) errors.Add("A kolcsonzo neve kotelezo.");
        if (borrowerEmail is null) errors.Add("A kolcsonzo e-mail cime kotelezo.");
        else if (!IsValidEmail(borrowerEmail)) errors.Add("A kolcsonzo e-mail cime ervenytelen.");
        if (string.IsNullOrWhiteSpace(request.DueAt)) errors.Add("A hatarido megadasa kotelezo.");
        if (!TryParseClientDate(request.DueAt, out var dueAt)) errors.Add("A visszahozasi hatarido ervenytelen.");

        return errors.Count > 0
            ? (null, errors.ToArray())
            : (new ValidatedLoanUpdatePayload(borrowerName!, borrowerEmail!, dueAt!.Value, notes), []);
    }

    private static (ValidatedLoanReturnPayload? Payload, string[] Errors) ValidateLoanReturnPayload(LoanReturnRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReturnedAt))
        {
            return (new ValidatedLoanReturnPayload(DateTime.UtcNow), []);
        }

        return TryParseClientDate(request.ReturnedAt, out var returnedAt)
            ? (new ValidatedLoanReturnPayload(returnedAt!.Value), [])
            : (null, ["A visszahozas datuma ervenytelen."]);
    }

    private static string? NormalizeRequiredString(string? value)
    {
        var normalized = NormalizeOptionalString(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidEmail(string value)
        => Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static bool TryParseClientDate(string? value, out DateTime? parsedDate)
    {
        parsedDate = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var success = DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date);

        if (!success)
        {
            return false;
        }

        parsedDate = date;
        return true;
    }

    private static FilterDefinition<BsonDocument> BookIdentifierFilter(ObjectId bookId)
    {
        return Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("bookId", bookId),
            Builders<BsonDocument>.Filter.Eq("bookId", bookId.ToString()));
    }

    private static BsonDocument CreateLoanDocument(BookInventoryResponse book, ObjectId bookId, ValidatedLoanCreatePayload payload)
    {
        var now = DateTime.UtcNow;

        return new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["bookId"] = bookId,
            ["bookTitle"] = book.Title,
            ["bookAuthor"] = book.Author,
            ["borrowerName"] = payload.BorrowerName,
            ["borrowerEmail"] = payload.BorrowerEmail,
            ["notes"] = payload.Notes is null ? BsonNull.Value : payload.Notes,
            ["loanedAt"] = now,
            ["dueAt"] = payload.DueAt,
            ["returnedAt"] = BsonNull.Value,
            ["status"] = "active",
            ["createdAt"] = now,
            ["updatedAt"] = now,
        };
    }

    private static LoanResponse MapLoanResponse(BsonDocument loan)
    {
        return new LoanResponse(
            GetFlexibleId(loan, "_id"),
            GetFlexibleId(loan, "bookId"),
            GetString(loan, "bookTitle"),
            GetString(loan, "bookAuthor"),
            GetString(loan, "borrowerName"),
            GetNullableString(loan, "borrowerEmail"),
            GetNullableString(loan, "notes"),
            GetDate(loan, "loanedAt") ?? DateTime.UtcNow,
            GetDate(loan, "dueAt"),
            GetDate(loan, "returnedAt"),
            GetString(loan, "status"),
            GetDate(loan, "createdAt"),
            GetDate(loan, "updatedAt"));
    }

    private static string GetFlexibleId(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return string.Empty;
        }

        return value.BsonType switch
        {
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.String => value.AsString,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string GetString(BsonDocument document, string fieldName)
        => document.TryGetValue(fieldName, out var value) && !value.IsBsonNull ? value.ToString() ?? string.Empty : string.Empty;

    private static string? GetNullableString(BsonDocument document, string fieldName)
        => document.TryGetValue(fieldName, out var value) && !value.IsBsonNull ? value.ToString() : null;

    private static DateTime? GetDate(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return null;
        }

        if (value.BsonType == BsonType.DateTime)
        {
            return value.ToUniversalTime();
        }

        return value.BsonType == BsonType.String
            && DateTime.TryParse(
                value.AsString,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
            ? parsed
            : null;
    }
}
