using System.Text.RegularExpressions;

static class LoanValidation
{
    public static (ValidatedLoanCreatePayload? Payload, string[] Errors) ValidateLoanCreatePayload(LoanCreateRequest? request)
    {
        if (!TryValidateRequestBody(request, out var errors))
        {
            return (null, errors);
        }

        var bookId = request!.BookId?.Trim() ?? string.Empty;
        var borrower = ValidateBorrower(request.BorrowerName, request.BorrowerEmail, request.Notes);
        var dueDate = ValidateRequiredDate(
            request.DueAt,
            missingMessage: "A határidő megadása kötelező.",
            invalidMessage: "A visszahozási határidő érvénytelen. A formátum legyen YYYY-MM-DD.",
            borrower.Errors);

        if (string.IsNullOrWhiteSpace(bookId))
        {
            borrower.Errors.Add("A bookId megadása kötelező.");
        }

        if (dueDate is not null && dueDate.Value < LoanDateRules.GetCurrentUtcDate())
        {
            borrower.Errors.Add("A határidő nem lehet korábbi, mint a mai nap.");
        }

        if (dueDate is null || borrower.Errors.Count > 0)
        {
            return (null, borrower.Errors.ToArray());
        }

        return (new ValidatedLoanCreatePayload(bookId, borrower.Name!, borrower.Email!, dueDate.Value, borrower.Notes), []);
    }

    public static (ValidatedLoanUpdatePayload? Payload, string[] Errors) ValidateLoanUpdatePayload(LoanUpdateRequest? request)
    {
        if (!TryValidateRequestBody(request, out var errors))
        {
            return (null, errors);
        }

        var borrower = ValidateBorrower(request!.BorrowerName, request.BorrowerEmail, request.Notes);
        var dueDate = ValidateRequiredDate(
            request.DueAt,
            missingMessage: "A határidő megadása kötelező.",
            invalidMessage: "A visszahozási határidő érvénytelen. A formátum legyen YYYY-MM-DD.",
            borrower.Errors);

        if (dueDate is null || borrower.Errors.Count > 0)
        {
            return (null, borrower.Errors.ToArray());
        }

        return (new ValidatedLoanUpdatePayload(borrower.Name!, borrower.Email!, dueDate.Value, borrower.Notes), []);
    }

    public static (ValidatedLoanReturnPayload? Payload, string[] Errors) ValidateLoanReturnPayload(LoanReturnRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReturnedAt))
        {
            return (new ValidatedLoanReturnPayload(LoanDateRules.GetCurrentUtcDate()), []);
        }

        return LoanDateRules.TryParseClientDate(request.ReturnedAt, out var returnedDate)
            ? (new ValidatedLoanReturnPayload(returnedDate), [])
            : (null, ["A visszahozás dátuma érvénytelen. A formátum legyen YYYY-MM-DD."]);
    }

    private static bool TryValidateRequestBody<TRequest>(TRequest? request, out string[] errors)
        where TRequest : class
    {
        if (request is not null)
        {
            errors = [];
            return true;
        }

        errors = ["A kérés törzsének JSON objektumnak kell lennie."];
        return false;
    }

    private static (string? Name, string? Email, string? Notes, List<string> Errors) ValidateBorrower(
        string? borrowerName,
        string? borrowerEmail,
        string? notes)
    {
        var errors = new List<string>();
        var normalizedName = NormalizeRequiredString(borrowerName);
        var normalizedEmail = NormalizeRequiredString(borrowerEmail);
        var normalizedNotes = NormalizeOptionalString(notes);

        if (normalizedName is null)
        {
            errors.Add("A kölcsönző neve kötelező.");
        }

        if (normalizedEmail is null)
        {
            errors.Add("A kölcsönző e-mail-címe kötelező.");
        }
        else if (!IsValidEmail(normalizedEmail))
        {
            errors.Add("A kölcsönző e-mail-címe érvénytelen.");
        }

        return (normalizedName, normalizedEmail, normalizedNotes, errors);
    }

    private static DateOnly? ValidateRequiredDate(
        string? value,
        string missingMessage,
        string invalidMessage,
        List<string>? errors = null)
    {
        errors ??= [];

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(missingMessage);
            return null;
        }

        if (!LoanDateRules.TryParseClientDate(value, out var parsedDate))
        {
            errors.Add(invalidMessage);
            return null;
        }

        return parsedDate;
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
}
