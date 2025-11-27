# PostHog.AI (.NET)

AI tracking helpers for the PostHog .NET SDK. This package adds shared types, sanitization, and event tracking utilities for LLM usage reporting to PostHog.

## Installation

```bash
dotnet add package PostHog.AI
```

This package depends on `PostHog`. Install that first or add both packages together.

## Quickstart

Register PostHog and AI tracking via DI:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PostHog;
using PostHog.AI;

var services = new ServiceCollection();
services.AddPostHog(options =>
{
    options.ProjectApiKey = "YOUR_PROJECT_API_KEY";
});
services.AddPostHogAi();

var provider = services.BuildServiceProvider();
var aiTracker = provider.GetRequiredService<IAiEventTracker>();
```

Capture a generation event (e.g., wrapping an LLM call):

```csharp
var tracking = new AiTrackingContext(
    DistinctId: "user-123",
    TraceId: Guid.NewGuid().ToString());

var generation = new AiGenerationEvent(
    Provider: "openai",
    Model: "gpt-4o",
    BaseUrl: new Uri("https://api.openai.com"),
    ModelParameters: new Dictionary<string, object?>
    {
        ["temperature"] = 0.7,
        ["top_p"] = 1
    },
    FormattedInput: new[]
    {
        new AiMessage("user", new AiContentItem[]
        {
            new AiTextContent("Tell me a joke about databases.")
        })
    },
    FormattedOutput: null, // fill with provider-formatted output if you have it
    Usage: AiTokenUsage.Empty,
    Latency: TimeSpan.FromSeconds(1.2),
    HttpStatus: 200);

await aiTracker.CaptureGenerationAsync(tracking, generation);
```

Capture an embedding event:

```csharp
var embedding = new AiEmbeddingEvent(
    Provider: "openai",
    Model: "text-embedding-3-large",
    BaseUrl: new Uri("https://api.openai.com"),
    Input: "Example input",
    Usage: new AiTokenUsage(InputTokens: 128),
    Latency: TimeSpan.FromMilliseconds(350),
    HttpStatus: 200);

await aiTracker.CaptureEmbeddingAsync(tracking, embedding);
```

## Privacy

Set `PrivacyMode` on `AiTrackingContext` to redact `$ai_input`, `$ai_output_choices`, and `$ai_instructions` before sending events. You can also omit `DistinctId` to avoid person profile processing; the tracker will fall back to the `TraceId` and set `$process_person_profile=false`.

## Sanitization

`AiSanitizer` removes inline/base64 image payloads from tracked input/output for OpenAI, Anthropic, Gemini, and LangChain-style shapes. Use the provider-specific helpers (e.g., `AiSanitizer.SanitizeOpenAi(data)`) before emitting events.

## Next steps

- Provider wrappers (OpenAI, Anthropic, Gemini) to automatically capture usage and streaming deltas.
- Converter utilities to format provider requests/responses into the standardized tracking shapes used above.
