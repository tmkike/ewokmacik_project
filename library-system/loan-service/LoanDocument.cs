using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

sealed class LoanDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("bookId")]
    public ObjectId BookId { get; set; }

    [BsonElement("bookTitle")]
    public string BookTitle { get; set; } = string.Empty;

    [BsonElement("bookAuthor")]
    public string BookAuthor { get; set; } = string.Empty;

    [BsonElement("borrowerName")]
    public string BorrowerName { get; set; } = string.Empty;

    [BsonElement("borrowerEmail")]
    [BsonIgnoreIfNull]
    public string? BorrowerEmail { get; set; }

    [BsonElement("notes")]
    [BsonIgnoreIfNull]
    public string? Notes { get; set; }

    [BsonElement("loanedAt")]
    public DateTime LoanedAt { get; set; }

    [BsonElement("dueAt")]
    [BsonIgnoreIfNull]
    public DateTime? DueAt { get; set; }

    [BsonElement("returnedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ReturnedAt { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
