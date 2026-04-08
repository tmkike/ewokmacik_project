using System.Globalization;
using System.Text.RegularExpressions;
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
        var filterResult = BuildBooksFilter(request.Query);

        if (filterResult.ErrorMessage is not null)
        {
            return Results.BadRequest(new ErrorResponse(filterResult.ErrorMessage));
        }

        var books = await db.Books
            .Find(filterResult.Filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("title"))
            .ToListAsync(cancellationToken);

        var activeLoanBookIds = await loanServiceClient.GetActiveLoanBookIdsAsync(cancellationToken);
        return Results.Ok(books.Select(book => MapBookResponse(book, activeLoanBookIds.Contains(GetId(book)))));
    }

    private static async Task<IResult> GetBookByIdAsync(
        string id,
        MongoDbContext db,
        LoanServiceClient loanServiceClient,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        var book = await db.Books
            .Find(Builders<BsonDocument>.Filter.Eq("_id", bookId))
            .FirstOrDefaultAsync(cancellationToken);

        if (book is null)
        {
            return Results.NotFound(new ErrorResponse("A konyv nem talalhato."));
        }

        var activeLoan = await loanServiceClient.GetActiveLoanForBookAsync(id, cancellationToken);
        return Results.Ok(MapBookDetailResponse(book, activeLoan));
    }

    private static async Task<IResult> CreateBookAsync(BookUpsertRequest? request, MongoDbContext db, CancellationToken cancellationToken)
    {
        var validationResult = ValidateBookPayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen konyvadatok.", validationResult.Errors));
        }

        var payload = validationResult.Payload!;
        var bookDocument = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["title"] = payload.Title,
            ["author"] = payload.Author,
            ["year"] = payload.Year,
            ["genre"] = payload.Genre,
            ["available"] = payload.Available,
        };

        await db.Books.InsertOneAsync(bookDocument, cancellationToken: cancellationToken);
        return Results.Json(MapBookResponse(bookDocument), statusCode: StatusCodes.Status201Created);
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
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        var validationResult = ValidateBookPayload(request);

        if (validationResult.Errors.Length > 0)
        {
            return Results.BadRequest(new ValidationErrorResponse("Ervenytelen konyvadatok.", validationResult.Errors));
        }

        var payload = validationResult.Payload!;

        if (payload.Available && await loanServiceClient.HasActiveLoanAsync(id, cancellationToken))
        {
            return Results.Conflict(new ErrorResponse("Aktiv kolcsonzes mellett a konyv nem jelolheto elerhetonek."));
        }

        var updatedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", bookId),
            Builders<BsonDocument>.Update
                .Set("title", payload.Title)
                .Set("author", payload.Author)
                .Set("year", payload.Year)
                .Set("genre", payload.Genre)
                .Set("available", payload.Available),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updatedBook is null
            ? Results.NotFound(new ErrorResponse("A konyv nem talalhato."))
            : Results.Ok(MapBookResponse(updatedBook));
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
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        if (request?.Available is null)
        {
            return Results.BadRequest(new ValidationErrorResponse(
                "Ervenytelen elerhetosegi adat.",
                ["Az available mezo kotelezo, es logikai erteknek kell lennie."]));
        }

        if (request.Available.Value && await loanServiceClient.HasActiveLoanAsync(id, cancellationToken))
        {
            return Results.Conflict(new ErrorResponse("Aktiv kolcsonzes mellett a konyv nem jelolheto elerhetonek."));
        }

        var updatedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", bookId),
            Builders<BsonDocument>.Update.Set("available", request.Available.Value),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updatedBook is null
            ? Results.NotFound(new ErrorResponse("A konyv nem talalhato."))
            : Results.Ok(MapBookResponse(updatedBook));
    }

    private static async Task<IResult> DeleteBookAsync(
        string id,
        MongoDbContext db,
        LoanServiceClient loanServiceClient,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        if (await loanServiceClient.HasActiveLoanAsync(id, cancellationToken))
        {
            return Results.Conflict(new ErrorResponse("Aktiv kolcsonzes alatt allo konyv nem torolheto."));
        }

        var result = await db.Books.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", bookId),
            cancellationToken);

        return result.DeletedCount == 0
            ? Results.NotFound(new ErrorResponse("A konyv nem talalhato."))
            : Results.NoContent();
    }

    private static async Task<IResult> ReserveBookAsync(string id, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        var reservedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", bookId),
                Builders<BsonDocument>.Filter.Eq("available", true)),
            Builders<BsonDocument>.Update.Set("available", false),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (reservedBook is not null)
        {
            return Results.Ok(MapBookInventoryResponse(reservedBook));
        }

        var existingBook = await db.Books
            .Find(Builders<BsonDocument>.Filter.Eq("_id", bookId))
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .FirstOrDefaultAsync(cancellationToken);

        return existingBook is null
            ? Results.NotFound(new ErrorResponse("A konyv nem talalhato."))
            : Results.Conflict(new ErrorResponse("A konyv jelenleg nem kolcsonozheto, mert mar ki van adva vagy nem elerheto."));
    }

    private static async Task<IResult> ReleaseBookAsync(string id, MongoDbContext db, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var bookId))
        {
            return Results.BadRequest(new ErrorResponse("Ervenytelen konyvazonosito."));
        }

        var releasedBook = await db.Books.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", bookId),
            Builders<BsonDocument>.Update.Set("available", true),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return releasedBook is null
            ? Results.NotFound(new ErrorResponse("A konyv nem talalhato."))
            : Results.Ok(MapBookInventoryResponse(releasedBook));
    }

    private static BookQueryFilterResult BuildBooksFilter(IQueryCollection query)
    {
        var filters = new List<FilterDefinition<BsonDocument>>();
        var search = query["search"].ToString().Trim();
        var title = query["title"].ToString().Trim();
        var author = query["author"].ToString().Trim();
        var genre = query["genre"].ToString().Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add(Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Regex("title", Contains(search)),
                Builders<BsonDocument>.Filter.Regex("author", Contains(search)),
                Builders<BsonDocument>.Filter.Regex("genre", Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            filters.Add(Builders<BsonDocument>.Filter.Regex("title", Contains(title)));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            filters.Add(Builders<BsonDocument>.Filter.Regex("author", Contains(author)));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            filters.Add(Builders<BsonDocument>.Filter.Regex("genre", Exact(genre)));
        }

        var available = query["available"].ToString();

        if (!string.IsNullOrWhiteSpace(available))
        {
            if (!bool.TryParse(available, out var availableValue))
            {
                return new BookQueryFilterResult(
                    Builders<BsonDocument>.Filter.Empty,
                    "Az elerhetoseg szuroje csak true vagy false lehet.");
            }

            filters.Add(Builders<BsonDocument>.Filter.Eq("available", availableValue));
        }

        return new BookQueryFilterResult(
            filters.Count == 0 ? Builders<BsonDocument>.Filter.Empty : Builders<BsonDocument>.Filter.And(filters),
            null);
    }

    private static (ValidatedBookPayload? Payload, string[] Errors) ValidateBookPayload(BookUpsertRequest? request)
    {
        if (request is null)
        {
            return (null, ["A keres torzsenek JSON objektumnak kell lennie."]);
        }

        var errors = new List<string>();
        var title = NormalizeRequiredString(request.Title);
        var author = NormalizeRequiredString(request.Author);
        var genre = NormalizeRequiredString(request.Genre);

        if (title is null) errors.Add("A cim kotelezo, es nem lehet ures.");
        if (author is null) errors.Add("A szerzo megadasa kotelezo, es nem lehet ures.");
        if (request.Year is null || request.Year < 0) errors.Add("A kiadas eve kotelezo, es nemnegativ egesz szamnak kell lennie.");
        if (genre is null) errors.Add("A kategoria megadasa kotelezo, es nem lehet ures.");
        if (request.Available is null) errors.Add("Az elerhetoseg megadasa kotelezo, es logikai erteknek kell lennie.");

        return errors.Count > 0
            ? (null, errors.ToArray())
            : (new ValidatedBookPayload(title!, author!, request.Year!.Value, genre!, request.Available!.Value), []);
    }

    private static string? NormalizeRequiredString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static BookResponse MapBookResponse(BsonDocument book, bool hasActiveLoan = false)
    {
        return new BookResponse(
            GetId(book),
            GetString(book, "title"),
            GetString(book, "author"),
            GetInt(book, "year"),
            GetString(book, "genre"),
            GetBool(book, "available"),
            hasActiveLoan);
    }

    private static BookInventoryResponse MapBookInventoryResponse(BsonDocument book)
    {
        return new BookInventoryResponse(
            GetId(book),
            GetString(book, "title"),
            GetString(book, "author"),
            GetInt(book, "year"),
            GetString(book, "genre"),
            GetBool(book, "available"));
    }

    private static BookDetailResponse MapBookDetailResponse(BsonDocument book, LoanResponse? activeLoan)
    {
        return new BookDetailResponse(
            GetId(book),
            GetString(book, "title"),
            GetString(book, "author"),
            GetInt(book, "year"),
            GetString(book, "genre"),
            GetBool(book, "available"),
            activeLoan is not null,
            activeLoan);
    }

    private static string GetId(BsonDocument document) => FlexibleId(document, "_id");

    private static string FlexibleId(BsonDocument document, string fieldName)
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

    private static int GetInt(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return 0;
        }

        return value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => (int)value.AsInt64,
            BsonType.Double => (int)value.AsDouble,
            _ => 0,
        };
    }

    private static bool GetBool(BsonDocument document, string fieldName)
        => document.TryGetValue(fieldName, out var value) && !value.IsBsonNull && value.ToBoolean();

    private static BsonRegularExpression Contains(string value) => new(Regex.Escape(value), "i");

    private static BsonRegularExpression Exact(string value) => new($"^{Regex.Escape(value)}$", "i");
}
