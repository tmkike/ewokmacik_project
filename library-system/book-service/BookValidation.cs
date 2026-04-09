using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;

static class BookValidation
{
    public static BookQueryFilterResult BuildBooksFilter(IQueryCollection query)
    {
        var filters = new List<FilterDefinition<BookDocument>>();
        var search = query["search"].ToString().Trim();
        var title = query["title"].ToString().Trim();
        var author = query["author"].ToString().Trim();
        var genre = query["genre"].ToString().Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add(Builders<BookDocument>.Filter.Or(
                Builders<BookDocument>.Filter.Regex(book => book.Title, Contains(search)),
                Builders<BookDocument>.Filter.Regex(book => book.Author, Contains(search)),
                Builders<BookDocument>.Filter.Regex(book => book.Genre, Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            filters.Add(Builders<BookDocument>.Filter.Regex(book => book.Title, Contains(title)));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            filters.Add(Builders<BookDocument>.Filter.Regex(book => book.Author, Contains(author)));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            filters.Add(Builders<BookDocument>.Filter.Regex(book => book.Genre, Exact(genre)));
        }

        var available = query["available"].ToString();

        if (!string.IsNullOrWhiteSpace(available))
        {
            if (!bool.TryParse(available, out var availableValue))
            {
                return new BookQueryFilterResult(
                    Builders<BookDocument>.Filter.Empty,
                    "Az elérhetőség szűrője csak true vagy false lehet.");
            }

            filters.Add(Builders<BookDocument>.Filter.Eq(book => book.Available, availableValue));
        }

        return new BookQueryFilterResult(
            filters.Count == 0 ? Builders<BookDocument>.Filter.Empty : Builders<BookDocument>.Filter.And(filters),
            null);
    }

    public static (ValidatedBookPayload? Payload, string[] Errors) ValidateBookPayload(BookUpsertRequest? request)
    {
        if (request is null)
        {
            return (null, ["A kérés törzsének JSON objektumnak kell lennie."]);
        }

        var errors = new List<string>();
        var title = NormalizeRequiredString(request.Title);
        var author = NormalizeRequiredString(request.Author);
        var genre = NormalizeRequiredString(request.Genre);

        if (title is null) errors.Add("A cím kötelező, és nem lehet üres.");
        if (author is null) errors.Add("A szerző megadása kötelező, és nem lehet üres.");
        if (request.Year is null || request.Year < 0) errors.Add("A kiadás éve kötelező, és nemnegatív egész számnak kell lennie.");
        if (genre is null) errors.Add("A kategória megadása kötelező, és nem lehet üres.");
        if (request.Available is null) errors.Add("Az elérhetőség megadása kötelező, és logikai értéknek kell lennie.");

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

    private static BsonRegularExpression Contains(string value) => new(Regex.Escape(value), "i");

    private static BsonRegularExpression Exact(string value) => new($"^{Regex.Escape(value)}$", "i");
}
