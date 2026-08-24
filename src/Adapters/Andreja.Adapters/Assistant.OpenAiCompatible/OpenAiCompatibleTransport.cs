using Andreja.Platform.Contracts.Assistant;
using Andreja.Platform.Contracts.Skills;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Andreja.Adapters.Assistant.OpenAiCompatible;

public sealed record OpenAiCompatibleTransportPolicy(
    IReadOnlyList<Uri> AllowedEndpoints,
    int MaximumResponseBodyBytes,
    int MaximumRetries,
    TimeSpan RetryBaseDelay,
    long ApprovedExternalTotalUnits);

public sealed class OpenAiCompatibleTransport : IOpenAiCompatibleTransport
{
    private const string ProviderName = "openai-compatible";
    private readonly HttpClient client;
    private readonly IAssistantCredentialStore credentialStore;
    private readonly OpenAiCompatibleTransportPolicy policy;
    private readonly HashSet<string> allowedEndpoints;
    private readonly AssistantUsageBudget usageBudget;

    public OpenAiCompatibleTransport(
        HttpClient client,
        IAssistantCredentialStore credentialStore,
        OpenAiCompatibleTransportPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.AllowedEndpoints.Count == 0
            || policy.MaximumResponseBodyBytes is < 1024 or > 16 * 1024 * 1024
            || policy.MaximumRetries is < 0 or > 5
            || policy.RetryBaseDelay < TimeSpan.Zero
            || policy.RetryBaseDelay > TimeSpan.FromSeconds(5)
            || policy.ApprovedExternalTotalUnits < 0)
        {
            throw new ArgumentException("The OpenAI-compatible transport policy is invalid.", nameof(policy));
        }

        this.client = client;
        this.credentialStore = credentialStore;
        this.policy = policy;
        allowedEndpoints = policy.AllowedEndpoints
            .Select(ValidateAndCanonicalizeEndpoint)
            .ToHashSet(StringComparer.Ordinal);
        if (allowedEndpoints.Count != policy.AllowedEndpoints.Count)
        {
            throw new ArgumentException("The endpoint allowlist contains a duplicate.", nameof(policy));
        }

        usageBudget = new(policy.ApprovedExternalTotalUnits);
    }

    public async ValueTask<AssistantResponse> CompleteAsync(
        AssistantProviderProfile profile,
        AssistantSessionRequest session,
        AssistantRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        if (!allowedEndpoints.Contains(ValidateAndCanonicalizeEndpoint(profile.Endpoint)))
        {
            return Failure(
                request,
                profile,
                "endpoint-not-allowed",
                "The assistant endpoint is not on the operator allowlist.",
                false,
                stopwatch.Elapsed);
        }

        var tools = ResolveTools(session, request);
        var providerToolNames = CreateProviderToolNames(tools);
        var requestBody = WriteRequest(profile, request, tools, providerToolNames);
        try
        {
            var estimatedInputUnits = Math.Max(1, (requestBody.LongLength + 3) / 4);
            if (profile.MaximumInputUnits is { } maximumInput
                && estimatedInputUnits > maximumInput)
            {
                return Failure(
                    request,
                    profile,
                    "provider-input-limit-exceeded",
                    "The assistant request exceeds the configured input limit.",
                    false,
                    stopwatch.Elapsed);
            }

            var isExternal = !profile.Endpoint.IsLoopback;
            if (!TryGetReservationUnits(profile, isExternal, out var reservationUnits))
            {
                return Failure(
                    request,
                    profile,
                    "budget-policy-required",
                    "External assistant requests require explicit input, output, and total unit limits.",
                    false,
                    stopwatch.Elapsed);
            }

            var completionEndpoint = BuildCompletionEndpoint(profile.Endpoint);
            for (var attempt = 0; attempt <= policy.MaximumRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var reservation = isExternal
                    ? usageBudget.TryReserve(reservationUnits)
                    : AssistantUsageReservation.Unmetered;
                if (reservation is null)
                {
                    return Failure(
                        request,
                        profile,
                        "budget-exhausted",
                        "The approved external assistant usage envelope is exhausted.",
                        false,
                        stopwatch.Elapsed,
                        attempt);
                }

                AssistantCredential? credential;
                try
                {
                    credential = await credentialStore.ResolveAsync(
                        profile.CredentialHandle,
                        cancellationToken);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    return Failure(
                        request,
                        profile,
                        "credential-unavailable",
                        "The assistant credential cannot be read from the approved secret store.",
                        false,
                        stopwatch.Elapsed,
                        attempt);
                }

                using (credential)
                {
                    if (credential is null)
                    {
                        return Failure(
                            request,
                            profile,
                            "credential-revoked",
                            "The assistant credential is missing or revoked.",
                            false,
                            stopwatch.Elapsed,
                            attempt);
                    }

                    try
                    {
                        using var outbound = CreateRequest(
                            completionEndpoint,
                            requestBody,
                            credential);
                        reservation.Commit(reservationUnits);
                        using var response = await client.SendAsync(
                            outbound,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken).ConfigureAwait(false);

                        if (IsRedirect(response.StatusCode))
                        {
                            return Failure(
                                request,
                                profile,
                                "provider-redirect-rejected",
                                "The assistant provider attempted to redirect the configured endpoint.",
                                false,
                                stopwatch.Elapsed,
                                attempt);
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            var transient = IsRetryable(response.StatusCode);
                            if (transient && attempt < policy.MaximumRetries)
                            {
                                OpenAiCompatibleMetrics.Retries.Add(1);
                                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            return Failure(
                                request,
                                profile,
                                transient ? "provider-temporarily-unavailable" : "provider-rejected-request",
                                transient
                                    ? "The assistant provider is temporarily unavailable."
                                    : "The assistant provider rejected the request.",
                                transient,
                                stopwatch.Elapsed,
                                attempt);
                        }

                        var responseBody = await ReadBoundedBodyAsync(
                            response.Content,
                            policy.MaximumResponseBodyBytes,
                            cancellationToken).ConfigureAwait(false);
                        ParsedCompletion parsed;
                        try
                        {
                            parsed = ParseResponse(responseBody, tools, providerToolNames);
                        }
                        catch (ProviderPayloadException exception)
                        {
                            return Failure(
                                request,
                                profile,
                                exception.Code,
                                exception.SafeMessage,
                                false,
                                stopwatch.Elapsed,
                                attempt);
                        }
                        catch (JsonException)
                        {
                            return Failure(
                                request,
                                profile,
                                "provider-malformed-json",
                                "The assistant provider returned malformed JSON.",
                                false,
                                stopwatch.Elapsed,
                                attempt);
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(responseBody);
                        }

                        if (parsed.InputUnits > long.MaxValue - parsed.OutputUnits)
                        {
                            return Failure(
                                request,
                                profile,
                                "provider-malformed-response",
                                "The assistant provider returned invalid usage.",
                                false,
                                stopwatch.Elapsed,
                                attempt);
                        }

                        var totalUnits = parsed.InputUnits + parsed.OutputUnits;
                        reservation.Commit(totalUnits);
                        if ((profile.MaximumInputUnits is { } inputLimit
                                && parsed.InputUnits > inputLimit)
                            || (profile.MaximumOutputUnits is { } outputLimit
                                && parsed.OutputUnits > outputLimit))
                        {
                            return Failure(
                                request,
                                profile,
                                "provider-usage-limit-exceeded",
                                "The assistant provider reported usage above the configured limit.",
                                false,
                                stopwatch.Elapsed,
                                attempt,
                                parsed.InputUnits,
                                parsed.OutputUnits);
                        }

                        var usage = Usage(
                            profile,
                            parsed.InputUnits,
                            parsed.OutputUnits,
                            stopwatch.Elapsed,
                            "completed",
                            attempt,
                            parsed.ToolCalls.Count);
                        RecordMetrics(usage);
                        return new(
                            request.RequestId,
                            AssistantResponseStatus.Completed,
                            parsed.Content,
                            parsed.ToolCalls,
                            usage,
                            null);
                    }
                    catch (ResponseBodyTooLargeException)
                    {
                        return Failure(
                            request,
                            profile,
                            "provider-response-too-large",
                            "The assistant provider response exceeds the configured size limit.",
                            false,
                            stopwatch.Elapsed,
                            attempt);
                    }
                    catch (HttpRequestException) when (attempt < policy.MaximumRetries)
                    {
                        OpenAiCompatibleMetrics.Retries.Add(1);
                        await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    }
                    catch (HttpRequestException)
                    {
                        return Failure(
                            request,
                            profile,
                            "provider-temporarily-unavailable",
                            "The assistant provider is temporarily unavailable.",
                            true,
                            stopwatch.Elapsed,
                            attempt);
                    }
                    catch (IOException) when (attempt < policy.MaximumRetries)
                    {
                        OpenAiCompatibleMetrics.Retries.Add(1);
                        await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        return Failure(
                            request,
                            profile,
                            "provider-temporarily-unavailable",
                            "The assistant provider response could not be read.",
                            true,
                            stopwatch.Elapsed,
                            attempt);
                    }
                }
            }

            throw new UnreachableException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(requestBody);
        }
    }

    private static AssistantResponse Failure(
        AssistantRequest request,
        AssistantProviderProfile profile,
        string code,
        string message,
        bool isTransient,
        TimeSpan duration,
        int retryCount = 0,
        long? inputUnits = null,
        long? outputUnits = null)
    {
        var usage = Usage(
            profile,
            inputUnits,
            outputUnits,
            duration,
            code,
            retryCount,
            0);
        RecordMetrics(usage);
        return new(
            request.RequestId,
            AssistantResponseStatus.Failed,
            null,
            [],
            usage,
            new(code, message, isTransient));
    }

    private static AssistantUsage Usage(
        AssistantProviderProfile profile,
        long? inputUnits,
        long? outputUnits,
        TimeSpan duration,
        string resultClass,
        int retryCount,
        int toolCount) =>
        new(
            ProviderName,
            profile.Model,
            inputUnits,
            outputUnits,
            duration,
            resultClass,
            retryCount,
            toolCount);

    private static void RecordMetrics(AssistantUsage usage)
        => OpenAiCompatibleMetrics.Record(usage);

    private static ToolDefinition[] ResolveTools(
        AssistantSessionRequest session,
        AssistantRequest request)
    {
        var requested = request.AllowedToolNames.ToHashSet(StringComparer.Ordinal);
        if (requested.Count != request.AllowedToolNames.Count)
        {
            throw new ArgumentException("The tool allowlist contains a duplicate.", nameof(request));
        }

        var tools = session.AllowedTools
            .Where(tool => requested.Contains(tool.Name))
            .ToArray();
        if (tools.Length != requested.Count)
        {
            throw new InvalidOperationException("The request widened the session tool allowlist.");
        }

        return tools;
    }

    private static Dictionary<string, string> CreateProviderToolNames(
        ToolDefinition[] tools)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < tools.Length; index++)
        {
            var candidate = new string(tools[index].Name
                .Select(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'
                    ? character
                    : '_')
                .Take(56)
                .ToArray());
            if (string.IsNullOrEmpty(candidate))
            {
                candidate = "tool";
            }

            var providerName = candidate;
            if (result.Values.Contains(providerName, StringComparer.Ordinal))
            {
                providerName = $"{candidate}_{index.ToString(CultureInfo.InvariantCulture)}";
            }

            result.Add(tools[index].Name, providerName);
        }

        return result;
    }

    private static byte[] WriteRequest(
        AssistantProviderProfile profile,
        AssistantRequest request,
        ToolDefinition[] tools,
        Dictionary<string, string> providerToolNames)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", profile.Model);
            writer.WriteNumber("n", 1);
            writer.WriteStartArray("messages");
            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WriteString("content", request.Content);
            writer.WriteEndObject();
            writer.WriteEndArray();
            if (profile.MaximumOutputUnits is { } maximumOutput)
            {
                writer.WriteNumber("max_tokens", maximumOutput);
            }

            if (tools.Length > 0)
            {
                writer.WriteStartArray("tools");
                foreach (var tool in tools)
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", "function");
                    writer.WriteStartObject("function");
                    writer.WriteString("name", providerToolNames[tool.Name]);
                    writer.WriteString("description", tool.Description);
                    writer.WriteStartObject("parameters");
                    writer.WriteString("type", "object");
                    writer.WriteStartObject("properties");
                    foreach (var field in tool.InputSchema)
                    {
                        writer.WriteStartObject(field.Name);
                        writer.WriteString("type", JsonType(field.Kind));
                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                    writer.WriteStartArray("required");
                    foreach (var required in tool.InputSchema.Where(field => field.Required))
                    {
                        writer.WriteStringValue(required.Name);
                    }

                    writer.WriteEndArray();
                    writer.WriteBoolean("additionalProperties", false);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteString("tool_choice", "auto");
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static HttpRequestMessage CreateRequest(
        Uri endpoint,
        byte[] body,
        AssistantCredential credential)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new string(credential.Value));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async ValueTask<byte[]> ReadBoundedBodyAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximumBytes)
        {
            throw new ResponseBodyTooLargeException();
        }

        var buffer = ArrayPool<byte>.Shared.Rent(maximumBytes + 1);
        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var totalRead = 0;
            while (totalRead < maximumBytes + 1)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, maximumBytes + 1 - totalRead),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead > maximumBytes)
            {
                throw new ResponseBodyTooLargeException();
            }

            return buffer.AsSpan(0, totalRead).ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ParsedCompletion ParseResponse(
        ReadOnlyMemory<byte> body,
        ToolDefinition[] tools,
        Dictionary<string, string> providerToolNames)
    {
        using var document = JsonDocument.Parse(body);
        var root = RequireObject(document.RootElement);
        var usage = RequireObject(RequireProperty(root, "usage"));
        var inputUnits = RequireNonNegativeInt64(usage, "prompt_tokens");
        var outputUnits = RequireNonNegativeInt64(usage, "completion_tokens");
        var choices = RequireArray(RequireProperty(root, "choices"));
        if (choices.GetArrayLength() != 1)
        {
            throw Malformed("The assistant provider response must contain exactly one choice.");
        }

        var message = RequireObject(RequireProperty(RequireObject(choices[0]), "message"));
        if (!string.Equals(RequireString(message, "role"), "assistant", StringComparison.Ordinal))
        {
            throw Malformed("The assistant provider returned an invalid message role.");
        }

        string? content = null;
        if (message.TryGetProperty("content", out var contentElement)
            && contentElement.ValueKind != JsonValueKind.Null)
        {
            if (contentElement.ValueKind != JsonValueKind.String)
            {
                throw Malformed("The assistant provider returned invalid message content.");
            }

            content = contentElement.GetString();
        }

        var toolCalls = new List<AssistantToolCall>();
        if (message.TryGetProperty("tool_calls", out var callsElement)
            && callsElement.ValueKind != JsonValueKind.Null)
        {
            var calls = RequireArray(callsElement);
            foreach (var callElement in calls.EnumerateArray())
            {
                var call = RequireObject(callElement);
                if (!call.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || !string.Equals(type.GetString(), "function", StringComparison.Ordinal))
                {
                    throw Malformed("The assistant provider returned an unsupported tool-call type.");
                }

                var function = RequireObject(RequireProperty(call, "function"));
                var providerName = RequireString(function, "name");
                var matched = providerToolNames.SingleOrDefault(
                    pair => string.Equals(pair.Value, providerName, StringComparison.Ordinal));
                if (string.IsNullOrEmpty(matched.Key))
                {
                    throw new ProviderPayloadException(
                        "provider-unknown-tool",
                        "The assistant provider requested a tool that was not allowed.");
                }

                var tool = tools.Single(candidate =>
                    string.Equals(candidate.Name, matched.Key, StringComparison.Ordinal));
                var argumentsText = RequireString(function, "arguments");
                JsonDocument argumentsDocument;
                try
                {
                    argumentsDocument = JsonDocument.Parse(argumentsText);
                }
                catch (JsonException exception)
                {
                    throw new ProviderPayloadException(
                        "provider-malformed-tool",
                        "The assistant provider returned malformed tool arguments.",
                        exception);
                }

                using (argumentsDocument)
                {
                    if (argumentsDocument.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw new ProviderPayloadException(
                            "provider-malformed-tool",
                            "The assistant provider returned tool arguments that are not an object.");
                    }

                    var argumentsObject = argumentsDocument.RootElement;
                    var arguments = ValidateAndCloneArguments(tool, argumentsObject);
                    toolCalls.Add(new(tool.Name, arguments));
                }
            }
        }

        return new(content, toolCalls, inputUnits, outputUnits);
    }

    private static Dictionary<string, JsonElement> ValidateAndCloneArguments(
        ToolDefinition tool,
        JsonElement arguments)
    {
        var schema = tool.InputSchema.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in arguments.EnumerateObject())
        {
            if (!schema.TryGetValue(property.Name, out var field)
                || !Matches(field.Kind, property.Value)
                || !result.TryAdd(property.Name, property.Value.Clone()))
            {
                throw new ProviderPayloadException(
                    "provider-malformed-tool",
                    "The assistant provider returned tool arguments that do not match the allowed schema.");
            }
        }

        if (tool.InputSchema.Any(field => field.Required && !result.ContainsKey(field.Name)))
        {
            throw new ProviderPayloadException(
                "provider-malformed-tool",
                "The assistant provider omitted a required tool argument.");
        }

        return result;
    }

    private static bool Matches(ToolValueKind kind, JsonElement value) =>
        kind switch
        {
            ToolValueKind.Text => value.ValueKind == JsonValueKind.String,
            ToolValueKind.Numeric => value.ValueKind == JsonValueKind.Number,
            ToolValueKind.Logical => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            ToolValueKind.Structured => value.ValueKind == JsonValueKind.Object,
            ToolValueKind.Sequence => value.ValueKind == JsonValueKind.Array,
            _ => false,
        };

    private static string JsonType(ToolValueKind kind) =>
        kind switch
        {
            ToolValueKind.Text => "string",
            ToolValueKind.Numeric => "number",
            ToolValueKind.Logical => "boolean",
            ToolValueKind.Structured => "object",
            ToolValueKind.Sequence => "array",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tool value kind."),
        };

    private static JsonElement RequireProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
            ? value
            : throw Malformed($"The assistant provider response omitted '{propertyName}'.");

    private static JsonElement RequireObject(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? element
            : throw Malformed("The assistant provider returned an invalid object.");

    private static JsonElement RequireArray(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array
            ? element
            : throw Malformed("The assistant provider returned an invalid array.");

    private static string RequireString(JsonElement element, string propertyName)
    {
        var value = RequireProperty(element, propertyName);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw Malformed($"The assistant provider returned an invalid '{propertyName}'.");
    }

    private static long RequireNonNegativeInt64(JsonElement element, string propertyName)
    {
        var value = RequireProperty(element, propertyName);
        return value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number)
            && number >= 0
                ? number
                : throw Malformed($"The assistant provider returned invalid '{propertyName}'.");
    }

    private static ProviderPayloadException Malformed(string message) =>
        new("provider-malformed-response", message);

    private static bool TryGetReservationUnits(
        AssistantProviderProfile profile,
        bool isExternal,
        out long reservationUnits)
    {
        reservationUnits = 0;
        if (!isExternal)
        {
            return true;
        }

        if (profile.MaximumInputUnits is not { } input
            || profile.MaximumOutputUnits is not { } output
            || input <= 0
            || output <= 0)
        {
            return false;
        }

        try
        {
            reservationUnits = checked(input + output);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static Uri BuildCompletionEndpoint(Uri endpoint) =>
        new($"{endpoint.AbsoluteUri.TrimEnd('/')}/chat/completions", UriKind.Absolute);

    private static string ValidateAndCanonicalizeEndpoint(Uri endpoint)
    {
        OpenAiCompatibleAssistantAdapter.Validate(
            new(
                endpoint,
                "validation",
                "credential://validation/handle",
                TimeSpan.FromSeconds(1),
                "validation",
                "validation",
                1,
                1));
        return endpoint.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped).TrimEnd('/');
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        (int)statusCode is >= 300 and <= 399;

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            || (int)statusCode is >= 500 and <= 599;

    private Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var multiplier = Math.Pow(2, attempt);
        var delay = TimeSpan.FromMilliseconds(
            Math.Min(
                policy.RetryBaseDelay.TotalMilliseconds * multiplier,
                TimeSpan.FromSeconds(5).TotalMilliseconds));
        return Task.Delay(delay, cancellationToken);
    }

    private sealed record ParsedCompletion(
        string? Content,
        IReadOnlyList<AssistantToolCall> ToolCalls,
        long InputUnits,
        long OutputUnits);

    private sealed class ProviderPayloadException : Exception
    {
        public ProviderPayloadException(string code, string safeMessage, Exception? inner = null)
            : base(safeMessage, inner)
        {
            Code = code;
            SafeMessage = safeMessage;
        }

        public string Code { get; }

        public string SafeMessage { get; }
    }

    private sealed class ResponseBodyTooLargeException : Exception;
}

public static class OpenAiCompatibleMetrics
{
    public const string MeterName = "Andreja.Assistant.OpenAiCompatible";
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("andreja_assistant_requests_total");
    public static readonly Counter<long> Retries =
        Meter.CreateCounter<long>("andreja_assistant_retries_total");
    public static readonly Counter<long> InputUnits =
        Meter.CreateCounter<long>("andreja_assistant_input_units_total");
    public static readonly Counter<long> OutputUnits =
        Meter.CreateCounter<long>("andreja_assistant_output_units_total");
    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("andreja_assistant_request_duration_ms");

    internal static void Record(AssistantUsage usage)
    {
        Requests.Add(
            1,
            new KeyValuePair<string, object?>("result.class", usage.ResultClass));
        Duration.Record(usage.Duration.TotalMilliseconds);
        if (usage.InputUnits is { } inputUnits)
        {
            InputUnits.Add(inputUnits);
        }

        if (usage.OutputUnits is { } outputUnits)
        {
            OutputUnits.Add(outputUnits);
        }
    }
}

internal sealed class AssistantUsageBudget(long approvedTotalUnits)
{
    private readonly object sync = new();
    private long consumed;
    private long reserved;

    public AssistantUsageReservation? TryReserve(long maximumUnits)
    {
        lock (sync)
        {
            if (maximumUnits <= 0
                || maximumUnits > approvedTotalUnits - consumed - reserved)
            {
                return null;
            }

            reserved += maximumUnits;
            return new(this, maximumUnits);
        }
    }

    internal void Complete(long reservedUnits, long consumedUnits)
    {
        lock (sync)
        {
            reserved -= reservedUnits;
            consumed = checked(consumed + Math.Min(consumedUnits, reservedUnits));
        }
    }
}

internal sealed class AssistantUsageReservation : IDisposable
{
    public static AssistantUsageReservation Unmetered { get; } = new();
    private AssistantUsageBudget? budget;
    private readonly long reservedUnits;
    private long consumedUnits;

    private AssistantUsageReservation()
    {
    }

    public AssistantUsageReservation(AssistantUsageBudget budget, long reservedUnits)
    {
        this.budget = budget;
        this.reservedUnits = reservedUnits;
    }

    public void Commit(long units)
    {
        consumedUnits = units;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref budget, null)?.Complete(reservedUnits, consumedUnits);
    }
}
