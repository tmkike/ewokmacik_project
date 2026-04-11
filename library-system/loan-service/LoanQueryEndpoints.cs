using MongoDB.Driver;

static class LoanQueryEndpoints
{
    public static void Map(RouteGroupBuilder loans)
    {
        loans.MapGet("/", GetLoansAsync)
            .WithName("GetLoans");
        loans.MapGet("/active", GetActiveLoansAsync)
            .WithName("GetActiveLoans");
    }

    public static void MapInternal(RouteGroupBuilder internalLoans)
    {
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
        var filter = Builders<LoanDocument>.Filter.Eq(loan => loan.Status, LoanStatus.Active);

        if (!string.IsNullOrWhiteSpace(bookId))
        {
            if (!LoanObjectIds.TryParse(bookId, out var parsedBookId))
            {
                return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
            }

            filter = Builders<LoanDocument>.Filter.And(filter, LoanDocumentMapper.BookIdentifierFilter(parsedBookId));
        }

        var loans = await db.Loans
            .Find(filter)
            .Sort(Builders<LoanDocument>.Sort.Descending(loan => loan.LoanedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(loans.Select(LoanDocumentMapper.MapLoanResponse));
    }

    private static async Task<IResult> GetActiveLoanBookIdsAsync(MongoDbContext db, CancellationToken cancellationToken)
    {
        var activeLoans = await db.Loans
            .Find(Builders<LoanDocument>.Filter.Eq(loan => loan.Status, LoanStatus.Active))
            .Project(loan => loan.BookId)
            .ToListAsync(cancellationToken);

        var bookIds = activeLoans
            .Select(LoanObjectIds.ToString)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Results.Ok(bookIds);
    }

    private static async Task<IResult> GetActiveLoanForBookAsync(string bookId, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!LoanObjectIds.TryParse(bookId, out var parsedBookId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        var activeLoan = await db.Loans
            .Find(Builders<LoanDocument>.Filter.And(
                Builders<LoanDocument>.Filter.Eq(loan => loan.Status, LoanStatus.Active),
                LoanDocumentMapper.BookIdentifierFilter(parsedBookId)))
            .Sort(Builders<LoanDocument>.Sort.Descending(loan => loan.LoanedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return activeLoan is null
            ? Results.NotFound(new ErrorResponse("Nincs aktiv kolcsonzes a konyvhoz."))
            : Results.Ok(LoanDocumentMapper.MapLoanResponse(activeLoan));
    }
}
