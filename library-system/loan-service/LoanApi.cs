using MongoDB.Bson;
using MongoDB.Driver;

static class LoanApi
{
    public static void Map(WebApplication app)
    {
        var loans = app.MapGroup("/api/loans")
            .WithTags("Loans");

        loans.MapGet("/", GetLoansAsync)
            .WithName("GetLoans");
        loans.MapGet("/active", GetActiveLoansAsync)
            .WithName("GetActiveLoans");
        loans.MapPost("/", CreateLoanAsync)
            .WithName("CreateLoan");
        loans.MapPut("/{id}", UpdateLoanAsync)
            .WithName("UpdateLoan");
        loans.MapPut("/{id}/return", ReturnLoanAsync)
            .WithName("ReturnLoan");

        var internalLoans = app.MapGroup("/internal/loans");
        internalLoans.MapGet("/active-book-ids", GetActiveLoanBookIdsAsync)
            .ExcludeFromDescription();
        internalLoans.MapGet("/books/{bookId}/active", GetActiveLoanForBookAsync)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> GetLoansAsync(MongoDbContext db, CancellationToken cancellationToken)
    {
        var loans = await db.Loans
            .Find(Builders<LoanDocument>.Filter.Empty)
            .Sort(Builders<LoanDocument>.Sort.Descending(loan => loan.LoanedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(loans.Select(LoanDocumentMapper.MapLoanResponse));
    }

    private static async Task<IResult> GetActiveLoansAsync(string? bookId, MongoDbContext db, CancellationToken cancellationToken)
    {
        var filter = Builders<LoanDocument>.Filter.Eq(loan => loan.Status, "active");

        if (!string.IsNullOrWhiteSpace(bookId))
        {
            if (!ObjectId.TryParse(bookId, out var parsedBookId))
            {
                return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
            }

            filter = Builders<LoanDocument>.Filter.And(filter, LoanDocumentMapper.BookIdentifierFilter(parsedBookId));
        }

        var loans = await db.Loans
            .Find(filter)
            .Sort(Builders<LoanDocument>.Sort.Descending(loan => loan.LoanedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(loans.Select(LoanDocumentMapper.MapLoanResponse));
    }

    private static async Task<IResult> CreateLoanAsync(
        LoanCreateRequest? request,
        MongoDbContext db,
        BookServiceClient bookServiceClient,
        BookReleaseSyncService bookReleaseSyncService,
        CancellationToken cancellationToken)
    {
        var validationResult = LoanValidation.ValidateLoanCreatePayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Érvénytelen kölcsönzési adatok.", validationResult.Errors));
        }

        var payload = validationResult.Payload!;

        if (!ObjectId.TryParse(payload.BookId, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        var reservationResult = await bookServiceClient.ReserveBookAsync(payload.BookId, cancellationToken);

        if (reservationResult.Status == BookReservationStatus.NotFound)
        {
            return Results.NotFound(new ErrorResponse("A könyv nem található."));
        }

        if (reservationResult.Status == BookReservationStatus.Unavailable)
        {
            return Results.Conflict(new ErrorResponse("A könyv jelenleg nem kölcsönözhető, mert már ki van adva vagy nem elérhető."));
        }

        var reservedBook = reservationResult.Book!;
        var loanDocument = LoanDocumentMapper.CreateLoanDocument(reservedBook, bookId, payload);
        var loanId = LoanDocumentMapper.GetFlexibleId(loanDocument, "_id");

        try
        {
            await db.Loans.InsertOneAsync(loanDocument, cancellationToken: cancellationToken);
            return Results.Json(LoanDocumentMapper.MapLoanResponse(loanDocument), statusCode: StatusCodes.Status201Created);
        }
        catch (MongoWriteException error) when (error.WriteError?.Code == 11000)
        {
            await bookReleaseSyncService.ReleaseOrQueueAsync(payload.BookId, loanId, cancellationToken);
            return Results.Conflict(new ErrorResponse("Ehhez a könyvhöz már tartozik aktív kölcsönzés."));
        }
        catch
        {
            await bookReleaseSyncService.ReleaseOrQueueAsync(payload.BookId, loanId, cancellationToken);
            throw;
        }
    }

    private static async Task<IResult> UpdateLoanAsync(
        string id,
        LoanUpdateRequest? request,
        MongoDbContext db,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var loanId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen kölcsönzésazonosító."));
        }

        var validationResult = LoanValidation.ValidateLoanUpdatePayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Érvénytelen kölcsönzési adatok.", validationResult.Errors));
        }

        var existingLoan = await db.Loans
            .Find(Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingLoan is null)
        {
            return Results.NotFound(new ErrorResponse("A kölcsönzés nem található."));
        }

        if (!string.Equals(existingLoan.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new ErrorResponse("Csak aktív kölcsönzés módosítható."));
        }

        var payload = validationResult.Payload!;

        if (LoanDateRules.IsBeforeLoanedAt(payload.DueDate, existingLoan.LoanedAt))
        {
            return Results.BadRequest(new ErrorResponse("A határidő nem lehet korábbi, mint a kölcsönzés napja."));
        }

        var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
            Builders<LoanDocument>.Filter.And(
                Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId),
                Builders<LoanDocument>.Filter.Eq(loan => loan.Status, "active")),
            Builders<LoanDocument>.Update
                .Set(loan => loan.BorrowerName, payload.BorrowerName)
                .Set(loan => loan.BorrowerEmail, payload.BorrowerEmail)
                .Set(loan => loan.Notes, payload.Notes)
                .Set(loan => loan.DueAt, LoanDateRules.ToUtcDateTime(payload.DueDate))
                .Set(loan => loan.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<LoanDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updatedLoan is null
            ? Results.Conflict(new ErrorResponse("A kölcsönzés időközben lezárult."))
            : Results.Ok(LoanDocumentMapper.MapLoanResponse(updatedLoan));
    }

    private static async Task<IResult> ReturnLoanAsync(
        string id,
        LoanReturnRequest? request,
        MongoDbContext db,
        BookReleaseSyncService bookReleaseSyncService,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var loanId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen kölcsönzésazonosító."));
        }

        var validationResult = LoanValidation.ValidateLoanReturnPayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Érvénytelen visszahozási adatok.", validationResult.Errors));
        }

        var existingLoan = await db.Loans
            .Find(Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingLoan is null)
        {
            return Results.NotFound(new ErrorResponse("A kölcsönzés nem található."));
        }

        if (!string.Equals(existingLoan.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new ErrorResponse("A kölcsönzés már le van zárva."));
        }

        var payload = validationResult.Payload!;

        if (LoanDateRules.IsBeforeLoanedAt(payload.ReturnedDate, existingLoan.LoanedAt))
        {
            return Results.BadRequest(new ErrorResponse("A visszahozás dátuma nem lehet korábbi, mint a kölcsönzés napja."));
        }

        var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
            Builders<LoanDocument>.Filter.And(
                Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId),
                Builders<LoanDocument>.Filter.Eq(loan => loan.Status, "active")),
            Builders<LoanDocument>.Update
                .Set(loan => loan.ReturnedAt, LoanDateRules.ToUtcDateTime(payload.ReturnedDate))
                .Set(loan => loan.Status, "returned")
                .Set(loan => loan.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<LoanDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (updatedLoan is null)
        {
            return Results.Conflict(new ErrorResponse("A kölcsönzés időközben lezárult."));
        }

        var bookId = LoanDocumentMapper.GetFlexibleId(existingLoan, "bookId");
        var releaseCompleted = await bookReleaseSyncService.ReleaseOrQueueAsync(bookId, id, cancellationToken);
        var response = LoanDocumentMapper.MapLoanResponse(updatedLoan);

        return releaseCompleted
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> GetActiveLoanBookIdsAsync(MongoDbContext db, CancellationToken cancellationToken)
    {
        var activeLoans = await db.Loans
            .Find(Builders<LoanDocument>.Filter.Eq(loan => loan.Status, "active"))
            .Project(loan => loan.BookId)
            .ToListAsync(cancellationToken);

        var bookIds = activeLoans
            .Select(bookId => bookId.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Results.Ok(bookIds);
    }

    private static async Task<IResult> GetActiveLoanForBookAsync(string bookId, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(bookId, out var parsedBookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        var activeLoan = await db.Loans
            .Find(Builders<LoanDocument>.Filter.And(
                Builders<LoanDocument>.Filter.Eq(loan => loan.Status, "active"),
                LoanDocumentMapper.BookIdentifierFilter(parsedBookId)))
            .Sort(Builders<LoanDocument>.Sort.Descending(loan => loan.LoanedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return activeLoan is null
            ? Results.NotFound(new ErrorResponse("Nincs aktív kölcsönzés a könyvhöz."))
            : Results.Ok(LoanDocumentMapper.MapLoanResponse(activeLoan));
    }
}
