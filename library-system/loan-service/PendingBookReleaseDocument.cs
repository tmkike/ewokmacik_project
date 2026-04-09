using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

sealed class PendingBookReleaseDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("bookId")]
    public string BookId { get; set; } = string.Empty;

    [BsonElement("loanId")]
    [BsonIgnoreIfNull]
    public string? LoanId { get; set; }

    [BsonElement("attemptCount")]
    public int AttemptCount { get; set; }

    [BsonElement("lastAttemptAt")]
    public DateTime LastAttemptAt { get; set; }

    [BsonElement("lastError")]
    [BsonIgnoreIfNull]
    public string? LastError { get; set; }

    [BsonElement("nextAttemptAt")]
    public DateTime NextAttemptAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
