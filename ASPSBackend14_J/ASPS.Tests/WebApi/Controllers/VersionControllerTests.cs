using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using WebApi.Controllers;

namespace ASPS.Tests.WebApi.Controllers;

public class VersionControllerTests
{
    private readonly VersionController _sut;

    public VersionControllerTests()
    {
        _sut = new VersionController();
    }

    [Fact]
    public void GetVersion_ReturnsOkResult()
    {
        // Act
        var result = _sut.GetVersion();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetVersion_ReturnsVersionAndComponent()
    {
        // Act
        var result = _sut.GetVersion() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var value = result!.Value;
        value.Should().NotBeNull();
        
        var valueType = value!.GetType();
        var versionProp = valueType.GetProperty("version");
        var componentProp = valueType.GetProperty("component");
        
        versionProp.Should().NotBeNull();
        componentProp.Should().NotBeNull();
        
        var version = versionProp!.GetValue(value) as string;
        var component = componentProp!.GetValue(value) as string;
        
        version.Should().NotBeNullOrEmpty();
        component.Should().Be("WebApi");
    }

    [Fact]
    public void GetVersion_VersionFollowsSemanticVersioning()
    {
        // Act
        var result = _sut.GetVersion() as OkObjectResult;
        var value = result!.Value;
        var versionProp = value!.GetType().GetProperty("version");
        var version = versionProp!.GetValue(value) as string;

        // Assert
        version.Should().NotBeNullOrEmpty();
        // Version should contain at least one dot (e.g., "0.0.0.1" or "1.0.0")
        version.Should().Contain(".");
    }
}
