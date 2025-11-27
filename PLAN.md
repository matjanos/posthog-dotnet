## PostHog .NET AI Port – Working Plan

### Objectives
- Port AI tracking from `posthog-python/posthog/ai` to .NET with API parity and .NET-idiomatic surface.
- Keep AI support optional (separate `PostHog.AI` package) and align with existing PostHog client patterns (DI-friendly, async-first).
- Ensure event payload parity (`$ai_generation`, `$ai_embedding`, tools, tokens, privacy redaction) and streaming coverage.

### Milestones
1) Shared layer (DTOs, sanitization, tracking helpers, DI wiring) **[In Progress - scaffolding merged]**  
2) OpenAI wrapper (sync/async, streaming, embeddings, instructions/tools, usage/web-search) **[Todo]**  
3) Anthropic wrapper (streaming content blocks, tools, cache token fields) **[Todo]**  
4) Gemini wrapper (parts/inline_data, usage metadata, streaming) **[Todo]**  
5) Tests (converters, sanitization, tracker with fake `IPostHogClient`, streaming accumulation) **[Todo]**  
6) Documentation & samples (package README, usage snippets, DI examples) **[Todo]**

### Design guardrails
- Composition over inheritance: wrap official provider clients, expose minimal shims that mirror provider SDK method names while accepting optional `AiTrackingContext`.
- Async-first APIs, `CancellationToken` everywhere; streaming surfaced as `IAsyncEnumerable`.
- Privacy-first: `PrivacyMode` or client privacy flag suppresses `$ai_input`/`$ai_output_choices`/`$ai_instructions`.
- No hard provider deps for consumers who don’t need them; keep AI package modular.
- Strongly typed DTOs for formatted messages/content/tool calls; usage merging supports incremental/cumulative reporting like python.

### Open questions to validate during implementation
- Provider package selection and minimum versions (OpenAI/Anthropic/Gemini) that support streaming hooks we need.
- How to expose base URL overrides and instructions merge for OpenAI Responses API in a .NET-idiomatic way.
- Default trace ID generation and surface for callers who want to correlate across systems.

### Next actions
- Scaffold `PostHog.AI` project with shared DTOs, sanitizer, tracking helper, DI extension.
- Add OpenAI wrapper first to prove event shapes and streaming flow, then port Anthropic/Gemini.
