namespace Symphony.Security;

public static class Redaction
{
    public const string Redacted = "***";

    public static string? Redact(string? value) =>
        string.IsNullOrEmpty(value) ? value : Redacted;

    public static string RedactSecrets(string? value) => Redact(value) ?? string.Empty;
}