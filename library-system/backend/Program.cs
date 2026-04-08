using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

var configuredPort = builder.Configuration["PORT"];

if (!string.IsNullOrWhiteSpace(configuredPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{configuredPort}");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["MONGODB_URI"]
        ?? configuration.GetConnectionString("MongoDb")
        ?? "mongodb://localhost:27017";
    var databaseName = configuration["MONGODB_DB"]
        ?? configuration["MongoDb:DatabaseName"]
        ?? "library";

    return new MongoDbContext(connectionString, databaseName);
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (exception is not null)
        {
            logger.LogError(exception, "Váratlan backend hiba történt.");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ErrorResponse("Belső szerverhiba történt."));
    });
});

app.UseCors();

await app.Services.GetRequiredService<MongoDbContext>().EnsureIndexesAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var books = app.MapGroup("/api/books");
books.MapGet("/", GetBooksAsync);
books.MapGet("/{id}", GetBookByIdAsync);
books.MapPost("/", CreateBookAsync);
books.MapPut("/{id}", UpdateBookAsync);
books.MapPatch("/{id}/availability", UpdateBookAvailabilityAsync);
books.MapDelete("/{id}", DeleteBookAsync);

var loans = app.MapGroup("/api/loans");
loans.MapGet("/", GetLoansAsync);
loans.MapGet("/active", GetActiveLoansAsync);
loans.MapPost("/", CreateLoanAsync);
loans.MapPut("/{id}", UpdateLoanAsync);
loans.MapPut("/{id}/return", ReturnLoanAsync);

await app.RunAsync();

static async Task<IResult> GetBooksAsync(HttpRequest request, MongoDbContext db, CancellationToken cancellationToken)
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

    return Results.Ok(books.Select(MapBookResponse));
}

static async Task<IResult> GetBookByIdAsync(string id, MongoDbContext db, CancellationToken cancellationToken)
{
    if (!TryParseObjectId(id, out var bookId))
    {
        return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
    }

    var book = await db.Books
        .Find(Builders<BsonDocument>.Filter.Eq("_id", bookId))
        .FirstOrDefaultAsync(cancellationToken);

    if (book is null)
    {
        return Results.NotFound(new ErrorResponse("A könyv nem található."));
    }

    var activeLoan = await GetActiveLoanAsync(db, bookId, cancellationToken);
    return Results.Ok(MapBookDetailResponse(book, activeLoan));
}

static async Task<IResult> CreateBookAsync(BookUpsertRequest? request, MongoDbContext db, CancellationToken cancellationToken)
{
    var validationResult = ValidateBookPayload(request);

    if (validationResult.Errors.Count > 0)
    {
        return Results.BadRequest(new ValidationErrorResponse("Érvénytelen könyvadatok.", validationResult.Errors));
    }

    var book = validationResult.Payload!;
    var bookDocument = new BsonDocument
    {
        ["_id"] = ObjectId.GenerateNewId(),
        ["title"] = book.Title,
        ["author"] = book.Author,
        ["year"] = book.Year,
        ["genre"] = book.Genre,
        ["available"] = book.Available,
    };

    await db.Books.InsertOneAsync(bookDocument, cancellationToken: cancellationToken);
    return Results.Json(MapBookResponse(bookDocument), statusCode: StatusCodes.Status201Created);
}

static async Task<IResult> UpdateBookAsync(string id, BookUpsertRequest? request, MongoDbContext db, CancellationToken cancellationToken)
{
    if (!TryParseObjectId(id, out var bookId))
    {
        return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
    }

    var validationResult = ValidateBookPayload(request);

    if (validationResult.Errors.Count > 0)
    {
        return Results.BadRequest(new ValidationErrorResponse("Érvénytelen könyvadatok.", validationResult.Errors));
    }

    var book = validationResult.Payload!;

    if (book.Available && await HasActiveLoanAsync(db, bookId, cancellationToken))
    {
        return Results.Conflict(new ErrorResponse("Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek."));
    }

    var updatedBook = await db.Books.FindOneAndUpdateAsync(
        Builders<BsonDocument>.Filter.Eq("_id", bookId),
        Builders<BsonDocument>.Update
            .Set("title", book.Title)
            .Set("author", book.Author)
            .Set("year", book.Year)
            .Set("genre", book.Genre)
            .Set("available", book.Available),
        new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After,
        },
        cancellationToken);

    if (updatedBook is null)
    {
        return Results.NotFound(new ErrorResponse("A könyv nem található."));
    }

    return Results.Ok(MapBookResponse(updatedBook));
}

static async Task<IResult> UpdateBookAvailabilityAsync(string id, BookAvailabilityRequest? request, MongoDbContext db, CancellationToken cancellationToken)
{
    if (!TryParseObjectId(id, out var bookId))
    {
        return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
    }

    if (request?.Available is null)
    {
        return Results.BadRequest(new ValidationErrorResponse(
            "Érvénytelen elérhetőségi adat.",
            ["Az available mező kötelező, és logikai értéknek kell lennie."]));
    }

    if (request.Available.Value && await HasActiveLoanAsync(db, bookId, cancellationToken))
    {
        return Results.Conflict(new ErrorResponse("Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek."));
    }

    var updatedBook = await db.Books.FindOneAndUpdateAsync(
        Builders<BsonDocument>.Filter.Eq("_id", bookId),
        Builders<BsonDocument>.Update.Set("available", request.Available.Value),
        new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After,
        },
        cancellationToken);

    if (updatedBook is null)
    {
        return Results.NotFound(new ErrorResponse("A könyv nem található."));
    }

    return Results.Ok(MapBookResponse(updatedBook));
}

static async Task<IResult> DeleteBookAsync(string id, MongoDbContext db, CancellationToken cancellationToken)
{
    if (!TryParseObjectId(id, out var bookId))
    {
        return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
    }

    if (await HasActiveLoanAsync(db, bookId, cancellationToken))
    {
        return Results.Conflict(new ErrorResponse("Aktív kölcsönzés alatt álló könyv nem törölhető."));
    }

    var result = await db.Books.DeleteOneAsync(
        Builders<BsonDocument>.Filter.Eq("_id", bookId),
        cancellationToken);

    return result.DeletedCount == 0
        ? Results.NotFound(new ErrorResponse("A könyv nem található."))
        : Results.NoContent();
}

static async Task<IResult> GetLoansAsync(MongoDbContext db, CancellationToken cancellationToken)
{
    var loans = await db.Loans
        .Find(Builders<BsonDocument>.Filter.Empty)
        .Sort(Builders<BsonDocument>.Sort.Descending("loanedAt"))
        .ToListAsync(cancellationToken);

    return Results.Ok(loans.Select(MapLoanResponse));
}

static async Task<IResult> GetActiveLoansAsync(string? bookId, MongoDbContext db, CancellationToken cancellationToken)
{
    var filter = Builders<BsonDocument>.Filter.Eq("status", "active");

    if (!string.IsNullOrWhiteSpace(bookId))
    {
        if (!TryParseObjectId(bookId, out var parsedBookId))
        {
            return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
        }

        filter = Builders<BsonDocument>.Filter.And(filter, BuildBookIdentifierFilter(parsedBookId));
    }

    var loans = await db.Loans
        .Find(filter)
        .Sort(Builders<BsonDocument>.Sort.Descending("loanedAt"))
        .ToListAsync(cancellationToken);

    return Results.Ok(loans.Select(MapLoanResponse));
}

static async Task<IResult> CreateLoanAsync(LoanCreateRequest? request, MongoDbContext db, CancellationToken cancellationToken)
{
    var validationResult = ValidateLoanCreatePayload(request);

    if (validationResult.Errors.Count > 0)
    {
        return Results.BadRequest(new ValidationErrorResponse("Érvénytelen kölcsönzési adatok.", validationResult.Errors));
    }

    var payload = validationResult.Payload!;

    if (!TryParseObjectId(payload.BookId, out var bookId))
    {
        return Results.BadRequest(new ErrorResponse("Érvénytelen könyvazonosító."));
    }

    var book = await db.Books
        .Find(Builders<BsonDocument>.Filter.Eq("_id", bookId))
        .FirstOrDefaultAsync(cancellationToken);

    if (book is null)
    {
        return Results.NotFound(new ErrorResponse("A könyv nem található."));
    }

    var availabilityResult = await db.Books.UpdateOneAsync(
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", bookId),
            Builders<BsonDocument>.Filter.Eq("available", true)),
        Builders<BsonDocument>.Update.Set("available", false),
        cancellationToken: cancellationToken);

    if (availabilityResult.ModifiedCount == 0)
    {
        return Results.Conflict(new ErrorResponse("A könyv jelenleg nem kölcsönözhető, mert már ki van adva vagy nem elérhető."));
    }

    var loanDocument = CreateLoanDocument(book, bookId, payload);

    try
    {
        await db.Loans.InsertOneAsync(loanDocument, cancellationToken: cancellationToken);
        return Results.Json(MapLoanResponse(loanDocument), statusCode: StatusCodes.Status201Created);
    }
    catch (MongoWriteException error) when (error.WriteError?.Code == 11000)
    {
        await SetBookAvailabilityAsync(db, bookId, true, cancellationToken);
        return Results.Conflict(new ErrorResponse("Ehhez a könyvhöz már tartozik aktív kölcsönzés."));
    }
    catch
    {
        await SetBookAvailabilityAsync(db, bookId, true, cancellationToken);
        throw;
    }
}

static async Task<IResult> UpdateLoanAsync(string id, LoanUpdateRequest? request, MongoDbContext db, CancellationToken cancellationToken)
{
    if (!TryParseObjectId(id, out var loanId))
    {
        return Results.BadRequest(new ErrorResponse("Érvénytelen kölcsönzésazonosító."));
    }

    var validationResult = ValidateLoanUpdatePayload(request);

    if (validationResult.Errors.Count > 0)
    {
        return Results.BadRequest(new ValidationErrorResponse("Érvénytelen kölcsönzési adatok.", validationResult.Errors));
    }

    var existingLoan = await db.Loans
        .Find(Builders<BsonDocument>.Filter.Eq("_id", loanId))
        .FirstOrDefaultAsync(cancellationToken);

    if (existingLoan is null)
    {
        return Results.NotFound(new ErrorResponse("A kölcsönzés nem található."));
    }

    if (!string.Equals(GetString(existingLoan, "status"), "active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new ErrorResponse("Csak aktív kölcsönzés módosítható."));
    }

    var payload = validationResult.Payload!;
    var loanedAt = GetRequiredDateTime(existingLoan, "loanedAt");

    if (payload.DueAt < loanedAt)
    {
        return Results.BadRequest(new ErrorResponse("A határidő nem lehet korábbi, mint a kölcsönzés dátuma."));
    }

    var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", loanId),
            Builders<BsonDocument>.Filter.Eq("status", "active")),
        Builders<BsonDocument>.Update
            .Set("borrowerName", payload.BorrowerName)
            .Set("borrowerEmail", payload.BorrowerEmail)
            .Set("notes", payload.Notes is null ? BsonNull.Value : payload.Notes)
            .Set("dueAt", payload.DueAt)
            .Set("updatedAt", DateTime.UtcNow),
        new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After,
        },
        cancellationToken);

    if (updatedLoan is null)
    {
        return Results.Conflict(new ErrorResponse("A kölcsönzés időközben lezárult."));
    }

    return Results.Ok(MapLoanResponse(updatedLoan));
}

static async Task<IResult> ReturnLoanAsync(string id, LoanReturnRequest? request, MongoDbContext db, CancellationToken cancellationToken)
{
    if (!TryParseObjectId(id, out var loanId))
    {
        return Results.BadRequest(new ErrorResponse("Érvénytelen kölcsönzésazonosító."));
    }

    var validationResult = ValidateLoanReturnPayload(request);

    if (validationResult.Errors.Count > 0)
    {
        return Results.BadRequest(new ValidationErrorResponse("Érvénytelen visszahozási adatok.", validationResult.Errors));
    }

    var loan = await db.Loans
        .Find(Builders<BsonDocument>.Filter.Eq("_id", loanId))
        .FirstOrDefaultAsync(cancellationToken);

    if (loan is null)
    {
        return Results.NotFound(new ErrorResponse("A kölcsönzés nem található."));
    }

    if (!string.Equals(GetString(loan, "status"), "active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new ErrorResponse("A kölcsönzés már le van zárva."));
    }

    var returnPayload = validationResult.Payload!;
    var loanedAt = GetRequiredDateTime(loan, "loanedAt");

    if (returnPayload.ReturnedAt < loanedAt)
    {
        return Results.BadRequest(new ErrorResponse("A visszahozás dátuma nem lehet korábbi, mint a kölcsönzés időpontja."));
    }

    var updatedLoan = await db.Loans.FindOneAndUpdateAsync(
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", loanId),
            Builders<BsonDocument>.Filter.Eq("status", "active")),
        Builders<BsonDocument>.Update
            .Set("returnedAt", returnPayload.ReturnedAt)
            .Set("status", "returned")
            .Set("updatedAt", DateTime.UtcNow),
        new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After,
        },
        cancellationToken);

    if (updatedLoan is null)
    {
        return Results.Conflict(new ErrorResponse("A kölcsönzés időközben lezárult."));
    }

    if (TryGetLoanBookId(loan, out var bookId))
    {
        await SetBookAvailabilityAsync(db, bookId, true, cancellationToken);
    }

    return Results.Ok(MapLoanResponse(updatedLoan));
}

static BookQueryFilterResult BuildBooksFilter(IQueryCollection query)
{
    var filters = new List<FilterDefinition<BsonDocument>>();
    var search = TrimQueryValue(query["search"]);
    var title = TrimQueryValue(query["title"]);
    var author = TrimQueryValue(query["author"]);
    var genre = TrimQueryValue(query["genre"]);

    if (!string.IsNullOrWhiteSpace(search))
    {
        filters.Add(Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Regex("title", CreateContainsRegex(search)),
            Builders<BsonDocument>.Filter.Regex("author", CreateContainsRegex(search)),
            Builders<BsonDocument>.Filter.Regex("genre", CreateContainsRegex(search))));
    }

    if (!string.IsNullOrWhiteSpace(title))
    {
        filters.Add(Builders<BsonDocument>.Filter.Regex("title", CreateContainsRegex(title)));
    }

    if (!string.IsNullOrWhiteSpace(author))
    {
        filters.Add(Builders<BsonDocument>.Filter.Regex("author", CreateContainsRegex(author)));
    }

    if (!string.IsNullOrWhiteSpace(genre))
    {
        filters.Add(Builders<BsonDocument>.Filter.Regex("genre", CreateExactRegex(genre)));
    }

    var available = query["available"].ToString();

    if (!string.IsNullOrWhiteSpace(available))
    {
        if (!bool.TryParse(available, out var availableValue))
        {
            return new BookQueryFilterResult(
                Builders<BsonDocument>.Filter.Empty,
                "Az elérhetőség szűrője csak true vagy false lehet.");
        }

        filters.Add(Builders<BsonDocument>.Filter.Eq("available", availableValue));
    }

    return new BookQueryFilterResult(
        filters.Count == 0 ? Builders<BsonDocument>.Filter.Empty : Builders<BsonDocument>.Filter.And(filters),
        null);
}

static (ValidatedBookPayload? Payload, string[] Errors) ValidateBookPayload(BookUpsertRequest? request)
{
    if (request is null)
    {
        return (null, ["A kérés törzsének JSON objektumnak kell lennie."]);
    }

    var errors = new List<string>();
    var title = NormalizeRequiredString(request.Title);
    var author = NormalizeRequiredString(request.Author);
    var genre = NormalizeRequiredString(request.Genre);

    if (title is null)
    {
        errors.Add("A cím kötelező, és nem lehet üres.");
    }

    if (author is null)
    {
        errors.Add("A szerző megadása kötelező, és nem lehet üres.");
    }

    if (request.Year is null || request.Year < 0)
    {
        errors.Add("A kiadás éve kötelező, és nemnegatív egész számnak kell lennie.");
    }

    if (genre is null)
    {
        errors.Add("A kategória megadása kötelező, és nem lehet üres.");
    }

    if (request.Available is null)
    {
        errors.Add("Az elérhetőség megadása kötelező, és logikai értéknek kell lennie.");
    }

    return errors.Count > 0
        ? (null, errors.ToArray())
        : (new ValidatedBookPayload(title!, author!, request.Year!.Value, genre!, request.Available!.Value), []);
}

static (ValidatedLoanCreatePayload? Payload, string[] Errors) ValidateLoanCreatePayload(LoanCreateRequest? request)
{
    if (request is null)
    {
        return (null, ["A kérés törzsének JSON objektumnak kell lennie."]);
    }

    var errors = new List<string>();
    var bookId = request.BookId?.Trim() ?? string.Empty;
    var borrowerName = NormalizeRequiredString(request.BorrowerName);
    var borrowerEmail = NormalizeRequiredString(request.BorrowerEmail);
    var notes = NormalizeOptionalString(request.Notes);

    if (string.IsNullOrWhiteSpace(bookId))
    {
        errors.Add("A bookId megadása kötelező.");
    }

    if (borrowerName is null)
    {
        errors.Add("A kölcsönző neve kötelező.");
    }

    if (borrowerEmail is null)
    {
        errors.Add("A kölcsönző e-mail címe kötelező.");
    }
    else if (!IsValidEmail(borrowerEmail))
    {
        errors.Add("A kölcsönző e-mail címe érvénytelen.");
    }

    if (string.IsNullOrWhiteSpace(request.DueAt))
    {
        errors.Add("A határidő megadása kötelező.");
    }

    if (!TryParseClientDate(request.DueAt, out var dueAt))
    {
        errors.Add("A visszahozási határidő érvénytelen.");
    }

    var loanedAt = DateTime.UtcNow;

    if (dueAt is not null && dueAt.Value < loanedAt)
    {
        errors.Add("A határidő nem lehet korábbi, mint a kölcsönzés dátuma.");
    }

    return errors.Count > 0
        ? (null, errors.ToArray())
        : (new ValidatedLoanCreatePayload(bookId, borrowerName!, borrowerEmail!, dueAt!.Value, notes), []);
}

static (ValidatedLoanUpdatePayload? Payload, string[] Errors) ValidateLoanUpdatePayload(LoanUpdateRequest? request)
{
    if (request is null)
    {
        return (null, ["A kérés törzsének JSON objektumnak kell lennie."]);
    }

    var errors = new List<string>();
    var borrowerName = NormalizeRequiredString(request.BorrowerName);
    var borrowerEmail = NormalizeRequiredString(request.BorrowerEmail);
    var notes = NormalizeOptionalString(request.Notes);

    if (borrowerName is null)
    {
        errors.Add("A kölcsönző neve kötelező.");
    }

    if (borrowerEmail is null)
    {
        errors.Add("A kölcsönző e-mail címe kötelező.");
    }
    else if (!IsValidEmail(borrowerEmail))
    {
        errors.Add("A kölcsönző e-mail címe érvénytelen.");
    }

    if (string.IsNullOrWhiteSpace(request.DueAt))
    {
        errors.Add("A határidő megadása kötelező.");
    }

    if (!TryParseClientDate(request.DueAt, out var dueAt))
    {
        errors.Add("A visszahozási határidő érvénytelen.");
    }

    return errors.Count > 0
        ? (null, errors.ToArray())
        : (new ValidatedLoanUpdatePayload(borrowerName!, borrowerEmail!, dueAt!.Value, notes), []);
}

static (ValidatedLoanReturnPayload? Payload, string[] Errors) ValidateLoanReturnPayload(LoanReturnRequest? request)
{
    if (request is null || string.IsNullOrWhiteSpace(request.ReturnedAt))
    {
        return (new ValidatedLoanReturnPayload(DateTime.UtcNow), []);
    }

    if (!TryParseClientDate(request.ReturnedAt, out var returnedAt))
    {
        return (null, ["A visszahozás dátuma érvénytelen."]);
    }

    return (new ValidatedLoanReturnPayload(returnedAt!.Value), []);
}

static string? NormalizeRequiredString(string? value)
{
    var normalizedValue = NormalizeOptionalString(value);
    return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
}

static string? NormalizeOptionalString(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return value.Trim();
}

static bool IsValidEmail(string value)
{
    return Regex.IsMatch(
        value,
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
}

static bool TryParseClientDate(string? value, out DateTime? parsedDate)
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

static bool TryParseObjectId(string value, out ObjectId objectId)
{
    return ObjectId.TryParse(value, out objectId);
}

static FilterDefinition<BsonDocument> BuildBookIdentifierFilter(ObjectId bookId)
{
    return Builders<BsonDocument>.Filter.Or(
        Builders<BsonDocument>.Filter.Eq("bookId", bookId),
        Builders<BsonDocument>.Filter.Eq("bookId", bookId.ToString()));
}

static FilterDefinition<BsonDocument> BuildActiveLoanFilter(ObjectId bookId)
{
    return Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("status", "active"),
        BuildBookIdentifierFilter(bookId));
}

static async Task<bool> HasActiveLoanAsync(MongoDbContext db, ObjectId bookId, CancellationToken cancellationToken)
{
    var activeLoan = await db.Loans
        .Find(BuildActiveLoanFilter(bookId))
        .Project(Builders<BsonDocument>.Projection.Include("_id"))
        .FirstOrDefaultAsync(cancellationToken);

    return activeLoan is not null;
}

static Task<BsonDocument?> GetActiveLoanAsync(MongoDbContext db, ObjectId bookId, CancellationToken cancellationToken)
{
    return db.Loans
        .Find(BuildActiveLoanFilter(bookId))
        .Sort(Builders<BsonDocument>.Sort.Descending("loanedAt"))
        .FirstOrDefaultAsync(cancellationToken);
}

static async Task SetBookAvailabilityAsync(MongoDbContext db, ObjectId bookId, bool available, CancellationToken cancellationToken)
{
    await db.Books.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("_id", bookId),
        Builders<BsonDocument>.Update.Set("available", available),
        cancellationToken: cancellationToken);
}

static BsonDocument CreateLoanDocument(BsonDocument book, ObjectId bookId, ValidatedLoanCreatePayload payload)
{
    var now = DateTime.UtcNow;

    return new BsonDocument
    {
        ["_id"] = ObjectId.GenerateNewId(),
        ["bookId"] = bookId,
        ["bookTitle"] = GetString(book, "title"),
        ["bookAuthor"] = GetString(book, "author"),
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

static bool TryGetLoanBookId(BsonDocument loan, out ObjectId bookId)
{
    bookId = ObjectId.Empty;

    if (!loan.TryGetValue("bookId", out var rawBookId) || rawBookId.IsBsonNull)
    {
        return false;
    }

    if (rawBookId.BsonType == BsonType.ObjectId)
    {
        bookId = rawBookId.AsObjectId;
        return true;
    }

    return rawBookId.BsonType == BsonType.String
        && ObjectId.TryParse(rawBookId.AsString, out bookId);
}

static BookResponse MapBookResponse(BsonDocument book)
{
    return new BookResponse(
        GetId(book),
        GetString(book, "title"),
        GetString(book, "author"),
        GetInt32(book, "year"),
        GetString(book, "genre"),
        GetBoolean(book, "available"));
}

static BookDetailResponse MapBookDetailResponse(BsonDocument book, BsonDocument? activeLoan)
{
    return new BookDetailResponse(
        GetId(book),
        GetString(book, "title"),
        GetString(book, "author"),
        GetInt32(book, "year"),
        GetString(book, "genre"),
        GetBoolean(book, "available"),
        activeLoan is not null,
        activeLoan is null ? null : MapLoanResponse(activeLoan));
}

static LoanResponse MapLoanResponse(BsonDocument loan)
{
    return new LoanResponse(
        GetId(loan),
        GetFlexibleId(loan, "bookId"),
        GetString(loan, "bookTitle"),
        GetString(loan, "bookAuthor"),
        GetString(loan, "borrowerName"),
        GetNullableString(loan, "borrowerEmail"),
        GetNullableString(loan, "notes"),
        GetRequiredDateTime(loan, "loanedAt"),
        GetNullableDateTime(loan, "dueAt"),
        GetNullableDateTime(loan, "returnedAt"),
        GetString(loan, "status"),
        GetNullableDateTime(loan, "createdAt"),
        GetNullableDateTime(loan, "updatedAt"));
}

static string GetId(BsonDocument document)
{
    return GetFlexibleId(document, "_id");
}

static string GetFlexibleId(BsonDocument document, string fieldName)
{
    if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
    {
        return string.Empty;
    }

    return value.BsonType switch
    {
        BsonType.ObjectId => value.AsObjectId.ToString(),
        BsonType.String => value.AsString,
        _ => value.ToString(),
    };
}

static string GetString(BsonDocument document, string fieldName)
{
    return document.TryGetValue(fieldName, out var value) && !value.IsBsonNull
        ? value.ToString()
        : string.Empty;
}

static string? GetNullableString(BsonDocument document, string fieldName)
{
    return document.TryGetValue(fieldName, out var value) && !value.IsBsonNull
        ? value.ToString()
        : null;
}

static int GetInt32(BsonDocument document, string fieldName)
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

static bool GetBoolean(BsonDocument document, string fieldName)
{
    return document.TryGetValue(fieldName, out var value) && !value.IsBsonNull && value.ToBoolean();
}

static DateTime GetRequiredDateTime(BsonDocument document, string fieldName)
{
    return GetNullableDateTime(document, fieldName) ?? DateTime.UtcNow;
}

static DateTime? GetNullableDateTime(BsonDocument document, string fieldName)
{
    if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
    {
        return null;
    }

    if (value.BsonType == BsonType.DateTime)
    {
        return value.ToUniversalTime();
    }

    if (value.BsonType == BsonType.String
        && DateTime.TryParse(
            value.AsString,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedDate))
    {
        return parsedDate;
    }

    return null;
}

static string TrimQueryValue(StringValues value)
{
    return value.ToString().Trim();
}

static BsonRegularExpression CreateContainsRegex(string value)
{
    return new BsonRegularExpression(Regex.Escape(value), "i");
}

static BsonRegularExpression CreateExactRegex(string value)
{
    return new BsonRegularExpression($"^{Regex.Escape(value)}$", "i");
}

sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private bool _indexesEnsured;

    public MongoDbContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<BsonDocument> Books => _database.GetCollection<BsonDocument>("books");

    public IMongoCollection<BsonDocument> Loans => _database.GetCollection<BsonDocument>("loans");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_indexesEnsured)
        {
            return;
        }

        // A részleges egyedi index gondoskodik róla, hogy egy könyvhöz egyszerre csak egy aktív kölcsönzés tartozhasson.
        var uniqueActiveLoanIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys
                .Ascending("bookId")
                .Ascending("status"),
            new CreateIndexOptions
            {
                Name = "unique_active_loan_per_book",
                Unique = true,
                PartialFilterExpression = Builders<BsonDocument>.Filter.Eq("status", "active"),
            });

        var loansByStatusAndDateIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys
                .Ascending("status")
                .Descending("loanedAt"),
            new CreateIndexOptions
            {
                Name = "loans_by_status_and_date",
            });

        await Loans.Indexes.CreateManyAsync(
            new[] { uniqueActiveLoanIndex, loansByStatusAndDateIndex },
            cancellationToken);

        _indexesEnsured = true;
    }
}

sealed record BookQueryFilterResult(FilterDefinition<BsonDocument> Filter, string? ErrorMessage);

record ErrorResponse(string Message);

sealed record ValidationErrorResponse(string Message, IReadOnlyList<string> Errors) : ErrorResponse(Message);

sealed record BookUpsertRequest(string? Title, string? Author, int? Year, string? Genre, bool? Available);

sealed record BookAvailabilityRequest(bool? Available);

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

sealed record ValidatedBookPayload(string Title, string Author, int Year, string Genre, bool Available);

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

sealed record BookResponse(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string Author,
    int Year,
    string Genre,
    bool Available);

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
