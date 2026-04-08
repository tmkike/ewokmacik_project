using MongoDB.Bson;
using MongoDB.Driver;

sealed class MongoDbContext
{
    public MongoDbContext(IMongoDatabase database)
    {
        Database = database;
    }

    private IMongoDatabase Database { get; }
    private bool _indexesEnsured;

    public IMongoCollection<BsonDocument> Loans => Database.GetCollection<BsonDocument>("loans");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_indexesEnsured)
        {
            return;
        }

        var uniqueActiveLoanIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("bookId").Ascending("status"),
            new CreateIndexOptions<BsonDocument>
            {
                Name = "unique_active_loan_per_book",
                Unique = true,
                PartialFilterExpression = Builders<BsonDocument>.Filter.Eq("status", "active"),
            });

        var loansByStatusAndDateIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("status").Descending("loanedAt"),
            new CreateIndexOptions<BsonDocument> { Name = "loans_by_status_and_date" });

        await Loans.Indexes.CreateManyAsync([uniqueActiveLoanIndex, loansByStatusAndDateIndex], cancellationToken);
        _indexesEnsured = true;
    }
}
