namespace Migration.Intelligence.Agents.Models;

public sealed class LlmAgentOptions
{
    public string Endpoint { get; init; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; init; } = "gpt-4.1-mini";
    public string ApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 60;
    public double Temperature { get; init; } = 0.1;
}
