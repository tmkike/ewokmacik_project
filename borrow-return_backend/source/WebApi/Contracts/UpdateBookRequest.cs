namespace WebApi.Contracts;

public record UpdateBookRequest(
    string Title,
    string Author,
    int Year,
    string Genre);
