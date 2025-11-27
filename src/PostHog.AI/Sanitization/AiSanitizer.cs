using System.Text.RegularExpressions;

namespace PostHog.AI;

/// <summary>
/// Helpers to redact base64 payloads before sending to PostHog.
/// </summary>
public static class AiSanitizer
{
    public const string RedactedImagePlaceholder = "[base64 image redacted]";

    static readonly Regex Base64DataUrlRegex = new("^data:([^;]+);base64,", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex RawBase64Regex = new("^[A-Za-z0-9+/]+=*$", RegexOptions.Compiled);

    public static object? SanitizeOpenAi(object? data) => ProcessMessages(data, SanitizeOpenAiImage);

    public static object? SanitizeOpenAiResponse(object? data) => ProcessMessages(data, SanitizeOpenAiResponseImage);

    public static object? SanitizeAnthropic(object? data) => ProcessMessages(data, SanitizeAnthropicImage);

    public static object? SanitizeGemini(object? data)
    {
        if (data is null)
        {
            return null;
        }

        if (data is IEnumerable<object?> list)
        {
            return list.Select(ProcessGeminiItem).ToList();
        }

        return ProcessGeminiItem(data);
    }

    public static object? SanitizeLangChain(object? data) => ProcessMessages(data, SanitizeLangChainImage);

    static object? ProcessMessages(object? messages, Func<object?, object?> transformContent)
    {
        if (messages is null)
        {
            return null;
        }

        object? ProcessContent(object? content)
        {
            switch (content)
            {
                case null:
                    return null;
                case string:
                    return content;
                case IEnumerable<object?> enumerable:
                    return enumerable.Select(transformContent).ToList();
                default:
                    return transformContent(content);
            }
        }

        object? ProcessMessage(object? message)
        {
            if (message is not IDictionary<string, object?> dict || !dict.TryGetValue("content", out var content))
            {
                return message;
            }

            var clone = CloneDictionary(dict);
            clone["content"] = ProcessContent(content);
            return clone;
        }

        if (messages is IEnumerable<object?> list)
        {
            return list.Select(ProcessMessage).ToList();
        }

        return ProcessMessage(messages);
    }

    static object? SanitizeOpenAiImage(object? item)
    {
        if (!TryGetDictionary(item, out var dict))
        {
            return item;
        }

        if (dict.TryGetValue("type", out var type) && type is string typeString && typeString == "image_url")
        {
            if (dict.TryGetValue("image_url", out var imageUrl) && TryGetDictionary(imageUrl, out var imageUrlDict))
            {
                var clone = CloneDictionary(dict);
                var imageUrlClone = CloneDictionary(imageUrlDict);
                if (imageUrlClone.TryGetValue("url", out var url))
                {
                    imageUrlClone["url"] = RedactBase64Data(url);
                }

                clone["image_url"] = imageUrlClone;
                return clone;
            }
        }

        return item;
    }

    static object? SanitizeOpenAiResponseImage(object? item)
    {
        if (!TryGetDictionary(item, out var dict))
        {
            return item;
        }

        if (dict.TryGetValue("type", out var type) && type is string typeString && typeString == "input_image")
        {
            if (dict.TryGetValue("image_url", out var imageUrl))
            {
                var clone = CloneDictionary(dict);
                clone["image_url"] = RedactBase64Data(imageUrl);
                return clone;
            }
        }

        return item;
    }

    static object? SanitizeAnthropicImage(object? item)
    {
        if (!TryGetDictionary(item, out var dict))
        {
            return item;
        }

        if (dict.TryGetValue("type", out var type) && type is string typeString && typeString == "image")
        {
            if (dict.TryGetValue("source", out var source) && TryGetDictionary(source, out var sourceDict))
            {
                if (sourceDict.TryGetValue("type", out var sourceType)
                    && sourceType is string sourceTypeString
                    && sourceTypeString == "base64"
                    && sourceDict.ContainsKey("data"))
                {
                    var clone = CloneDictionary(dict);
                    var sourceClone = CloneDictionary(sourceDict);
                    sourceClone["data"] = RedactedImagePlaceholder;
                    clone["source"] = sourceClone;
                    return clone;
                }
            }
        }

        return item;
    }

    static object? ProcessGeminiItem(object? item)
    {
        if (!TryGetDictionary(item, out var dict))
        {
            return item;
        }

        if (!dict.TryGetValue("parts", out var parts) || parts is null)
        {
            return item;
        }

        var clone = CloneDictionary(dict);

        clone["parts"] = parts switch
        {
            IEnumerable<object?> enumerable => enumerable.Select(SanitizeGeminiPart).ToList(),
            _ => SanitizeGeminiPart(parts)
        };

        return clone;
    }

    static object? SanitizeGeminiPart(object? part)
    {
        if (!TryGetDictionary(part, out var dict))
        {
            return part;
        }

        if (dict.TryGetValue("inline_data", out var inlineData) && TryGetDictionary(inlineData, out var inlineDict))
        {
            if (inlineDict.ContainsKey("data"))
            {
                var clone = CloneDictionary(dict);
                var inlineClone = CloneDictionary(inlineDict);
                inlineClone["data"] = RedactedImagePlaceholder;
                clone["inline_data"] = inlineClone;
                return clone;
            }
        }

        return part;
    }

    static object? SanitizeLangChainImage(object? item)
    {
        if (!TryGetDictionary(item, out var dict))
        {
            return item;
        }

        if (dict.TryGetValue("type", out var type) && type is string typeString)
        {
            switch (typeString)
            {
                case "image_url":
                    if (dict.TryGetValue("image_url", out var imageUrl) && TryGetDictionary(imageUrl, out var imageUrlDict))
                    {
                        var clone = CloneDictionary(dict);
                        var imageUrlClone = CloneDictionary(imageUrlDict);
                        if (imageUrlClone.TryGetValue("url", out var url))
                        {
                            imageUrlClone["url"] = RedactBase64Data(url);
                        }

                        clone["image_url"] = imageUrlClone;
                        return clone;
                    }

                    break;
                case "image" when dict.ContainsKey("data"):
                {
                    var clone = CloneDictionary(dict);
                    clone["data"] = RedactBase64Data(dict["data"]);
                    return clone;
                }
                case "image" when dict.TryGetValue("source", out var source) && TryGetDictionary(source, out var sourceDict) && sourceDict.ContainsKey("data"):
                {
                    var clone = CloneDictionary(dict);
                    var sourceClone = CloneDictionary(sourceDict);
                    sourceClone["data"] = RedactedImagePlaceholder;
                    clone["source"] = sourceClone;
                    return clone;
                }
                case "media" when dict.ContainsKey("data"):
                {
                    var clone = CloneDictionary(dict);
                    clone["data"] = RedactBase64Data(dict["data"]);
                    return clone;
                }
            }
        }

        return item;
    }

    static object? RedactBase64Data(object? value)
    {
        if (value is not string text)
        {
            return value;
        }

        if (IsBase64DataUrl(text) || IsRawBase64(text))
        {
            return RedactedImagePlaceholder;
        }

        return value;
    }

    static bool IsBase64DataUrl(string text) => Base64DataUrlRegex.IsMatch(text);

    static bool IsValidUrl(string text)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return !string.IsNullOrEmpty(uri.Scheme) && !string.IsNullOrEmpty(uri.Host);
        }

        return (text.Length > 0 && text[0] == '/')
               || text.StartsWith("./", StringComparison.Ordinal)
               || text.StartsWith("../", StringComparison.Ordinal);
    }

    static bool IsRawBase64(string text)
    {
        if (IsValidUrl(text))
        {
            return false;
        }

        return text.Length > 20 && RawBase64Regex.IsMatch(text);
    }

    static bool TryGetDictionary(object? value, out Dictionary<string, object?> dict)
    {
        if (value is Dictionary<string, object?> typed)
        {
            dict = typed;
            return true;
        }

        if (value is IDictionary<string, object> nonNullable)
        {
            dict = nonNullable.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
            return true;
        }

        if (value is IDictionary<string, object?> generic)
        {
            dict = new Dictionary<string, object?>(generic);
            return true;
        }

        dict = new Dictionary<string, object?>();
        return false;
    }

    static Dictionary<string, object?> CloneDictionary(IDictionary<string, object?> source)
    {
        var clone = new Dictionary<string, object?>(source.Count);
        foreach (var (key, value) in source)
        {
            clone[key] = value;
        }

        return clone;
    }
}
