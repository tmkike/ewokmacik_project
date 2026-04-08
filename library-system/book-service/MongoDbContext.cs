using MongoDB.Bson;
using MongoDB.Driver;

sealed class MongoDbContext
{
    public MongoDbContext(IMongoDatabase database)
    {
        Database = database;
    }

    private IMongoDatabase Database { get; }

    public IMongoCollection<BsonDocument> Books => Database.GetCollection<BsonDocument>("books");
}
