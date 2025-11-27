namespace PostHog.AI;

internal static class AiEventPropertiesBuilder
{
    public static AiEventBuildResult BuildGeneration(
        AiTrackingContext context,
        AiGenerationEvent data)
    {
        var traceId = EnsureTraceId(context.TraceId);
        var distinctId = EnsureDistinctId(context.DistinctId, traceId);

        var properties = new Dictionary<string, object?>
        {
            ["$ai_provider"] = data.Provider,
            ["$ai_model"] = data.Model,
            ["$ai_model_parameters"] = data.ModelParameters ?? new Dictionary<string, object?>(),
            ["$ai_input"] = WithPrivacy(context.PrivacyMode, data.FormattedInput),
            ["$ai_output_choices"] = WithPrivacy(context.PrivacyMode, data.FormattedOutput),
            ["$ai_http_status"] = data.HttpStatus,
            ["$ai_input_tokens"] = data.Usage.InputTokens ?? 0,
            ["$ai_output_tokens"] = data.Usage.OutputTokens ?? 0,
            ["$ai_latency"] = data.Latency.TotalSeconds,
            ["$ai_trace_id"] = traceId,
            ["$ai_base_url"] = data.BaseUrl.ToString()
        };

        if (data.AvailableTools is not null)
        {
            properties["$ai_tools"] = data.AvailableTools;
        }

        if (data.Instructions is not null)
        {
            properties["$ai_instructions"] = WithPrivacy(context.PrivacyMode, data.Instructions);
        }

        AddToken(properties, "$ai_cache_read_input_tokens", data.Usage.CacheReadInputTokens, data.IncludeCacheFieldsWhenZero);
        AddToken(properties, "$ai_cache_creation_input_tokens", data.Usage.CacheCreationInputTokens, data.IncludeCacheFieldsWhenZero);
        AddToken(properties, "$ai_reasoning_tokens", data.Usage.ReasoningTokens, includeWhenZero: false);

        if (data.Usage.WebSearchCount is not null && data.Usage.WebSearchCount > 0)
        {
            properties["$ai_web_search_count"] = data.Usage.WebSearchCount.Value;
        }

        Merge(properties, context.Properties);

        if (context.DistinctId is null)
        {
            properties["$process_person_profile"] = false;
        }

        return new AiEventBuildResult(properties, traceId, distinctId);
    }

    public static AiEventBuildResult BuildEmbedding(
        AiTrackingContext context,
        AiEmbeddingEvent data)
    {
        var traceId = EnsureTraceId(context.TraceId);
        var distinctId = EnsureDistinctId(context.DistinctId, traceId);

        var properties = new Dictionary<string, object?>
        {
            ["$ai_provider"] = data.Provider,
            ["$ai_model"] = data.Model,
            ["$ai_input"] = WithPrivacy(context.PrivacyMode, data.Input),
            ["$ai_http_status"] = data.HttpStatus,
            ["$ai_input_tokens"] = data.Usage.InputTokens ?? 0,
            ["$ai_latency"] = data.Latency.TotalSeconds,
            ["$ai_trace_id"] = traceId,
            ["$ai_base_url"] = data.BaseUrl.ToString()
        };

        Merge(properties, context.Properties);

        if (context.DistinctId is null)
        {
            properties["$process_person_profile"] = false;
        }

        return new AiEventBuildResult(properties, traceId, distinctId);
    }

    static void AddToken(
        Dictionary<string, object?> properties,
        string propertyName,
        int? value,
        bool includeWhenZero)
    {
        if (value is null)
        {
            return;
        }

        if (value == 0 && !includeWhenZero)
        {
            return;
        }

        properties[propertyName] = value;
    }

    static string EnsureTraceId(string? traceId)
        => string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString() : traceId!;

    static string EnsureDistinctId(string? distinctId, string fallback)
        => string.IsNullOrWhiteSpace(distinctId) ? fallback : distinctId!;

    static object? WithPrivacy(bool privacyMode, object? value) => privacyMode ? null : value;

    static void Merge(
        Dictionary<string, object?> properties,
        IReadOnlyDictionary<string, object?>? additional)
    {
        if (additional is null)
        {
            return;
        }

        foreach (var (key, value) in additional)
        {
            properties[key] = value;
        }
    }
}

internal sealed record AiEventBuildResult(
    IReadOnlyDictionary<string, object?> Properties,
    string TraceId,
    string DistinctId);
