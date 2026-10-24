using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Config;

namespace Symphony.Agent;

sealed class CodingAgentFactory
{
    public static ICodingAgent Create(ServiceConfig config) =>
        Create(config, logger: (ILogger<CopilotAgent>?)null);

    public static ICodingAgent Create(ServiceConfig config, ILogger<CopilotAgent>? logger) =>
        Create(config, logger, validate: true);

    public static ICodingAgent Create(ServiceConfig config, ILoggerFactory loggerFactory) =>
        Create(config, loggerFactory?.CreateLogger<CopilotAgent>());

    static ICodingAgent Create(ServiceConfig config, ILogger<CopilotAgent>? logger, bool validate = true)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (validate)
            Validate(config);

        return new CopilotAgent(logger ?? NullLogger<CopilotAgent>.Instance, config);
    }

    static void Validate(ServiceConfig config)
    {
        var copilot = config.Copilot;
        if (copilot is null)
            return;

        if (!copilot.IsValid(out var error))
            throw new ArgumentException(error, nameof(config));
    }
}