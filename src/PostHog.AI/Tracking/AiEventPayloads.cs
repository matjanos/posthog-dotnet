namespace PostHog.AI;

/// <summary>
/// Shared payload for a generation event (chat/response).
/// </summary>
/// <param name="Provider">Provider name (openai, anthropic, gemini, etc.).</param>
/// <param name="Model">The model name.</param>
/// <param name="BaseUrl">Base URL used for the provider request.</param>
/// <param name="ModelParameters">Subset of model parameters (temperature, top_p, max_tokens, etc.).</param>
/// <param name="FormattedInput">Provider-formatted input, already sanitized when possible.</param>
/// <param name="FormattedOutput">Provider-formatted output.</param>
/// <param name="Usage">Token usage metadata.</param>
/// <param name="Latency">Elapsed time for the call.</param>
/// <param name="HttpStatus">HTTP status code if available. Defaults to 200.</param>
/// <param name="AvailableTools">Tools/functions passed to the provider.</param>
/// <param name="Instructions">Optional instructions (OpenAI Responses API).</param>
/// <param name="IncludeCacheFieldsWhenZero">Whether to include cache token fields even when zero (Anthropic parity).</param>
public sealed record AiGenerationEvent(
    string Provider,
    string Model,
    Uri BaseUrl,
    IReadOnlyDictionary<string, object?>? ModelParameters,
    object? FormattedInput,
    object? FormattedOutput,
    AiTokenUsage Usage,
    TimeSpan Latency,
    int HttpStatus = 200,
    object? AvailableTools = null,
    object? Instructions = null,
    bool IncludeCacheFieldsWhenZero = false);

/// <summary>
/// Payload for embedding requests.
/// </summary>
/// <param name="Provider">Provider name.</param>
/// <param name="Model">The model used for embeddings.</param>
/// <param name="BaseUrl">Base URL used for the provider request.</param>
/// <param name="Input">Embedding input (sanitized if needed).</param>
/// <param name="Usage">Token usage metadata.</param>
/// <param name="Latency">Elapsed time for the request.</param>
/// <param name="HttpStatus">HTTP status code if available. Defaults to 200.</param>
public sealed record AiEmbeddingEvent(
    string Provider,
    string Model,
    Uri BaseUrl,
    object? Input,
    AiTokenUsage Usage,
    TimeSpan Latency,
    int HttpStatus = 200);
