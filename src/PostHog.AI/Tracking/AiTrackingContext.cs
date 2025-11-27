using PostHog;

namespace PostHog.AI;

/// <summary>
/// Context applied to AI tracking events.
/// </summary>
/// <param name="DistinctId">Distinct ID to attribute usage to. If omitted, the tracker will fall back to the trace ID.</param>
/// <param name="TraceId">Optional correlation identifier. If omitted, a new GUID will be generated.</param>
/// <param name="Properties">Additional properties to merge into the event payload.</param>
/// <param name="Groups">Optional groups associated with the event.</param>
/// <param name="PrivacyMode">If true, input/output payloads are redacted from event properties.</param>
/// <param name="Timestamp">Optional timestamp to apply to the capture event.</param>
public sealed record AiTrackingContext(
    string? DistinctId = null,
    string? TraceId = null,
    IReadOnlyDictionary<string, object?>? Properties = null,
    GroupCollection? Groups = null,
    bool PrivacyMode = false,
    DateTimeOffset? Timestamp = null);
