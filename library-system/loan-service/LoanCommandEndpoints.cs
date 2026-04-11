using MongoDB.Driver;

static class LoanCommandEndpoints
{
    public static void Map(RouteGroupBuilder loans)
    {
        loans.MapPost("/", CreateLoanAsync)
            .WithName("CreateLoan");
        loans.MapPut("/{id}", UpdateLoanAsync)
            .WithName("UpdateLoan");
        loans.MapPut("/{id}/return", ReturnLoanAsync)
            .WithName("ReturnLoan");
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
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen kolcsonzesi adatok.", validationResult.Errors));
        }

        var payload = validationResult.Payload!;

        if (!LoanObjectIds.TryParse(payload.BookId, out var bookId))
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
            return Results.Conflict(new ErrorResponse("A konyv jelenleg nem kolcsonozheto, mert mar ki van adva vagy nem erheto el."));
        }

        var reservedBook = reservationResult.Book!;
        var loanDocument = LoanDocumentMapper.CreateLoanDocument(reservedBook, bookId, payload);
        var loanId = LoanDocumentMapper.GetLoanId(loanDocument);

        try
        {
            await db.Loans.InsertOneAsync(loanDocument, cancellationToken: cancellationToken);
            return Results.Json(LoanDocumentMapper.MapLoanResponse(loanDocument), statusCode: StatusCodes.Status201Created);
        }
        catch (MongoWriteException error) when (error.WriteError?.Code == 11000)
        {
            await bookReleaseSyncService.ReleaseOrQueueAsync(payload.BookId, loanId, cancellationToken);
            return Results.Conflict(new ErrorResponse("Ehhez a konyvhoz mar tartozik aktiv kolcsonzes."));
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
        if (!LoanObjectIds.TryParse(id, out var loanId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen kolcsonzesazonosito."));
        }

        var validationResult = LoanValidation.ValidateLoanUpdatePayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen kolcsonzesi adatok.", validationResult.Errors));
        }

        var existingLoan = await db.Loans
            .Find(Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingLoan is null)
        {
            return Results.NotFound(new ErrorResponse("A kolcsonzes nem talalhato."));
        }

        if (!LoanStatus.IsActive(existingLoan.Status))
        {
            return Results.Conflict(new ErrorResponse("Csak aktiv kolcsonzes modosithato."));
        }

        var payload = validationResult.Payload!;

        if (LoanDateRules.IsBeforeLoanedAt(payload.DueDate, existingLoan.LoanedAt))
        {
            return Results.BadRequest(new ErrorResponse("A hatarido nem lehet korabbi, mint a kolcsonzes napja."));
        }

        var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
            Builders<LoanDocument>.Filter.And(
                Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId),
                Builders<LoanDocument>.Filter.Eq(loan => loan.Status, LoanStatus.Active)),
            Builders<LoanDocument>.Update
                .Set(loan => loan.BorrowerName, payload.BorrowerName)
                .Set(loan => loan.BorrowerEmail, payload.BorrowerEmail)
                .Set(loan => loan.Notes, payload.Notes)
                .Set(loan => loan.DueAt, LoanDateRules.ToUtcDateTime(payload.DueDate))
                .Set(loan => loan.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<LoanDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updatedLoan is null
            ? Results.Conflict(new ErrorResponse("A kolcsonzes idokozben lezarult."))
            : Results.Ok(LoanDocumentMapper.MapLoanResponse(updatedLoan));
    }

    private static async Task<IResult> ReturnLoanAsync(
        string id,
        LoanReturnRequest? request,
        MongoDbContext db,
        BookReleaseSyncService bookReleaseSyncService,
        CancellationToken cancellationToken)
    {
        if (!LoanObjectIds.TryParse(id, out var loanId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen kolcsonzesazonosito."));
        }

        var validationResult = LoanValidation.ValidateLoanReturnPayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen visszahozasi adatok.", validationResult.Errors));
        }

        var existingLoan = await db.Loans
            .Find(Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingLoan is null)
        {
            return Results.NotFound(new ErrorResponse("A kolcsonzes nem talalhato."));
        }

        if (!LoanStatus.IsActive(existingLoan.Status))
        {
            return Results.Conflict(new ErrorResponse("A kolcsonzes mar le van zarva."));
        }

        var payload = validationResult.Payload!;

        if (LoanDateRules.IsBeforeLoanedAt(payload.ReturnedDate, existingLoan.LoanedAt))
        {
            return Results.BadRequest(new ErrorResponse("A visszahozas datuma nem lehet korabbi, mint a kolcsonzes napja."));
        }

        var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
            Builders<LoanDocument>.Filter.And(
                Builders<LoanDocument>.Filter.Eq(loan => loan.Id, loanId),
                Builders<LoanDocument>.Filter.Eq(loan => loan.Status, LoanStatus.Active)),
            Builders<LoanDocument>.Update
                .Set(loan => loan.ReturnedAt, LoanDateRules.ToUtcDateTime(payload.ReturnedDate))
                .Set(loan => loan.Status, LoanStatus.Returned)
                .Set(loan => loan.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<LoanDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (updatedLoan is null)
        {
            return Results.Conflict(new ErrorResponse("A kolcsonzes idokozben lezarult."));
        }

        var bookId = LoanDocumentMapper.GetBookId(existingLoan);
        var releaseCompleted = await bookReleaseSyncService.ReleaseOrQueueAsync(bookId, id, cancellationToken);
        var response = LoanDocumentMapper.MapLoanResponse(updatedLoan);

        return releaseCompleted
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status202Accepted);
    }
}
