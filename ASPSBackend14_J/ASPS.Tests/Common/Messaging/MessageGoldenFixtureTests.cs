using Common.Generated.Messaging.V1;
using FluentAssertions;

namespace ASPS.Tests.Common.Messaging;

public class MessageGoldenFixtureTests
{
    [Fact]
    public void AllGoldenFixtures_AreConsumedByCSharpRuntimeValidator()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var fixtureDirectory = Path.Combine(
            repositoryRoot, "contracts", "messaging", "v1", "fixtures");
        var fixturePaths = Directory.GetFiles(fixtureDirectory, "*.json");

        fixturePaths.Should().HaveCount(3);
        foreach (var path in fixturePaths)
        {
            var validation = MessageEnvelopeValidator.DeserializeAndValidate(
                File.ReadAllText(path), out var envelope);
            validation.IsValid.Should().BeTrue(Path.GetFileName(path));
            envelope.Should().NotBeNull();
        }
    }
}
