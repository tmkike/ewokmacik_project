using DataAccess;
using Domain;
using MongoDB.Driver;

namespace WebApi.Repos;

public class BookRepository(IMongoDbConnectionFactory dbFactory) : IBookRepository
{
    private IMongoCollection<Book> Collection => dbFactory.GetDatabase().GetCollection<Book>("books");

    public async Task<IReadOnlyList<Book>> GetBooks(CancellationToken ct = default)
    {
        return await Collection.Find(Builders<Book>.Filter.Empty).ToListAsync(ct);
    }

    public async Task<Book?> GetBookById(string id, CancellationToken ct = default)
    {
        return await Collection.Find(book => book.Id == id).SingleOrDefaultAsync(ct);
    }

    public async Task<Book> CreateBook(Book book, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(book, cancellationToken: ct);
        return book;
    }

    public async Task<Book?> UpdateBook(string id, Book book, CancellationToken ct = default)
    {
        var result = await Collection.FindOneAndReplaceAsync(
            existing => existing.Id == id,
            book,
            new FindOneAndReplaceOptions<Book>
            {
                ReturnDocument = ReturnDocument.After
            },
            ct);

        return result;
    }

    public async Task<bool> DeleteBook(string id, CancellationToken ct = default)
    {
        DeleteResult result = await Collection.DeleteOneAsync(book => book.Id == id, ct);
        return result.DeletedCount > 0;
    }

    public async Task<bool> SetAvailability(string id, bool available, CancellationToken ct = default)
    {
        UpdateResult result = await Collection.UpdateOneAsync(
            book => book.Id == id,
            Builders<Book>.Update.Set(book => book.Available, available),
            cancellationToken: ct);

        return result.ModifiedCount > 0;
    }
}
