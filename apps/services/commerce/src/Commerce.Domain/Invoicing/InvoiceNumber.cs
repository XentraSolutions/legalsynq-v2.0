namespace Commerce.Domain.Invoicing;

/// <summary>
/// Invoice-number formatting helpers. Numbers are issued as
/// <c>COM-INV-000001</c> style strings. Sequence allocation lives in
/// the infrastructure layer; this type only knows how to format and
/// parse the resulting string.
/// </summary>
public static class InvoiceNumber
{
    public const string Prefix = "COM-INV-";
    public const int MinDigits = 6;

    public static string Format(long sequence)
    {
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        return Prefix + sequence.ToString("D" + MinDigits);
    }

    public static bool TryParseSequence(string? value, out long sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var tail = value[Prefix.Length..];
        return long.TryParse(tail, out sequence) && sequence >= 1;
    }
}
