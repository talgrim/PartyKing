namespace PartyKing.Application.Ports;

/// <summary>The LLM provider refused a request; <see cref="Status"/> is the HTTP status it answered with.</summary>
public class LlmProviderException : InvalidOperationException
{
    public LlmProviderException(int status, string message, Exception inner)
        : base(message, inner)
    {
        Status = status;
    }

    public int Status { get; }

    /// <summary>Overload (429) and server errors say nothing about the request itself, so another model may still answer.</summary>
    public bool IsTransient => Status == 429 || Status >= 500;
}
