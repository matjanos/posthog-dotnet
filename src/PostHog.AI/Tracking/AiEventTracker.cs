using PostHog;

namespace PostHog.AI;

/// <summary>
/// Default implementation that emits AI events through <see cref="IPostHogClient"/>.
/// </summary>
public sealed class AiEventTracker : IAiEventTracker
{
    readonly IPostHogClient _client;

    public AiEventTracker(IPostHogClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public ValueTask<bool> CaptureGenerationAsync(
        AiTrackingContext context,
        AiGenerationEvent generation,
        CancellationToken cancellationToken = default)
    {
        #if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(generation);
        #else
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (generation is null)
        {
            throw new ArgumentNullException(nameof(generation));
        }
        #endif
        cancellationToken.ThrowIfCancellationRequested();

        var buildResult = AiEventPropertiesBuilder.BuildGeneration(context, generation);
        var properties = ToCaptureProperties(buildResult.Properties);

        var captured = _client.Capture(
            buildResult.DistinctId,
            "$ai_generation",
            properties,
            context.Groups,
            sendFeatureFlags: false,
            timestamp: context.Timestamp);

        return new ValueTask<bool>(captured);
    }

    public ValueTask<bool> CaptureEmbeddingAsync(
        AiTrackingContext context,
        AiEmbeddingEvent embedding,
        CancellationToken cancellationToken = default)
    {
        #if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(embedding);
        #else
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (embedding is null)
        {
            throw new ArgumentNullException(nameof(embedding));
        }
        #endif
        cancellationToken.ThrowIfCancellationRequested();

        var buildResult = AiEventPropertiesBuilder.BuildEmbedding(context, embedding);
        var properties = ToCaptureProperties(buildResult.Properties);

        var captured = _client.Capture(
            buildResult.DistinctId,
            "$ai_embedding",
            properties,
            context.Groups,
            sendFeatureFlags: false,
            timestamp: context.Timestamp);

        return new ValueTask<bool>(captured);
    }

    static Dictionary<string, object> ToCaptureProperties(IReadOnlyDictionary<string, object?> source)
    {
        var result = new Dictionary<string, object>(source.Count);
        foreach (var (key, value) in source)
        {
            result[key] = value!;
        }

        return result;
    }
}
