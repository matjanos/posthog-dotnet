namespace PostHog.AI;

/// <summary>
/// Supported content types for AI messages.
/// </summary>
public enum AiContentType
{
    Text,
    Function,
    Image,
    Unknown
}

/// <summary>
/// Base type for AI content items.
/// </summary>
public abstract record AiContentItem(AiContentType Type);

/// <summary>
/// Text content returned by a provider.
/// </summary>
/// <param name="Text">The text content.</param>
public sealed record AiTextContent(string Text) : AiContentItem(AiContentType.Text);

/// <summary>
/// Tool / function call content returned by a provider.
/// </summary>
/// <param name="Id">Provider-assigned identifier for the tool call, if any.</param>
/// <param name="Name">Tool name.</param>
/// <param name="Arguments">Arguments payload as provided by the model (usually JSON string or object).</param>
public sealed record AiFunctionCall(string? Id, string Name, object? Arguments) : AiContentItem(AiContentType.Function);

/// <summary>
/// Image content reference returned by a provider.
/// </summary>
/// <param name="Image">Image payload or URL.</param>
public sealed record AiImageContent(string Image) : AiContentItem(AiContentType.Image);

/// <summary>
/// Standardized message format for PostHog AI tracking.
/// </summary>
/// <param name="Role">The speaker role (user, assistant, system, etc.).</param>
/// <param name="Content">The content items for this message.</param>
public sealed record AiMessage(string Role, IReadOnlyList<AiContentItem> Content);
