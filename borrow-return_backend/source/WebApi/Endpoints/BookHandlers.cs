using Domain;
using Microsoft.AspNetCore.Mvc;
using WebApi.Contracts;
using WebApi.Repos;

namespace WebApi.Endpoints;

public static class BookHandlers
{
    public static RouteGroupBuilder MapBookEndpoints(this RouteGroupBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/books").WithTags("Books");

        group.MapGet("", async ([FromServices] IBookRepository repository, CancellationToken ct) =>
        {
            IReadOnlyList<Book> books = await repository.GetBooks(ct);
            return Results.Ok(books);
        });

        group.MapGet("/{id}", async ([FromRoute] string id, [FromServices] IBookRepository repository, CancellationToken ct) =>
        {
            Book? book = await repository.GetBookById(id, ct);
            return book is null ? Results.NotFound(new MessageResponse("Book not found.")) : Results.Ok(book);
        });

        group.MapPost("", async ([FromBody] CreateBookRequest request, [FromServices] IBookRepository repository, CancellationToken ct) =>
        {
            var book = new Book
            {
                Title = request.Title.Trim(),
                Author = request.Author.Trim(),
                Year = request.Year,
                Genre = request.Genre.Trim(),
                Available = true
            };

            Book created = await repository.CreateBook(book, ct);
            return Results.Created($"/api/books/{created.Id}", created);
        });

        group.MapPut("/{id}", async ([FromRoute] string id, [FromBody] UpdateBookRequest request, [FromServices] IBookRepository repository, CancellationToken ct) =>
        {
            Book? existing = await repository.GetBookById(id, ct);
            if (existing is null)
            {
                return Results.NotFound(new MessageResponse("Book not found."));
            }

            var updatedBook = existing with
            {
                Title = request.Title.Trim(),
                Author = request.Author.Trim(),
                Year = request.Year,
                Genre = request.Genre.Trim()
            };

            Book? result = await repository.UpdateBook(id, updatedBook, ct);
            return result is null ? Results.NotFound(new MessageResponse("Book not found.")) : Results.Ok(result);
        });

        group.MapDelete("/{id}", async ([FromRoute] string id, [FromServices] IBookRepository repository, [FromServices] ILoanRepository loanRepository, CancellationToken ct) =>
        {
            bool hasActiveLoan = await loanRepository.HasActiveLoanForBook(id, ct);
            if (hasActiveLoan)
            {
                return Results.Conflict(new MessageResponse("Book has an active loan and cannot be deleted."));
            }

            bool deleted = await repository.DeleteBook(id, ct);
            return deleted
                ? Results.Ok(new MessageResponse("Book deleted"))
                : Results.NotFound(new MessageResponse("Book not found."));
        });

        return group;
    }
}
