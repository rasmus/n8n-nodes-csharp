using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace N8n.CSharpRunner.Tests;

public class RunnerSmokeTests
{
    [Fact]
    public async Task AllItems_Returns_Items()
    {
        var request = new
        {
            mode = "allItems",
            items = new object[] { new { a = 1 }, new { a = 2 } },
            code = "return Items;",
        };

        var response = await RunRunnerAsync(request);
        Assert.True(response.Ok);
        Assert.Equal(2, response.Items.Length);
        Assert.Equal(1, response.Items[0].GetProperty("a").GetInt32());
        Assert.Equal(2, response.Items[1].GetProperty("a").GetInt32());
    }

    [Fact]
    public async Task PerItem_Allows_Multiple_Output_Items()
    {
        var request = new
        {
            mode = "perItem",
            items = new object[] { new { a = 1 }, new { a = 2 } },
            code = "return new object[] { new { i = Index, doubled = ((int)Item![\"a\"]!) * 2 } };",
        };

        var response = await RunRunnerAsync(request);
        Assert.True(response.Ok);
        Assert.Equal(2, response.Items.Length);
        Assert.Equal(0, response.Items[0].GetProperty("i").GetInt32());
        Assert.Equal(2, response.Items[0].GetProperty("doubled").GetInt32());
        Assert.Equal(1, response.Items[1].GetProperty("i").GetInt32());
        Assert.Equal(4, response.Items[1].GetProperty("doubled").GetInt32());
    }

    [Fact]
    public async Task AllItems_Array_Of_Scalars_Is_Wrapped_As_Value_Items()
    {
        var request = new
        {
            mode = "allItems",
            items = new object[] { new { a = 1 }, new { a = 2 } },
            code = "return Items.Select(_ => 1).ToArray();",
        };

        var response = await RunRunnerAsync(request);
        Assert.True(response.Ok);
        Assert.Equal(2, response.Items.Length);
        Assert.Equal(1, response.Items[0].GetProperty("value").GetInt32());
        Assert.Equal(1, response.Items[1].GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task AllItems_Can_Read_Properties_With_Str_Helper()
    {
        var request = new
        {
            mode = "allItems",
            items = new object[] { new { name = "test" } },
            code = "return Items.Select(i => new { message = i.Str(\"name\") + \" from C#\" }).ToArray();",
        };

        var response = await RunRunnerAsync(request);
        Assert.True(response.Ok);
        Assert.Single(response.Items);
        Assert.Equal("test from C#", response.Items[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Compilation_Error_Returns_Ok_False()
    {
        var request = new
        {
            mode = "allItems",
            items = new object[] { new { a = 1 } },
            code = "return DoesNotExist + 1;",
        };

        var response = await RunRunnerAsync(request);
        Assert.False(response.Ok);
        Assert.Contains("compilation", response.Error?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Header_Using_Directive_Adds_Import()
    {
        var request = new
        {
            mode = "allItems",
            items = new object[] { new { a = 1 } },
            code = """

/* block comment */
/// xml doc comment (ignored)
// some normal comment
#nullable enable
//   using   System.Text ;

var sb = new StringBuilder();
return new { ok = sb != null };
""",
        };

        var response = await RunRunnerAsync(request);
        Assert.True(response.Ok);
        Assert.Single(response.Items);
        Assert.True(response.Items[0].GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Invalid_Header_Using_Directive_Returns_Ok_False()
    {
        var request = new
        {
            mode = "allItems",
            items = new object[] { new { a = 1 } },
            code = """
// using System..Text
return Items;
""",
        };

        var response = await RunRunnerAsync(request);
        Assert.False(response.Ok);
        Assert.Contains("Invalid using directive", response.Error?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Header_Using_Directive_Is_Not_Parsed_After_Code_Starts()
    {
        var request = new
        {
            mode = "allItems",
            items = new object[] { new { a = 1 } },
            code = """
var x = 1;
// using System.Text
var sb = new StringBuilder();
return new { ok = true };
""",
        };

        var response = await RunRunnerAsync(request);
        Assert.False(response.Ok);
        Assert.Contains("compilation", response.Error?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<RunnerResponse> RunRunnerAsync(object request)
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "runner", "N8n.CSharpRunner");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -c Release",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var p = new Process { StartInfo = psi };
        p.Start();

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await p.StandardInput.WriteAsync(json);
        p.StandardInput.Close();

        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();

        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"Runner process failed. ExitCode={p.ExitCode}. stderr={stderr}. stdout={stdout}");
        }

        var response = JsonSerializer.Deserialize<RunnerResponse>(stdout, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (response is null)
        {
            throw new InvalidOperationException($"Failed to parse runner response JSON. stdout={stdout}");
        }

        return response;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) &&
                File.Exists(Path.Combine(dir.FullName, "README.md")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root from test base directory.");
    }

    private sealed record RunnerResponse(
        bool Ok,
        JsonElement[] Items,
        RunnerError? Error
    );

    private sealed record RunnerError(
        string Message,
        string? Detail
    );
}
