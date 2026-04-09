using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

sealed class BookDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("author")]
    public string Author { get; set; } = string.Empty;

    [BsonElement("year")]
    public int Year { get; set; }

    [BsonElement("genre")]
    public string Genre { get; set; } = string.Empty;

    [BsonElement("available")]
    public bool Available { get; set; }
}
