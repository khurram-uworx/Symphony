namespace Symphony.Config;

public record CopilotConfig(
    string Provider,
    string? BaseUrl,
    string? ModelId,
    string? ApiKey,
    string? WireApi)
{
    public string ProviderType => Provider;
    public string? Type => Provider;

    public bool IsValid(out string? error)
    {
        var provider = Provider?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            error = null;
            return true;
        }

        var normalized = provider.ToLowerInvariant();
        if (normalized is not ("openai" or "azure" or "anthropic"))
        {
            error = "CopilotProvider must be one of openai, azure, anthropic.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool IsValid(CopilotConfig? config, out string? error)
    {
        if (config is null)
        {
            error = null;
            return true;
        }

        return config.IsValid(out error);
    }

    public static CopilotConfig From(IReadOnlyDictionary<string, object> root)
    {
        var copilot = ConfigReader.ReadMap(root, "copilot");
        var provider = ConfigReader.ReadString(copilot, "provider")
            ?? ConfigReader.ReadString(root, "copilot_provider")
            ?? string.Empty;
        return new CopilotConfig(
            provider,
            ConfigReader.ReadString(copilot, "base_url")
                ?? ConfigReader.ReadString(root, "copilot_base_url")
                ?? ConfigReader.ReadString(copilot, "baseurl"),
            ConfigReader.ReadString(copilot, "model_id")
                ?? ConfigReader.ReadString(copilot, "modelid")
                ?? ConfigReader.ReadString(root, "copilot_model_id"),
            ConfigReader.ReadString(copilot, "api_key")
                ?? ConfigReader.ReadString(copilot, "apikey")
                ?? ConfigReader.ReadString(root, "copilot_api_key"),
            ConfigReader.ReadString(copilot, "wire_api")
                ?? ConfigReader.ReadString(copilot, "wireapi")
                ?? ConfigReader.ReadString(root, "copilot_wire_api"));
    }
}