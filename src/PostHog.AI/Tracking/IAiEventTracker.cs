namespace PostHog.AI;

/// <summary>
/// Emits AI usage events to PostHog.
/// </summary>
public interface IAiEventTracker
{
    /// <summary>
    /// Capture a generation event (chat/completions/responses) with token usage and metadata.
    /// </summary>
    ValueTask<bool> CaptureGenerationAsync(
        AiTrackingContext context,
        AiGenerationEvent generation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture an embedding event with token usage and metadata.
    /// </summary>
    ValueTask<bool> CaptureEmbeddingAsync(
        AiTrackingContext context,
        AiEmbeddingEvent embedding,
        CancellationToken cancellationToken = default);
}
