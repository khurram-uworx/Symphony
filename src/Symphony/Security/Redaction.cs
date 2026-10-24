using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Symphony.Security;

public static class Redaction
{
    public const string Redacted = "***";

    public static string RedactedValue => Redacted;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string? Redact(string? value) =>
        string.IsNullOrEmpty(value) ? value : Redacted;

    public static string RedactSecrets(string? value) => Redact(value) ?? string.Empty;

    public static string RedactSecrets(string? value, IEnumerable<string?> secrets)
    {
        var result = value ?? string.Empty;
        if (result.Length == 0)
            return result;

        foreach (var secret in secrets)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 4)
                continue;

            result = result.Replace(secret, Redacted, StringComparison.Ordinal);
        }

        return result;
    }

    public static bool IsSecretName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalized = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (normalized.Length == 0)
            return false;

        return normalized.Contains("password")
            || normalized.Contains("secret")
            || normalized.Contains("token")
            || normalized.Contains("apikey")
            || normalized.Contains("accesstoken")
            || normalized.Contains("personalaccesstoken")
            || normalized.Contains("credential")
            || normalized.Contains("authorization")
            || normalized.Contains("cookie")
            || normalized.Contains("privatekey")
            || normalized.Contains("clientsecret")
            || normalized.Equals("pat")
            || normalized.EndsWith("pat");
    }

    public static T? RedactSecrets<T>(T? value)
    {
        if (value is null)
            return default;

        if (value is string stringValue)
            return (T)(object)(Redact(stringValue) ?? string.Empty);

        if (value is IDictionary dictionary)
        {
            var target = CreateDictionary(dictionary);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (key is not null && IsSecretName(key))
                    continue; // remove secret-like fields from dictionary payloads

                target[entry.Key!] = SanitizeNonSecretValue(entry.Value);
            }

            return target is T typedResult ? typedResult : (T)(object)target;
        }

        try
        {
            MutateSecrets(value);
        }
        catch
        {
            // best effort
        }

        string json;
        try
        {
            json = JsonSerializer.Serialize(value, value.GetType());
        }
        catch
        {
            return value;
        }

        try
        {
            var node = JsonNode.Parse(json);
            SanitizeNode(node, propertyName: null);
            var sanitizedJson = node?.ToJsonString() ?? json;
            var result = JsonSerializer.Deserialize(sanitizedJson, value.GetType());
            if (result is T typedResult)
                return typedResult;
        }
        catch
        {
            // fall through
        }

        return value;
    }

    public static T? Redact<T>(T? value) => RedactSecrets(value);

    public static T? RedactObject<T>(T? value) => RedactSecrets(value);

    public static object? RedactObject(object? value) => value is null ? null : RedactSecrets(value);

    public static T? RedactPayload<T>(T? value) => RedactSecrets(value);

    public static T? RedactEventPayload<T>(T? value) => RedactSecrets(value);

    public static T? RemoveSecrets<T>(T? value) => RedactSecrets(value);

    public static T? StripSecrets<T>(T? value) => RedactSecrets(value);

    static object? SanitizeNonSecretValue(object? value)
    {
        if (value is null)
            return null;

        if (value is string || value.GetType().IsPrimitive || value is Enum || value is DateTime || value is DateTimeOffset || value is Guid || value is decimal)
            return value;

        if (value is IDictionary dictionary)
            return RedactSecrets(dictionary);

        try
        {
            var json = JsonSerializer.Serialize(value, value.GetType());
            var node = JsonNode.Parse(json);
            SanitizeNode(node, propertyName: null);
            var sanitizedJson = node?.ToJsonString() ?? json;
            return JsonSerializer.Deserialize(sanitizedJson, value.GetType());
        }
        catch
        {
            return value;
        }
    }

    static IDictionary CreateDictionary(IDictionary source)
    {
        try
        {
            return (IDictionary)Activator.CreateInstance(source.GetType())!;
        }
        catch
        {
            return new Dictionary<object, object?>();
        }
    }

    static void MutateSecrets(object value)
    {
        foreach (var property in value.GetType().GetProperties())
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            var current = property.GetValue(value);
            if (IsSecretName(property.Name))
            {
                if (property.PropertyType == typeof(string))
                    property.SetValue(value, Redacted);
                else if (property.PropertyType.IsClass && current is not null)
                    property.SetValue(value, null);
            }
            else if (current is not null && current is not string && current.GetType().IsClass)
            {
                MutateSecrets(current);
            }
        }
    }

    static void SanitizeNode(JsonNode? node, string? propertyName)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.ToArray().Select(pair => pair.Key).ToList())
            {
                var child = obj[key];
                if (IsSecretName(key))
                {
                    obj[key] = Redacted;
                }
                else
                {
                    SanitizeNode(child, key);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                SanitizeNode(item, propertyName);
        }
    }
}