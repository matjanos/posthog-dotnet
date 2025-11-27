namespace PostHog.AI;

/// <summary>
/// Token usage metadata reported by providers.
/// </summary>
/// <param name="InputTokens">Input / prompt tokens consumed.</param>
/// <param name="OutputTokens">Output / completion tokens produced.</param>
/// <param name="CacheReadInputTokens">Tokens served from cache (provider-specific).</param>
/// <param name="CacheCreationInputTokens">Tokens used to populate cache (provider-specific).</param>
/// <param name="ReasoningTokens">Reasoning tokens, if reported by the provider.</param>
/// <param name="WebSearchCount">Number of web searches executed (if provided).</param>
public sealed record AiTokenUsage(
    int? InputTokens = null,
    int? OutputTokens = null,
    int? CacheReadInputTokens = null,
    int? CacheCreationInputTokens = null,
    int? ReasoningTokens = null,
    int? WebSearchCount = null)
{
    public static AiTokenUsage Empty { get; } = new();

    /// <summary>
    /// Returns a new instance with any null values replaced by zero.
    /// </summary>
    public AiTokenUsage WithDefaults()
        => new(
            InputTokens ?? 0,
            OutputTokens ?? 0,
            CacheReadInputTokens ?? 0,
            CacheCreationInputTokens ?? 0,
            ReasoningTokens ?? 0,
            WebSearchCount ?? 0);
}

/// <summary>
/// Controls how usage is merged when processing streaming events.
/// </summary>
public enum AiUsageAggregationMode
{
    Incremental,
    Cumulative
}

/// <summary>
/// Helper utilities for usage aggregation.
/// </summary>
public static class AiUsageMerger
{
    public static AiTokenUsage Merge(
        AiTokenUsage target,
        AiTokenUsage source,
        AiUsageAggregationMode mode = AiUsageAggregationMode.Incremental)
    {
        #if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        #else
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        #endif

        return mode switch
        {
            AiUsageAggregationMode.Incremental => new AiTokenUsage(
                Sum(target.InputTokens, source.InputTokens),
                Sum(target.OutputTokens, source.OutputTokens),
                Sum(target.CacheReadInputTokens, source.CacheReadInputTokens),
                Sum(target.CacheCreationInputTokens, source.CacheCreationInputTokens),
                Sum(target.ReasoningTokens, source.ReasoningTokens),
                Max(target.WebSearchCount, source.WebSearchCount)),
            AiUsageAggregationMode.Cumulative => new AiTokenUsage(
                source.InputTokens ?? target.InputTokens,
                source.OutputTokens ?? target.OutputTokens,
                source.CacheReadInputTokens ?? target.CacheReadInputTokens,
                source.CacheCreationInputTokens ?? target.CacheCreationInputTokens,
                source.ReasoningTokens ?? target.ReasoningTokens,
                source.WebSearchCount ?? target.WebSearchCount),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported aggregation mode")
        };
    }

    static int? Sum(int? left, int? right)
    {
        if (left is null && right is null)
        {
            return null;
        }

        return (left ?? 0) + (right ?? 0);
    }

    static int? Max(int? left, int? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return Math.Max(left.Value, right.Value);
    }
}
