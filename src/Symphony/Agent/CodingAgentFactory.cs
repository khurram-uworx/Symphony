using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Config;
using Symphony.Services;

namespace Symphony.Agent;

sealed class CodingAgentFactory
{
    public static ICodingAgent Create(ServiceConfig config) =>
        Create(config, logger: (ILogger<CopilotAgent>?)null, telemetryCapture: null);

    public static ICodingAgent Create(ServiceConfig config, ILogger<CopilotAgent>? logger, JobTelemetryCapture? telemetryCapture = null) =>
        Create(config, logger, telemetryCapture, validate: true);

    public static ICodingAgent Create(ServiceConfig config, ILoggerFactory loggerFactory, JobTelemetryCapture? telemetryCapture = null) =>
        Create(config, loggerFactory?.CreateLogger<CopilotAgent>(), telemetryCapture);

    static ICodingAgent Create(ServiceConfig config, ILogger<CopilotAgent>? logger, JobTelemetryCapture? telemetryCapture, bool validate = true)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (validate)
            Validate(config);

        return new CopilotAgent(logger ?? NullLogger<CopilotAgent>.Instance, config, telemetryCapture);
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