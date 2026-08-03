namespace NytUnlock;

/// <summary>Configuration or usage failure. Carries the process exit code.</summary>
internal sealed class ConfigError(string message, int exitCode = 2) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}
