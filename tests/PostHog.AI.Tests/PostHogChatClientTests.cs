using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace PostHog.AI.Tests;

public class PostHogChatClientTests
{
    private readonly Mock<IPostHogClient> _mockPostHogClient;
    private readonly Mock<ILogger<PostHogChatClient>> _mockLogger;

    public PostHogChatClientTests()
    {
        _mockPostHogClient = new Mock<IPostHogClient>();
        _mockLogger = new Mock<ILogger<PostHogChatClient>>();
    }

    [Fact]
    public async Task GetResponseAsyncCapturesEventOnSuccess()
    {
        // Arrange
        var expectedResponse = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Hello!")]
        )
        {
            ModelId = "gpt-4-0613",
            Usage = new UsageDetails
            {
                InputTokenCount = 9,
                OutputTokenCount = 12,
                TotalTokenCount = 21,
            },
        };

        using var innerClient = new TestChatClient { ResponseToReturn = expectedResponse };

        using var client = new PostHogChatClient(
            innerClient,
            _mockPostHogClient.Object,
            _mockLogger.Object
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
        };

        // Act
        var response = await client.GetResponseAsync(
            messages,
            new ChatOptions { ModelId = "gpt-4" }
        );

        // Assert
        Assert.Equal("Hello!", response.Messages[0].Text);

        _mockPostHogClient.Verify(
            x =>
                x.Capture(
                    It.IsAny<string>(),
                    PostHogAIFieldNames.Generation,
                    It.Is<Dictionary<string, object>>(props =>
                        (string)props[PostHogAIFieldNames.Model] == "gpt-4-0613"
                        && (long)props[PostHogAIFieldNames.InputTokens] == 9
                        && (long)props[PostHogAIFieldNames.OutputTokens] == 12
                        && (long)props[PostHogAIFieldNames.TotalTokens] == 21
                        && (string)props[PostHogAIFieldNames.Provider] == "openai"
                        && (string)props[PostHogAIFieldNames.Lib] == "posthog-dotnet"
                    ),
                    null,
                    false,
                    It.IsAny<DateTimeOffset?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetResponseAsyncCapturesErrorEventOnException()
    {
        // Arrange
        using var innerClient = new TestChatClient
        {
            ExceptionToThrow = new InvalidOperationException("Service unavailable"),
        };

        using var client = new PostHogChatClient(
            innerClient,
            _mockPostHogClient.Object,
            _mockLogger.Object
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetResponseAsync(messages, new ChatOptions { ModelId = "gpt-4" })
        );

        _mockPostHogClient.Verify(
            x =>
                x.Capture(
                    It.IsAny<string>(),
                    PostHogAIFieldNames.Generation,
                    It.Is<Dictionary<string, object>>(props =>
                        (bool)props[PostHogAIFieldNames.IsError] == true
                        && (string)props[PostHogAIFieldNames.Error] == "Service unavailable"
                    ),
                    null,
                    false,
                    It.IsAny<DateTimeOffset?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetResponseAsyncUsesPostHogAIContextForDistinctIdAndTraceId()
    {
        // Arrange
        var expectedResponse = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Hello!")]
        )
        {
            ModelId = "gpt-4",
        };

        using var innerClient = new TestChatClient { ResponseToReturn = expectedResponse };

        using var client = new PostHogChatClient(
            innerClient,
            _mockPostHogClient.Object,
            _mockLogger.Object
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
        };

        // Act
        using (
            PostHogAIContext.BeginScope(
                distinctId: "user-123",
                traceId: "trace-abc",
                spanId: "span-xyz"
            )
        )
        {
            await client.GetResponseAsync(messages);
        }

        // Assert
        _mockPostHogClient.Verify(
            x =>
                x.Capture(
                    "user-123",
                    PostHogAIFieldNames.Generation,
                    It.Is<Dictionary<string, object>>(props =>
                        (string)props[PostHogAIFieldNames.TraceId] == "trace-abc"
                        && (string)props[PostHogAIFieldNames.SpanId] == "span-xyz"
                    ),
                    null,
                    false,
                    It.IsAny<DateTimeOffset?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetStreamingResponseAsyncCapturesEventAfterStreamCompletes()
    {
        // Arrange
        var updates = new List<ChatResponseUpdate>
        {
            new()
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Hello")],
                ModelId = "gpt-4",
            },
            new()
            {
                Contents =
                [
                    new TextContent(" world!"),
                    new UsageContent(
                        new UsageDetails
                        {
                            InputTokenCount = 5,
                            OutputTokenCount = 2,
                            TotalTokenCount = 7,
                        }
                    ),
                ],
            },
        };

        using var innerClient = new TestChatClient { StreamingUpdatesToReturn = updates };

        using var client = new PostHogChatClient(
            innerClient,
            _mockPostHogClient.Object,
            _mockLogger.Object
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
        };

        // Act — consume the stream
        var received = new List<ChatResponseUpdate>();
        await foreach (
            var update in client.GetStreamingResponseAsync(
                messages,
                new ChatOptions { ModelId = "gpt-4" }
            )
        )
        {
            received.Add(update);
        }

        // Assert — all updates were yielded through
        Assert.Equal(2, received.Count);

        // Verify capture was called with accumulated data
        _mockPostHogClient.Verify(
            x =>
                x.Capture(
                    It.IsAny<string>(),
                    PostHogAIFieldNames.Generation,
                    It.Is<Dictionary<string, object>>(props =>
                        props.ContainsKey(PostHogAIFieldNames.Latency)
                        && (bool)props[PostHogAIFieldNames.Stream] == true
                    ),
                    null,
                    false,
                    It.IsAny<DateTimeOffset?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ResponseModelTakesPrecedenceOverOptionsModel()
    {
        // Arrange
        var expectedResponse = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Hi")]
        )
        {
            ModelId = "gpt-4-turbo-2024-04-09",
        };

        using var innerClient = new TestChatClient { ResponseToReturn = expectedResponse };

        using var client = new PostHogChatClient(
            innerClient,
            _mockPostHogClient.Object,
            _mockLogger.Object
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
        };

        // Act — request with model "gpt-4-turbo" but response has "gpt-4-turbo-2024-04-09"
        await client.GetResponseAsync(
            messages,
            new ChatOptions { ModelId = "gpt-4-turbo" }
        );

        // Assert — response model is used, not request model
        _mockPostHogClient.Verify(
            x =>
                x.Capture(
                    It.IsAny<string>(),
                    PostHogAIFieldNames.Generation,
                    It.Is<Dictionary<string, object>>(props =>
                        (string)props[PostHogAIFieldNames.Model] == "gpt-4-turbo-2024-04-09"
                    ),
                    null,
                    false,
                    It.IsAny<DateTimeOffset?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task LatencyIsCaptured()
    {
        // Arrange
        var expectedResponse = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Hi")]
        )
        {
            ModelId = "gpt-4",
        };

        using var innerClient = new TestChatClient
        {
            ResponseToReturn = expectedResponse,
            DelayMs = 50,
        };

        using var client = new PostHogChatClient(
            innerClient,
            _mockPostHogClient.Object,
            _mockLogger.Object
        );

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
        };

        // Act
        await client.GetResponseAsync(messages);

        // Assert — latency should be > 0
        _mockPostHogClient.Verify(
            x =>
                x.Capture(
                    It.IsAny<string>(),
                    PostHogAIFieldNames.Generation,
                    It.Is<Dictionary<string, object>>(props =>
                        (double)props[PostHogAIFieldNames.Latency] > 0
                    ),
                    null,
                    false,
                    It.IsAny<DateTimeOffset?>()
                ),
            Times.Once
        );
    }

    /// <summary>
    /// Simple test implementation of IChatClient for unit testing.
    /// </summary>
    private sealed class TestChatClient : IChatClient
    {
        public ChatResponse? ResponseToReturn { get; set; }
        public List<ChatResponseUpdate>? StreamingUpdatesToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public int DelayMs { get; set; }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return ResponseToReturn ?? new ChatResponse([]);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
        )
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            foreach (var update in StreamingUpdatesToReturn ?? [])
            {
                if (DelayMs > 0)
                {
                    await Task.Delay(DelayMs, cancellationToken);
                }

                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
