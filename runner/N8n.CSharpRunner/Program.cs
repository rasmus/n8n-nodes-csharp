using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace N8n.CSharpRunner;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static async Task<int> Main()
    {
        try
        {
            var input = await Console.In.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(input))
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: new RunnerError("No input received on stdin.")
                ));
                return 0;
            }

            ExecutionRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ExecutionRequest>(input, JsonOptions);
            }
            catch (Exception ex)
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: new RunnerError("Failed to parse JSON request.", ex.ToString())
                ));
                return 0;
            }

            if (request is null)
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: new RunnerError("Request was null.")
                ));
                return 0;
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: new RunnerError("Code must not be empty.")
                ));
                return 0;
            }

            if (!TryExtractHeaderUsingDirectives(request.Code, out var headerImports, out var directiveError))
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: directiveError
                ));
                return 0;
            }

            var mode = request.Mode?.Trim() ?? "allItems";
            var itemNodes = (request.Items ?? Array.Empty<JsonElement>())
                .Select(e => JsonNode.Parse(e.GetRawText()))
                .ToArray();

            var globalsItems = new JsonArray(itemNodes);

            var defaultImports = new[]
            {
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "System.Text.Json",
                "System.Text.Json.Nodes",
                "N8n.CSharpRunner",
            };

            var mergedImports = MergeImports(defaultImports, headerImports);

            var scriptOptions = ScriptOptions.Default
                .AddReferences(
                    typeof(object).Assembly,
                    typeof(Enumerable).Assembly,
                    typeof(JsonNode).Assembly,
                    typeof(Globals).Assembly)
                .AddImports(mergedImports);

            Script<object?> script;
            try
            {
                script = CSharpScript.Create<object?>(request.Code, scriptOptions, typeof(Globals));
                script.Compile();
            }
            catch (CompilationErrorException cex)
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: new RunnerError("C# compilation error", string.Join("\n", cex.Diagnostics))
                ));
                return 0;
            }

            ScriptRunner<object?> runner;
            try
            {
                runner = script.CreateDelegate();
            }
            catch (CompilationErrorException cex)
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: new RunnerError("C# compilation error", string.Join("\n", cex.Diagnostics))
                ));
                return 0;
            }
            var results = new List<JsonNode?>();

            try
            {
                if (string.Equals(mode, "perItem", StringComparison.OrdinalIgnoreCase))
                {
                    for (var i = 0; i < itemNodes.Length; i++)
                    {
                        var globals = new Globals
                        {
                            Items = globalsItems,
                            Item = itemNodes[i],
                            Index = i,
                        };

                        var result = await ExecuteWithConsoleRedirectAsync(() => runner(globals));
                        AppendNormalized(results, result);
                    }
                }
                else
                {
                    var globals = new Globals
                    {
                        Items = globalsItems,
                        Item = null,
                        Index = null,
                    };

                    var result = await ExecuteWithConsoleRedirectAsync(() => runner(globals));
                    AppendNormalized(results, result);
                }
            }
            catch (Exception ex)
            {
                await WriteResponseAsync(new RunnerResponse(
                    Ok: false,
                    Items: Array.Empty<JsonNode?>(),
                    Error: new RunnerError("C# runtime error", ex.ToString())
                ));
                return 0;
            }

            await WriteResponseAsync(new RunnerResponse(
                Ok: true,
                Items: results.ToArray(),
                Error: null
            ));

            return 0;
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(new RunnerResponse(
                Ok: false,
                Items: Array.Empty<JsonNode?>(),
                Error: new RunnerError("Runner crashed", ex.ToString())
            ));
            return 0;
        }
    }

    private static async Task<object?> ExecuteWithConsoleRedirectAsync(Func<Task<object?>> action)
    {
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(Console.Error);
            return await action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static IReadOnlyList<string> MergeImports(IReadOnlyList<string> defaults, IReadOnlyList<string> headerImports)
    {
        var merged = new List<string>(capacity: defaults.Count + headerImports.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in defaults)
        {
            if (seen.Add(item))
            {
                merged.Add(item);
            }
        }

        foreach (var item in headerImports)
        {
            if (seen.Add(item))
            {
                merged.Add(item);
            }
        }

        return merged;
    }

    private static bool TryExtractHeaderUsingDirectives(
        string code,
        out IReadOnlyList<string> imports,
        out RunnerError? error)
    {
        imports = Array.Empty<string>();
        error = null;

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var inBlockComment = false;
        var index = 0;

        if (code.Length > 0 && code[0] == '\uFEFF')
        {
            index = 1;
        }

        while (index <= code.Length)
        {
            var lineStart = index;
            var lineEnd = code.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = code.Length;
            }

            var line = code.AsSpan(lineStart, lineEnd - lineStart);
            if (line.Length > 0 && line[^1] == '\r')
            {
                line = line[..^1];
            }

            if (!TryProcessHeaderLine(line, ref inBlockComment, results, seen, out error))
            {
                if (error is not null)
                {
                    return false;
                }

                imports = results;
                return true;
            }

            if (lineEnd >= code.Length)
            {
                break;
            }

            index = lineEnd + 1;
        }

        imports = results;
        return true;
    }

    private static bool TryProcessHeaderLine(
        ReadOnlySpan<char> line,
        ref bool inBlockComment,
        List<string> imports,
        HashSet<string> seen,
        out RunnerError? error)
    {
        error = null;
        var position = 0;

        while (true)
        {
            if (inBlockComment)
            {
                var endIdx = line.Slice(position).IndexOf("*/".AsSpan());
                if (endIdx < 0)
                {
                    return true;
                }

                position += endIdx + 2;
                inBlockComment = false;
                continue;
            }

            while (position < line.Length && (line[position] == ' ' || line[position] == '\t'))
            {
                position++;
            }

            if (position >= line.Length)
            {
                return true;
            }

            if (line.Slice(position).StartsWith("//".AsSpan(), StringComparison.Ordinal))
            {
                var commentContent = line.Slice(position + 2);
                while (commentContent.Length > 0 && (commentContent[0] == ' ' || commentContent[0] == '\t'))
                {
                    commentContent = commentContent[1..];
                }

                if (TryParseUsingDirective(commentContent, out var ns, out var detail))
                {
                    if (!seen.Add(ns))
                    {
                        return true;
                    }

                    imports.Add(ns);
                    return true;
                }

                if (detail is not null)
                {
                    error = new RunnerError(
                        "Invalid using directive syntax.",
                        $"Line: '{line.ToString()}'. Expected: // using <Namespace>"
                    );
                    return false;
                }

                return true;
            }

            if (line.Slice(position).StartsWith("/*".AsSpan(), StringComparison.Ordinal))
            {
                inBlockComment = true;
                position += 2;
                continue;
            }

            if (line[position] == '#')
            {
                return true;
            }

            return false;
        }
    }

    private static bool TryParseUsingDirective(ReadOnlySpan<char> commentContent, out string ns, out string? errorDetail)
    {
        ns = string.Empty;
        errorDetail = null;

        if (!commentContent.StartsWith("using".AsSpan(), StringComparison.Ordinal))
        {
            return false;
        }

        if (commentContent.Length > 5 && !char.IsWhiteSpace(commentContent[5]))
        {
            return false;
        }

        var remainder = commentContent.Length == 5
            ? ReadOnlySpan<char>.Empty
            : commentContent[5..];

        remainder = remainder.Trim();
        if (remainder.IsEmpty)
        {
            errorDetail = "Missing namespace.";
            return false;
        }

        if (remainder.EndsWith(";".AsSpan(), StringComparison.Ordinal))
        {
            remainder = remainder[..^1].Trim();
        }

        if (remainder.IsEmpty)
        {
            errorDetail = "Missing namespace.";
            return false;
        }

        if (!IsValidNamespaceToken(remainder))
        {
            errorDetail = "Invalid namespace token.";
            return false;
        }

        ns = remainder.ToString();
        return true;
    }

    private static bool IsValidNamespaceToken(ReadOnlySpan<char> token)
    {
        if (token.StartsWith("global::".AsSpan(), StringComparison.Ordinal))
        {
            token = token[8..];
        }

        if (token.IsEmpty)
        {
            return false;
        }

        var segmentStart = true;

        for (var i = 0; i < token.Length; i++)
        {
            var c = token[i];

            if (segmentStart)
            {
                if (!(char.IsLetter(c) || c == '_'))
                {
                    return false;
                }
                segmentStart = false;
                continue;
            }

            if (c == '.')
            {
                segmentStart = true;
                continue;
            }

            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return !segmentStart;
    }

    private static void AppendNormalized(List<JsonNode?> outputItems, object? result)
    {
        if (result is null)
        {
            return;
        }

        JsonNode? node;
        try
        {
            node = JsonSerializer.SerializeToNode(result, JsonOptions);
        }
        catch
        {
            node = new JsonObject
            {
                ["value"] = result.ToString(),
            };
        }

        if (node is JsonArray arr)
        {
            foreach (var element in arr)
            {
                outputItems.Add(NormalizeItem(element));
            }
            return;
        }

        outputItems.Add(NormalizeItem(node));
    }

    private static JsonNode NormalizeItem(JsonNode? node)
    {
        if (node is null)
        {
            return new JsonObject { ["value"] = null };
        }

        // JsonNode instances are part of a tree and cannot be attached to multiple parents.
        // When a script returns arrays, elements already have a parent (the JsonArray),
        // so we deep-clone before returning/wrapping.
        var detached = node.DeepClone();

        if (detached is JsonObject)
        {
            return detached;
        }

        return new JsonObject { ["value"] = detached };
    }

    private static async Task WriteResponseAsync(RunnerResponse response)
    {
        var json = JsonSerializer.Serialize(response, JsonOptions);
        await Console.Out.WriteAsync(json);
    }
}

public sealed class Globals
{
    public JsonArray? Items { get; init; }
    public JsonNode? Item { get; init; }
    public int? Index { get; init; }
}

internal sealed record ExecutionRequest(
    string? Mode,
    JsonElement[]? Items,
    string? Code
);

internal sealed record RunnerResponse(
    bool Ok,
    JsonNode?[] Items,
    RunnerError? Error
);

internal sealed record RunnerError(
    string Message,
    string? Detail = null
);
