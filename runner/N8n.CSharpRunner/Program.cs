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

            var mode = request.Mode?.Trim() ?? "allItems";
            var itemNodes = (request.Items ?? Array.Empty<JsonElement>())
                .Select(e => JsonNode.Parse(e.GetRawText()))
                .ToArray();

            var globalsItems = new JsonArray(itemNodes);

            var scriptOptions = ScriptOptions.Default
                .AddReferences(
                    typeof(object).Assembly,
                    typeof(Enumerable).Assembly,
                    typeof(JsonNode).Assembly,
                    typeof(Globals).Assembly)
                .AddImports(
                    "System",
                    "System.Linq",
                    "System.Collections.Generic",
                    "System.Text.Json",
                    "System.Text.Json.Nodes",
                    "N8n.CSharpRunner");

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
