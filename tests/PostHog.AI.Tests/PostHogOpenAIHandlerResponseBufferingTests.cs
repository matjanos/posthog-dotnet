using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace PostHog.AI.Tests;

/// <summary>
/// Regression tests for the non-streaming response path of <see cref="PostHogOpenAIHandler"/>.
///
/// The handler reads the HTTP response body to capture telemetry. Real responses (via the
/// OpenAI/OpenRouter SDK transport) are delivered with <c>HttpCompletionOption.ResponseHeadersRead</c>
/// and are frequently gzip-compressed, so the body is backed by a <b>one-shot, non-seekable</b>
/// stream that HttpClient's <c>DecompressionHandler</c> lazily wraps in a <see cref="GZipStream"/>.
///
/// If the handler consumes that stream for telemetry without leaving a re-readable buffer, the
/// downstream consumer (the SDK) later reconstructs a <see cref="GZipStream"/> over the now-dead
/// stream and throws <c>ArgumentException: Stream does not support reading. (Parameter 'stream')</c>.
/// These tests pin the handler's contract: it must remain transparent to the downstream consumer.
/// </summary>
public sealed class PostHogOpenAIHandlerResponseBufferingTests
{
    private const string ResponseBody =
        "{\"id\":\"chatcmpl-1\",\"model\":\"gpt-4-0613\",\"choices\":[{\"index\":0,"
        + "\"message\":{\"role\":\"assistant\",\"content\":\"Hi there!\"},\"finish_reason\":\"stop\"}],"
        + "\"usage\":{\"prompt_tokens\":9,\"completion_tokens\":12,\"total_tokens\":21}}";

    [Fact]
    public async Task NonStreamingResponseRemainsReadableByDownstreamConsumer()
    {
        var postHog = Substitute.For<IPostHogClient>();
        var logger = Substitute.For<ILogger<PostHogOpenAIHandler>>();
        using var handler = new PostHogOpenAIHandler(postHog, logger);
        handler.InnerHandler = new MockHttpMessageHandler(
            () => CreateGzipResponse(new OneShotStream(GzipBytes(ResponseBody)))
        );

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com"),
        };

        using var response = await SendAsync(client);

        // The downstream consumer (SDK transport) reads the content after SendAsync returned.
        var downstream = await response.Content.ReadAsStringAsync();
        Assert.Equal(ResponseBody, downstream);

        // Telemetry was still captured from the buffered copy.
        postHog
            .Received(1)
            .Capture(
                Arg.Any<string>(),
                PostHogAIFieldNames.Generation,
                Arg.Is<Dictionary<string, object>>(props =>
                    (int)props[PostHogAIFieldNames.OutputTokens] == 12
                ),
                Arg.Any<GroupCollection?>(),
                false,
                Arg.Any<DateTimeOffset?>()
            );
    }

    [Fact]
    public async Task ResponseBodyReadFailureSurfacesRealErrorInsteadOfCorruptingStream()
    {
        var postHog = Substitute.For<IPostHogClient>();
        var logger = Substitute.For<ILogger<PostHogOpenAIHandler>>();
        using var handler = new PostHogOpenAIHandler(postHog, logger);

        // A response whose body faults partway through being read (e.g. a dropped connection),
        // leaving the underlying stream dead. This is the exact condition that produced the
        // "Stream does not support reading. (Parameter 'stream')" failures in production.
        handler.InnerHandler = new MockHttpMessageHandler(
            () => CreateGzipResponse(new FaultingStream(GzipBytes(ResponseBody)))
        );

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com"),
        };

        // Before the fix: SendAsync succeeds and the failure is deferred, surfacing later as a
        // misleading ArgumentException ("Stream does not support reading") when the SDK reads the
        // consumed stream. After the fix: the handler buffers up front, so the genuine read
        // failure surfaces here (fail-fast) instead of a corrupted response stream masked by a
        // confusing decompression error.
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => SendAsync(client));
        Assert.DoesNotContain(
            "Stream does not support reading",
            FlattenMessages(ex),
            StringComparison.Ordinal
        );
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/v1/chat/completions", UriKind.Relative)
        )
        {
            Content = new StringContent(
                "{\"model\":\"gpt-4\",\"messages\":[{\"role\":\"user\",\"content\":\"Hi\"}]}",
                Encoding.UTF8,
                "application/json"
            ),
        };

        // ResponseHeadersRead mirrors how the OpenAI/OpenRouter SDK transport sends requests, so
        // HttpClient does not transparently buffer the response body on our behalf.
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    private static HttpResponseMessage CreateGzipResponse(Stream source)
    {
        var content = new GZipDecompressedContent(source);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static byte[] GzipBytes(string text)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            gz.Write(bytes, 0, bytes.Length);
        }

        return ms.ToArray();
    }

    private static string FlattenMessages(Exception ex)
    {
        var sb = new StringBuilder();
        for (var current = ex; current != null; current = current.InnerException)
        {
            sb.Append(current.Message).Append(' ');
        }

        return sb.ToString();
    }

    private sealed class MockHttpMessageHandler(Func<HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(responseFactory());
    }

    /// <summary>
    /// Mirrors <c>System.Net.Http.DecompressionHandler</c>'s decompressed content: it lazily builds
    /// a <see cref="GZipStream"/> over a shared one-shot source each time the content is read.
    /// </summary>
    private sealed class GZipDecompressedContent(Stream source) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context
        )
        {
            using var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
            await gzip.CopyToAsync(stream);
        }

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
            new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(
                new GZipStream(source, CompressionMode.Decompress, leaveOpen: true)
            );

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>A read-once, non-seekable stream: after it is fully read it can no longer be read.</summary>
    private sealed class OneShotStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);
        private bool _dead;

        public override bool CanRead => !_dead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            if (read == 0)
            {
                _dead = true;
            }

            return read;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _dead = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>A non-seekable stream that faults partway through the first read, leaving itself dead.</summary>
    private sealed class FaultingStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);
        private bool _dead;
        private int _reads;

        public override bool CanRead => !_dead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _reads++;
            if (_reads >= 2)
            {
                _dead = true;
                throw new IOException("The response ended prematurely.");
            }

            return _inner.Read(buffer, offset, count);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _dead = true;
            base.Dispose(disposing);
        }
    }
}
