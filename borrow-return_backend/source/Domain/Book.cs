using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain;

public record Book
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; init; }

    public required string Title { get; init; }

    public required string Author { get; init; }

    public required int Year { get; init; }

    public required string Genre { get; init; }

    public bool Available { get; init; } = true;
}
