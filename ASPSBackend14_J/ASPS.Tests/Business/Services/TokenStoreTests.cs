using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Business.Services;
using Business.Data.EF;
using Microsoft.EntityFrameworkCore;
using Common.Entities;
using Common.Models;

namespace ASPS.Tests.Business.Services
{
    public class TokenStoreTests : IDisposable
    {
        private readonly Mock<ILogger<TokenStore>> _loggerMock;
        private readonly IConfiguration _config;
        private readonly ServiceProvider _serviceProvider;
        private readonly AppDbContext _dbContext;
        private readonly TokenStore _sut;

        public TokenStoreTests()
        {
            _loggerMock = new Mock<ILogger<TokenStore>>();

            // Setup configuration defaults
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TokenManagement:TokenExpirationPeriod"] = "1440",
                ["TokenManagement:MaxExpiration"] = "10080"
            });
            _config = configBuilder.Build();

            // Setup in-memory database
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);

            // Setup service provider
            var services = new ServiceCollection();
            services.AddSingleton(_dbContext);
            _serviceProvider = services.BuildServiceProvider();

            // Create TokenStore instance
            _sut = new TokenStore(_config, _loggerMock.Object, _serviceProvider);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            _serviceProvider?.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParams_CreatesInstance()
        {
            // Assert
            _sut.Should().NotBeNull();
        }

        #endregion

        #region CreateToken Tests

        [Fact]
        public void CreateToken_WithValidParams_ReturnsToken()
        {
            // Arrange
            var deviceUid = "test-device-123";
            var userKeyField = "user-key-456";

            // Act
            var result = _sut.CreateToken(deviceUid, userKeyField);

            // Assert
            result.Should().NotBeNull();
            result.DeviceUid.Should().Be(deviceUid);
            result.UserKeyField.Should().Be(userKeyField);
            result.TokenValue.Should().NotBeNullOrEmpty();
            result.TokenValue.Length.Should().Be(64); // 32 bytes hex = 64 chars
            result.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.Expiration.Should().BeAfter(result.DateCreated);
        }

        [Fact]
        public void CreateToken_CalledTwiceForSameDevice_ReplacesToken()
        {
            // Arrange
            var deviceUid = "test-device-123";
            var userKeyField = "user-key-456";

            // Act
            var token1 = _sut.CreateToken(deviceUid, userKeyField);
            var token2 = _sut.CreateToken(deviceUid, userKeyField);

            // Assert
            token1.TokenValue.Should().NotBe(token2.TokenValue);
            var retrieved = _sut.GetToken(deviceUid);
            retrieved.Should().NotBeNull();
            retrieved!.TokenValue.Should().Be(token2.TokenValue);
        }

        [Fact]
        public void CreateToken_GeneratesUniqueTokens()
        {
            // Arrange & Act
            var token1 = _sut.CreateToken("device1", "user1");
            var token2 = _sut.CreateToken("device2", "user2");

            // Assert
            token1.TokenValue.Should().NotBe(token2.TokenValue);
        }

        #endregion

        #region ValidateToken Tests

        [Fact]
        public void ValidateToken_WithValidToken_ReturnsValid()
        {
            // Arrange
            var deviceUid = "test-device-123";
            var token = _sut.CreateToken(deviceUid, "user-key");

            // Act
            var result = _sut.ValidateToken(deviceUid, token.TokenValue);

            // Assert
            result.Should().Be(TokenValidationResult.Valid);
        }

        [Fact]
        public void ValidateToken_WithNullToken_ReturnsInvalidToken()
        {
            // Arrange
            var deviceUid = "test-device-123";
            _sut.CreateToken(deviceUid, "user-key");

            // Act
            var result = _sut.ValidateToken(deviceUid, null!);

            // Assert
            result.Should().Be(TokenValidationResult.InvalidToken);
        }

        [Fact]
        public void ValidateToken_WithEmptyToken_ReturnsInvalidToken()
        {
            // Arrange
            var deviceUid = "test-device-123";
            _sut.CreateToken(deviceUid, "user-key");

            // Act
            var result = _sut.ValidateToken(deviceUid, string.Empty);

            // Assert
            result.Should().Be(TokenValidationResult.InvalidToken);
        }

        [Fact]
        public void ValidateToken_WithWrongToken_ReturnsInvalidToken()
        {
            // Arrange
            var deviceUid = "test-device-123";
            _sut.CreateToken(deviceUid, "user-key");
            var wrongToken = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            // Act
            var result = _sut.ValidateToken(deviceUid, wrongToken);

            // Assert
            result.Should().Be(TokenValidationResult.InvalidToken);
        }

        [Fact]
        public void ValidateToken_WithNonExistentDevice_ReturnsInvalidToken()
        {
            // Arrange
            var deviceUid = "non-existent-device";
            var tokenValue = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            // Act
            var result = _sut.ValidateToken(deviceUid, tokenValue);

            // Assert
            result.Should().Be(TokenValidationResult.InvalidToken);
        }

        #endregion

        #region GetToken Tests

        [Fact]
        public void GetToken_WhenTokenExists_ReturnsToken()
        {
            // Arrange
            var deviceUid = "test-device-123";
            var created = _sut.CreateToken(deviceUid, "user-key");

            // Act
            var result = _sut.GetToken(deviceUid);

            // Assert
            result.Should().NotBeNull();
            result!.DeviceUid.Should().Be(deviceUid);
            result.TokenValue.Should().Be(created.TokenValue);
        }

        [Fact]
        public void GetToken_WhenTokenDoesNotExist_ReturnsNull()
        {
            // Arrange
            var deviceUid = "non-existent-device";

            // Act
            var result = _sut.GetToken(deviceUid);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region RemoveToken Tests

        [Fact]
        public void RemoveToken_WhenTokenExists_RemovesAndReturnsTrue()
        {
            // Arrange
            var deviceUid = "test-device-123";
            _sut.CreateToken(deviceUid, "user-key");

            // Act
            var result = _sut.RemoveToken(deviceUid);

            // Assert
            result.Should().BeTrue();
            _sut.GetToken(deviceUid).Should().BeNull();
        }

        [Fact]
        public void RemoveToken_WhenTokenDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var deviceUid = "non-existent-device";

            // Act
            var result = _sut.RemoveToken(deviceUid);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region RefreshToken Tests

        [Fact]
        public void RefreshToken_WithValidOldToken_ReturnsNewToken()
        {
            // Arrange
            var deviceUid = "test-device-123";
            var oldToken = _sut.CreateToken(deviceUid, "user-key");

            // Act
            var newToken = _sut.RefreshToken(deviceUid, oldToken.TokenValue);

            // Assert
            newToken.Should().NotBeNull();
            newToken!.DeviceUid.Should().Be(deviceUid);
            newToken.TokenValue.Should().NotBe(oldToken.TokenValue);
            newToken.UserKeyField.Should().Be(oldToken.UserKeyField);
        }

        [Fact]
        public void RefreshToken_WithWrongOldToken_ReturnsNull()
        {
            // Arrange
            var deviceUid = "test-device-123";
            _sut.CreateToken(deviceUid, "user-key");
            var wrongToken = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            // Act
            var result = _sut.RefreshToken(deviceUid, wrongToken);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void RefreshToken_WithNonExistentDevice_ReturnsNull()
        {
            // Arrange
            var deviceUid = "non-existent-device";
            var tokenValue = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            // Act
            var result = _sut.RefreshToken(deviceUid, tokenValue);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region LoadFromDatabaseAsync Tests

        [Fact]
        public async Task LoadFromDatabaseAsync_WithExistingTokens_LoadsIntoMemory()
        {
            // Arrange
            var entity = new DeviceTokenEntity
            {
                DeviceUid = "test-device-db",
                TokenValue = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                UserKeyField = "user-key-db",
                DateCreated = DateTime.UtcNow.AddDays(-1),
                Expiration = DateTime.UtcNow.AddDays(1)
            };
            _dbContext.DeviceTokens.Add(entity);
            await _dbContext.SaveChangesAsync();

            // Act
            await _sut.LoadFromDatabaseAsync();

            // Give async persistence a moment
            await Task.Delay(100);

            // Assert
            var loaded = _sut.GetToken("test-device-db");
            loaded.Should().NotBeNull();
            loaded!.DeviceUid.Should().Be("test-device-db");
            loaded.TokenValue.Should().Be(entity.TokenValue);
            loaded.UserKeyField.Should().Be("user-key-db");
        }

        [Fact]
        public async Task LoadFromDatabaseAsync_WithEmptyDatabase_CompletesSuccessfully()
        {
            // Act
            var act = async () => await _sut.LoadFromDatabaseAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Token Value Format Tests

        [Fact]
        public void CreateToken_GeneratesHexToken()
        {
            // Arrange & Act
            var token = _sut.CreateToken("device-123", "user-456");

            // Assert
            token.TokenValue.Should().MatchRegex("^[0-9a-f]{64}$");
        }

        #endregion
    }
}
