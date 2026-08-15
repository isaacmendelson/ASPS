using Business.Messaging;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace ASPS.Tests.Business.Messaging;

public sealed class CqrsHandlerRegistryTests
{
    private sealed class TestCommand : Command
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestCommandResult : CommandResult
    {
    }

    private sealed class TestQuery : Query
    {
        public string Filter { get; set; } = string.Empty;
    }

    private sealed class TestQueryResult : QueryResult
    {
        public string Echo { get; set; } = string.Empty;
    }

    private sealed class TestHandler
    {
        public Task<TestCommandResult> HandleAsync(TestCommand command)
        {
            return Task.FromResult(new TestCommandResult
            {
                Success = true,
                Message = $"handled {command.Name}"
            });
        }

        public Task<TestQueryResult> HandleAsync(TestQuery query)
        {
            return Task.FromResult(new TestQueryResult
            {
                Success = true,
                Echo = query.Filter
            });
        }
    }

    private static IServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        var provider = services.BuildServiceProvider();
        return provider.CreateScope();
    }

    [Fact]
    public async Task DispatchAsync_RegisteredCommand_InvokesHandlerAndReturnsSerializedResult()
    {
        var registry = new CqrsHandlerRegistry();
        registry.RegisterCommand<TestCommand, TestHandler>("TestCommand");
        using var scope = CreateScope();

        var command = new TestCommand { Name = "Alice" };
        var json = JsonConvert.SerializeObject(command);

        var response = await registry.DispatchAsync("TestCommand", json, scope);

        var result = JsonConvert.DeserializeObject<TestCommandResult>(response);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("handled Alice", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_RegisteredQuery_InvokesHandlerAndReturnsSerializedResult()
    {
        var registry = new CqrsHandlerRegistry();
        registry.RegisterQuery<TestQuery, TestHandler>("TestQuery");
        using var scope = CreateScope();

        var query = new TestQuery { Filter = "abc" };
        var json = JsonConvert.SerializeObject(query);

        var response = await registry.DispatchAsync("TestQuery", json, scope);

        var result = JsonConvert.DeserializeObject<TestQueryResult>(response);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("abc", result.Echo);
    }

    [Fact]
    public async Task DispatchAsync_UnknownMessageType_ReturnsErrorJson()
    {
        var registry = new CqrsHandlerRegistry();
        using var scope = CreateScope();

        var response = await registry.DispatchAsync("SomeUnknownType", "{}", scope);

        dynamic? parsed = JsonConvert.DeserializeObject(response);
        Assert.NotNull(parsed);
        Assert.False((bool)parsed!.Success);
        Assert.Contains("Unknown message type: SomeUnknownType", (string)parsed.Message);
    }

    [Fact]
    public void IsRegistered_ReturnsTrueForRegisteredType_FalseOtherwise()
    {
        var registry = new CqrsHandlerRegistry();
        registry.RegisterCommand<TestCommand, TestHandler>("TestCommand");

        Assert.True(registry.IsRegistered("TestCommand"));
        Assert.False(registry.IsRegistered("NotRegistered"));
    }

    [Fact]
    public void RegisteredTypes_ReturnsAllRegisteredNames()
    {
        var registry = new CqrsHandlerRegistry();
        registry.RegisterCommand<TestCommand, TestHandler>("TestCommand");
        registry.RegisterQuery<TestQuery, TestHandler>("TestQuery");
        registry.RegisterCustom("CustomType", (_, _) => Task.FromResult("{}"));

        var types = registry.RegisteredTypes;

        Assert.Equal(3, types.Count);
        Assert.Contains("TestCommand", types);
        Assert.Contains("TestQuery", types);
        Assert.Contains("CustomType", types);
    }

    [Fact]
    public async Task DispatchAsync_CommandWithInvalidJson_ReturnsErrorJson()
    {
        var registry = new CqrsHandlerRegistry();
        registry.RegisterCommand<TestCommand, TestHandler>("TestCommand");
        using var scope = CreateScope();

        // "null" deserializes to a null TestCommand, triggering the invalid-format branch.
        var response = await registry.DispatchAsync("TestCommand", "null", scope);

        dynamic? parsed = JsonConvert.DeserializeObject(response);
        Assert.NotNull(parsed);
        Assert.False((bool)parsed!.Success);
        Assert.Contains("Invalid TestCommand format", (string)parsed.Message);
    }

    [Fact]
    public async Task DispatchAsync_QueryWithDefault_UsesDefaultWhenJsonIsNull()
    {
        var registry = new CqrsHandlerRegistry();
        registry.RegisterQueryWithDefault<TestQuery, TestHandler>("TestQuery");
        using var scope = CreateScope();

        var response = await registry.DispatchAsync("TestQuery", "null", scope);

        var result = JsonConvert.DeserializeObject<TestQueryResult>(response);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(string.Empty, result.Echo);
    }

    [Fact]
    public async Task DispatchAsync_CustomHandler_IsInvoked()
    {
        var registry = new CqrsHandlerRegistry();
        registry.RegisterCustom("CustomType", (json, _) => Task.FromResult($"echo:{json}"));
        using var scope = CreateScope();

        var response = await registry.DispatchAsync("CustomType", "payload", scope);

        Assert.Equal("echo:payload", response);
    }
}
