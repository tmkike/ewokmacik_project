using Domain;

namespace WebApi.Repos;

public interface IBookRepository
{
    Task<IReadOnlyList<Book>> GetBooks(CancellationToken ct = default);
    Task<Book?> GetBookById(string id, CancellationToken ct = default);
    Task<Book> CreateBook(Book book, CancellationToken ct = default);
    Task<Book?> UpdateBook(string id, Book book, CancellationToken ct = default);
    Task<bool> DeleteBook(string id, CancellationToken ct = default);
    Task<bool> SetAvailability(string id, bool available, CancellationToken ct = default);
}
