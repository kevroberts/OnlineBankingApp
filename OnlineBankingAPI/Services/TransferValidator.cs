using OnlineBankingAPI.Domain;
using OnlineBankingAPI.Infrastructure;

namespace OnlineBankingAPI.Services;

/// <summary>The destination of a transfer after resolving it against the customer's own data.</summary>
public record ResolvedDestination(string Kind, Guid Id, string Name, string MaskedNumber, Account? Account, Payee? Payee)
{
    public const string AccountKind = "account";
    public const string PayeeKind = "payee";

    public static ResolvedDestination From(Account a) => new(AccountKind, a.Id, a.Name, a.MaskedNumber, a, null);
    public static ResolvedDestination From(Payee p) => new(PayeeKind, p.Id, p.Nickname, p.MaskedAccountNumber, null, p);
}

/// <summary>Business rules for a transfer. Returns every failing rule so a form can mark them all at once.</summary>
public class TransferValidator
{
    public const int MaxNoteLength = 140;

    public IReadOnlyList<FieldError> Validate(Account source, ResolvedDestination? destination, decimal? amount,
        string? note)
    {
        var errors = new List<FieldError>();
        var amountValid = false;

        if (amount is not { } value || value <= 0)
            errors.Add(new("amount", ProblemCodes.AmountNotPositive, "Enter an amount greater than zero."));
        else if (value != Math.Round(value, 2))
            errors.Add(new("amount", ProblemCodes.AmountInvalidPrecision, "Amounts can have at most 2 decimal places."));
        else
            amountValid = true;

        if (destination is null)
            errors.Add(new("destination.id", ProblemCodes.DestinationNotFound, "The destination was not found."));
        else if (destination.Account?.Id == source.Id)
            errors.Add(new("destination.id", ProblemCodes.SameAccount, "Choose a different account to transfer to."));

        if (source.Status != AccountStatuses.Active)
            errors.Add(new("sourceAccountId", ProblemCodes.AccountNotActive, "The source account is not active."));
        if (destination?.Account is { } d && d.Id != source.Id && d.Status != AccountStatuses.Active)
            errors.Add(new("destination.id", ProblemCodes.AccountNotActive, "The destination account is not active."));

        if (amountValid && amount > source.Balance)
            errors.Add(new("amount", ProblemCodes.InsufficientFunds,
                "The amount is more than the available balance."));

        if (!IsValidNote(note))
            errors.Add(new("note", ProblemCodes.NoteInvalid,
                $"Notes can be up to {MaxNoteLength} characters with no control characters."));

        return errors;
    }

    public static bool IsValidNote(string? note) =>
        note is null || (note.Length <= MaxNoteLength && !note.Any(char.IsControl));
}
