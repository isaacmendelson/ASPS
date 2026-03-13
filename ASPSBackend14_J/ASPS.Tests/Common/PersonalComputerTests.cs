using Common.Entities;
using Common.Enums;
using Common.Models;
using FluentAssertions;
using Xunit;

namespace ASPS.Tests.Common;

public class PersonalComputerTests
{
    #region Constructor and Basic Properties Tests

    [Fact]
    public void DefaultConstructor_SetsEmptyStrings()
    {
        // Act
        var pc = new PersonalComputer();

        // Assert
        pc.MotherboardSerial.Should().BeEmpty();
        pc.UserAgent.Should().BeEmpty();
    }

    [Fact]
    public void Type_CanBeSetAndRetrieved()
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.Type = PersonalComputerType.Desktop;

        // Assert
        pc.Type.Should().Be(PersonalComputerType.Desktop);
    }

    [Fact]
    public void MotherboardSerial_CanBeSetAndRetrieved()
    {
        // Arrange
        var pc = new PersonalComputer();
        var serial = "MB-12345-ABCDE";

        // Act
        pc.MotherboardSerial = serial;

        // Assert
        pc.MotherboardSerial.Should().Be(serial);
    }

    [Fact]
    public void UserAgent_CanBeSetAndRetrieved()
    {
        // Arrange
        var pc = new PersonalComputer();
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        // Act
        pc.UserAgent = userAgent;

        // Assert
        pc.UserAgent.Should().Be(userAgent);
    }

    [Fact]
    public void Timezone_CanBeSetAndRetrieved()
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.Timezone = -5; // EST

        // Assert
        pc.Timezone.Should().Be(-5);
    }

    #endregion

    #region TypeName Tests

    [Fact]
    public void TypeName_ReturnsPersonalComputer()
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act & Assert
        pc.TypeName.Should().Be("PersonalComputer");
    }

    #endregion

    #region PersonalComputerType Tests

    [Theory]
    [InlineData(PersonalComputerType.Desktop)]
    [InlineData(PersonalComputerType.Laptop)]
    [InlineData(PersonalComputerType.Desktop)]
    public void Type_AcceptsDifferentComputerTypes(PersonalComputerType type)
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.Type = type;

        // Assert
        pc.Type.Should().Be(type);
    }

    #endregion

    #region Inherited Properties Tests

    [Fact]
    public void InheritsFromUserDevice_CanAccessBaseProperties()
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.DeviceUid = "MyLaptop";
        pc.MAC = "00:11:22:33:44:55";

        // Assert
        pc.DeviceUid.Should().Be("MyLaptop");
        pc.MAC.Should().Be("00:11:22:33:44:55");
    }

    [Fact]
    public void OperatingSystem_CanBeSetFromBaseClass()
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.OperatingSystem = OperatingSystemType.Windows;

        // Assert
        pc.OperatingSystem.Should().Be(OperatingSystemType.Windows);
    }

    #endregion

    #region Object Initializer Tests

    [Fact]
    public void ObjectInitializer_CanSetAllProperties()
    {
        // Arrange & Act
        var pc = new PersonalComputer
        {
            Type = PersonalComputerType.Laptop,
            MotherboardSerial = "SERIAL-123",
            UserAgent = "Mozilla/5.0",
            Timezone = 2,
            DeviceUid = "Work Laptop",
            MAC = "AA:BB:CC:DD:EE:FF",
            OperatingSystem = OperatingSystemType.Windows
        };

        // Assert
        pc.Type.Should().Be(PersonalComputerType.Laptop);
        pc.MotherboardSerial.Should().Be("SERIAL-123");
        pc.UserAgent.Should().Be("Mozilla/5.0");
        pc.Timezone.Should().Be(2);
        pc.DeviceUid.Should().Be("Work Laptop");
        pc.MAC.Should().Be("AA:BB:CC:DD:EE:FF");
        pc.OperatingSystem.Should().Be(OperatingSystemType.Windows);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("VERY-LONG-MOTHERBOARD-SERIAL-NUMBER-123456789-ABCDEFGHIJKLMNOP")]
    public void MotherboardSerial_AcceptsVariousInputs(string serial)
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.MotherboardSerial = serial;

        // Assert
        pc.MotherboardSerial.Should().Be(serial);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")]
    public void UserAgent_AcceptsVariousInputs(string userAgent)
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.UserAgent = userAgent;

        // Assert
        pc.UserAgent.Should().Be(userAgent);
    }

    [Theory]
    [InlineData(-12)] // UTC-12
    [InlineData(0)]   // UTC
    [InlineData(14)]  // UTC+14
    public void Timezone_AcceptsValidRanges(int timezone)
    {
        // Arrange
        var pc = new PersonalComputer();

        // Act
        pc.Timezone = timezone;

        // Assert
        pc.Timezone.Should().Be(timezone);
    }

    #endregion

    #region Business Scenarios

    [Fact]
    public void DesktopScenario_WindowsWorkstation()
    {
        // Arrange & Act
        var pc = new PersonalComputer
        {
            Type = PersonalComputerType.Desktop,
            MotherboardSerial = "WS-2024-001",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timezone = -5, // EST
            Model = "Dev Workstation",
            OperatingSystem = OperatingSystemType.Windows,
            MAC = "00:11:22:33:44:55",
            DeviceUid = "ws-001"
        };

        // Assert
        pc.Type.Should().Be(PersonalComputerType.Desktop);
        pc.OperatingSystem.Should().Be(OperatingSystemType.Windows);
        pc.TypeName.Should().Be("PersonalComputer");
        pc.MotherboardSerial.Should().NotBeEmpty();
        pc.Timezone.Should().Be(-5);
    }

    [Fact]
    public void LaptopScenario_MacOS()
    {
        // Arrange & Act
        var pc = new PersonalComputer
        {
            Type = PersonalComputerType.Laptop,
            MotherboardSerial = "MBA-2024-SERIAL",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            Timezone = -8, // PST
            Model = "MacBook Air",
            OperatingSystem = OperatingSystemType.MacOS,
            MAC = "FF:EE:DD:CC:BB:AA",
            DeviceUid = "mac-laptop-001"
        };

        // Assert
        pc.Type.Should().Be(PersonalComputerType.Laptop);
        pc.OperatingSystem.Should().Be(OperatingSystemType.MacOS);
        pc.TypeName.Should().Be("PersonalComputer");
        pc.UserAgent.Should().Contain("Macintosh");
    }

    [Fact]
    public void LinuxDesktop_Scenario()
    {
        // Arrange & Act
        var pc = new PersonalComputer
        {
            Type = PersonalComputerType.Desktop,
            MotherboardSerial = "LINUX-DESKTOP-001",
            UserAgent = "Mozilla/5.0 (X11; Linux x86_64)",
            Timezone = 1, // CET
            Model = "Linux Dev Machine",
            OperatingSystem = OperatingSystemType.Linux,
            MAC = "12:34:56:78:90:AB",
            DeviceUid = "linux-001"
        };

        // Assert
        pc.Type.Should().Be(PersonalComputerType.Desktop);
        pc.OperatingSystem.Should().Be(OperatingSystemType.Linux);
        pc.UserAgent.Should().Contain("Linux");
    }

    #endregion

    #region Type Distinction Tests

    [Fact]
    public void Desktop_Laptop_Tablet_AreDifferentTypes()
    {
        // Arrange
        var desktop = new PersonalComputer { Type = PersonalComputerType.Desktop };
        var laptop = new PersonalComputer { Type = PersonalComputerType.Laptop };
        var tablet = new PersonalComputer { Type = PersonalComputerType.Tablet };

        // Assert
        desktop.Type.Should().NotBe(laptop.Type);
        laptop.Type.Should().NotBe(tablet.Type);
        tablet.Type.Should().NotBe(desktop.Type);
    }

    #endregion

    #region Comparison and Equality Tests

    [Fact]
    public void TwoComputers_WithSameMotherboardSerial_CanExist()
    {
        // Arrange
        var pc1 = new PersonalComputer { MotherboardSerial = "SAME-SERIAL" };
        var pc2 = new PersonalComputer { MotherboardSerial = "SAME-SERIAL" };

        // Assert
        // Both objects exist with same serial (business logic would need to prevent this if required)
        pc1.MotherboardSerial.Should().Be(pc2.MotherboardSerial);
        ReferenceEquals(pc1, pc2).Should().BeFalse();
    }

    [Fact]
    public void Timezone_PositiveAndNegative_CanBeDifferentiated()
    {
        // Arrange
        var pcUSA = new PersonalComputer { Timezone = -5 };  // EST
        var pcEurope = new PersonalComputer { Timezone = 1 }; // CET

        // Assert
        pcUSA.Timezone.Should().BeLessThan(0);
        pcEurope.Timezone.Should().BeGreaterThan(0);
        pcUSA.Timezone.Should().NotBe(pcEurope.Timezone);
    }

    #endregion
}
