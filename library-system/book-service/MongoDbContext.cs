using MongoDB.Driver;

sealed class MongoDbContext
{
    public MongoDbContext(IMongoDatabase database)
    {
        Database = database;
    }

    private IMongoDatabase Database { get; }
    private bool _indexesEnsured;

    public IMongoCollection<BookDocument> Books => Database.GetCollection<BookDocument>("books");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_indexesEnsured)
        {
            return;
        }

        var titleIndex = new CreateIndexModel<BookDocument>(
            Builders<BookDocument>.IndexKeys.Ascending(book => book.Title),
            new CreateIndexOptions<BookDocument> { Name = "books_by_title" });

        var authorIndex = new CreateIndexModel<BookDocument>(
            Builders<BookDocument>.IndexKeys.Ascending(book => book.Author),
            new CreateIndexOptions<BookDocument> { Name = "books_by_author" });

        var genreIndex = new CreateIndexModel<BookDocument>(
            Builders<BookDocument>.IndexKeys.Ascending(book => book.Genre),
            new CreateIndexOptions<BookDocument> { Name = "books_by_genre" });

        await Books.Indexes.CreateManyAsync([titleIndex, authorIndex, genreIndex], cancellationToken);
        _indexesEnsured = true;
    }
}
