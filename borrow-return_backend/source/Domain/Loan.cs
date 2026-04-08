using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain;

public record Loan
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; init; }

    [BsonRepresentation(BsonType.ObjectId)]
    public required string BookId { get; init; }

    public required string UserName { get; init; }

    public required DateOnly BorrowDate { get; init; }

    public bool Returned { get; init; }

    public DateOnly? ReturnDate { get; init; }
}
