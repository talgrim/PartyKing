namespace PartyKing.Application.Configuration;

/// <summary>
/// Any OpenAI-compatible chat completions endpoint. The defaults point at Google's Gemini API (free tier), whose key
/// belongs in <c>LlmSettings:ApiKey</c> (a secret, never in appsettings.json). Without a key the client is unavailable.
/// </summary>
public class LlmConfiguration
{
    public const string SectionName = "LlmSettings";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Asked when <see cref="Model"/> is overloaded (429, 5xx) or too slow, for example a lighter flash-lite model.
    /// Empty means no fallback. It gets no <see cref="ReasoningEffort"/>, since not every model accepts one.
    /// </summary>
    public string? FallbackModel { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>
    /// How much the model thinks before answering (OpenAI's <c>reasoning_effort</c>; Gemini accepts low,
    /// medium and high, not minimal). Thinking makes a reply slow, and statements need none. Empty leaves the model's default.
    /// </summary>
    public string? ReasoningEffort { get; set; } = "low";
}
