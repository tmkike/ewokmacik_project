using System.Globalization;
using System.Text.RegularExpressions;

static class LoanValidation
{
    public static (ValidatedLoanCreatePayload? Payload, string[] Errors) ValidateLoanCreatePayload(LoanCreateRequest? request)
    {
        if (request is null)
        {
            return (null, ["A kérés törzsének JSON objektumnak kell lennie."]);
        }

        var errors = new List<string>();
        var bookId = request.BookId?.Trim() ?? string.Empty;
        var borrowerName = NormalizeRequiredString(request.BorrowerName);
        var borrowerEmail = NormalizeRequiredString(request.BorrowerEmail);
        var notes = NormalizeOptionalString(request.Notes);

        if (string.IsNullOrWhiteSpace(bookId)) errors.Add("A bookId megadása kötelező.");
        if (borrowerName is null) errors.Add("A kölcsönző neve kötelező.");
        if (borrowerEmail is null) errors.Add("A kölcsönző e-mail-címe kötelező.");
        else if (!IsValidEmail(borrowerEmail)) errors.Add("A kölcsönző e-mail-címe érvénytelen.");
        if (string.IsNullOrWhiteSpace(request.DueAt)) errors.Add("A határidő megadása kötelező.");
        if (!TryParseClientDate(request.DueAt, out var dueAt)) errors.Add("A visszahozási határidő érvénytelen.");
        if (dueAt is not null && dueAt.Value < DateTime.UtcNow) errors.Add("A határidő nem lehet korábbi, mint a kölcsönzés dátuma.");

        return errors.Count > 0
            ? (null, errors.ToArray())
            : (new ValidatedLoanCreatePayload(bookId, borrowerName!, borrowerEmail!, dueAt!.Value, notes), []);
    }

    public static (ValidatedLoanUpdatePayload? Payload, string[] Errors) ValidateLoanUpdatePayload(LoanUpdateRequest? request)
    {
        if (request is null)
        {
            return (null, ["A kérés törzsének JSON objektumnak kell lennie."]);
        }

        var errors = new List<string>();
        var borrowerName = NormalizeRequiredString(request.BorrowerName);
        var borrowerEmail = NormalizeRequiredString(request.BorrowerEmail);
        var notes = NormalizeOptionalString(request.Notes);

        if (borrowerName is null) errors.Add("A kölcsönző neve kötelező.");
        if (borrowerEmail is null) errors.Add("A kölcsönző e-mail-címe kötelező.");
        else if (!IsValidEmail(borrowerEmail)) errors.Add("A kölcsönző e-mail-címe érvénytelen.");
        if (string.IsNullOrWhiteSpace(request.DueAt)) errors.Add("A határidő megadása kötelező.");
        if (!TryParseClientDate(request.DueAt, out var dueAt)) errors.Add("A visszahozási határidő érvénytelen.");

        return errors.Count > 0
            ? (null, errors.ToArray())
            : (new ValidatedLoanUpdatePayload(borrowerName!, borrowerEmail!, dueAt!.Value, notes), []);
    }

    public static (ValidatedLoanReturnPayload? Payload, string[] Errors) ValidateLoanReturnPayload(LoanReturnRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReturnedAt))
        {
            return (new ValidatedLoanReturnPayload(DateTime.UtcNow), []);
        }

        return TryParseClientDate(request.ReturnedAt, out var returnedAt)
            ? (new ValidatedLoanReturnPayload(returnedAt!.Value), [])
            : (null, ["A visszahozás dátuma érvénytelen."]);
    }

    private static string? NormalizeRequiredString(string? value)
    {
        var normalized = NormalizeOptionalString(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidEmail(string value)
        => Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static bool TryParseClientDate(string? value, out DateTime? parsedDate)
    {
        parsedDate = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var success = DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date);

        if (!success)
        {
            return false;
        }

        parsedDate = date;
        return true;
    }
}
