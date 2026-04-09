using MongoDB.Bson;
using MongoDB.Driver;

static class BookApi
{
    public static void Map(WebApplication app)
    {
        var books = app.MapGroup("/api/books");
        books.MapGet("/", GetBooksAsync);
        books.MapGet("/{id}", GetBookByIdAsync);
        books.MapPost("/", CreateBookAsync);
        books.MapPut("/{id}", UpdateBookAsync);
        books.MapPatch("/{id}/availability", UpdateBookAvailabilityAsync);
        books.MapDelete("/{id}", DeleteBookAsync);

        var internalBooks = app.MapGroup("/internal/books");
        internalBooks.MapPost("/{id}/reserve", ReserveBookAsync);
        internalBooks.MapPost("/{id}/release", ReleaseBookAsync);
    }

    private static async Task<IResult> GetBooksAsync(
        HttpRequest request,
        MongoDbContext db,
        LoanServiceClient loanServiceClient,
        CancellationToken cancellationToken)
    {
        var filterResult = BookValidation.BuildBooksFilter(request.Query);

        if (filterResult.ErrorMessage is not null)
        {
            return Results.BadRequest(new ErrorResponse(filterResult.ErrorMessage));
        }

        var books = await db.Books
            .Find(filterResult.Filter)
            .Sort(Builders<BookDocument>.Sort.Ascending(book => book.Title))
            .ToListAsync(cancellationToken);

        var activeLoanBookIds = await loanServiceClient.GetActiveLoanBookIdsAsync(cancellationToken);
        return Results.Ok(books.Select(book => BookDocumentMapper.MapBookResponse(
            book,
            activeLoanBookIds.Contains(BookDocumentMapper.GetId(book)))));
    }

    private static async Task<IResult> GetBookByIdAsync(
        string id,
        MongoDbContext db,
        LoanServiceClient loanServiceClient,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        var book = await db.Books
            .Find(Builders<BookDocument>.Filter.Eq(item => item.Id, bookId))
            .FirstOrDefaultAsync(cancellationToken);

        if (book is null)
        {
            return Results.NotFound(new ErrorResponse("A könyv nem található."));
        }

        var activeLoan = await loanServiceClient.GetActiveLoanForBookAsync(id, cancellationToken);
        return Results.Ok(BookDocumentMapper.MapBookDetailResponse(book, activeLoan));
    }

    private static async Task<IResult> CreateBookAsync(BookUpsertRequest? request, MongoDbContext db, CancellationToken cancellationToken)
    {
        var validationResult = BookValidation.ValidateBookPayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Érvénytelen könyvadatok.", validationResult.Errors));
        }

        var payload = validationResult.Payload!;
        var bookDocument = new BookDocument
        {
            Id = ObjectId.GenerateNewId(),
            Title = payload.Title,
            Author = payload.Author,
            Year = payload.Year,
            Genre = payload.Genre,
            Available = payload.Available,
        };

        await db.Books.InsertOneAsync(bookDocument, cancellationToken: cancellationToken);
        return Results.Json(BookDocumentMapper.MapBookResponse(bookDocument), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateBookAsync(
        string id,
        BookUpsertRequest? request,
        MongoDbContext db,
        LoanServiceClient loanServiceClient,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        var validationResult = BookValidation.ValidateBookPayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Érvénytelen könyvadatok.", validationResult.Errors));
        }

        var payload = validationResult.Payload!;

        if (payload.Available && await loanServiceClient.HasActiveLoanAsync(id, cancellationToken))
        {
            return Results.Conflict(new ErrorResponse("Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek."));
        }

        var updatedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BookDocument>.Filter.Eq(book => book.Id, bookId),
            Builders<BookDocument>.Update
                .Set(book => book.Title, payload.Title)
                .Set(book => book.Author, payload.Author)
                .Set(book => book.Year, payload.Year)
                .Set(book => book.Genre, payload.Genre)
                .Set(book => book.Available, payload.Available),
            new FindOneAndUpdateOptions<BookDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updatedBook is null
            ? Results.NotFound(new ErrorResponse("A könyv nem található."))
            : Results.Ok(BookDocumentMapper.MapBookResponse(updatedBook));
    }

    private static async Task<IResult> UpdateBookAvailabilityAsync(
        string id,
        BookAvailabilityRequest? request,
        MongoDbContext db,
        LoanServiceClient loanServiceClient,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        if (request?.Available is null)
        {
            return Results.BadRequest(new ValidationErrorResponse(
                "Érvénytelen elérhetőségi adat.",
                ["Az available mező kötelező, és logikai értéknek kell lennie."]));
        }

        if (request.Available.Value && await loanServiceClient.HasActiveLoanAsync(id, cancellationToken))
        {
            return Results.Conflict(new ErrorResponse("Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek."));
        }

        var updatedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BookDocument>.Filter.Eq(book => book.Id, bookId),
            Builders<BookDocument>.Update.Set(book => book.Available, request.Available.Value),
            new FindOneAndUpdateOptions<BookDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updatedBook is null
            ? Results.NotFound(new ErrorResponse("A könyv nem található."))
            : Results.Ok(BookDocumentMapper.MapBookResponse(updatedBook));
    }

    private static async Task<IResult> DeleteBookAsync(
        string id,
        MongoDbContext db,
        LoanServiceClient loanServiceClient,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        if (await loanServiceClient.HasActiveLoanAsync(id, cancellationToken))
        {
            return Results.Conflict(new ErrorResponse("Aktív kölcsönzés alatt álló könyv nem törölhető."));
        }

        var result = await db.Books.DeleteOneAsync(
            Builders<BookDocument>.Filter.Eq(book => book.Id, bookId),
            cancellationToken);

        return result.DeletedCount == 0
            ? Results.NotFound(new ErrorResponse("A könyv nem található."))
            : Results.NoContent();
    }

    private static async Task<IResult> ReserveBookAsync(string id, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        var reservedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BookDocument>.Filter.And(
                Builders<BookDocument>.Filter.Eq(book => book.Id, bookId),
                Builders<BookDocument>.Filter.Eq(book => book.Available, true)),
            Builders<BookDocument>.Update.Set(book => book.Available, false),
            new FindOneAndUpdateOptions<BookDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (reservedBook is not null)
        {
            return Results.Ok(BookDocumentMapper.MapBookInventoryResponse(reservedBook));
        }

        var exists = await db.Books.Find(Builders<BookDocument>.Filter.Eq(book => book.Id, bookId)).AnyAsync(cancellationToken);

        return !exists
            ? Results.NotFound(new ErrorResponse("A könyv nem található."))
            : Results.Conflict(new ErrorResponse("A könyv jelenleg nem kölcsönözhető, mert már ki van adva vagy nem elérhető."));
    }

    private static async Task<IResult> ReleaseBookAsync(string id, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        var releasedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BookDocument>.Filter.Eq(book => book.Id, bookId),
            Builders<BookDocument>.Update.Set(book => book.Available, true),
            new FindOneAndUpdateOptions<BookDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return releasedBook is null
            ? Results.NotFound(new ErrorResponse("A könyv nem található."))
            : Results.Ok(BookDocumentMapper.MapBookInventoryResponse(releasedBook));
    }
}
