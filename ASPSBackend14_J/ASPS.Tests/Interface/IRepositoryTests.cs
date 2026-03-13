using Xunit;
using Moq;
using FluentAssertions;
using Interface.Repositories;
using Common.Models;
using System.Reflection;

namespace ASPS.Tests.Interface
{
    /// <summary>
    /// Tests for IRepository interface contract and behavior.
    /// </summary>
    public class IRepositoryTests
    {
        #region Interface Contract Tests

        [Fact]
        public void IRepository_HasGetByKeyAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IRepository<>).GetMethod("GetByKeyAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Match(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>));
        }

        [Fact]
        public void IRepository_HasGetAllAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IRepository<>).GetMethod("GetAllAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Match(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>));
        }

        [Fact]
        public void IRepository_HasAddAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IRepository<>).GetMethod("AddAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Match(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>));
        }

        [Fact]
        public void IRepository_HasUpdateAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IRepository<>).GetMethod("UpdateAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task));
        }

        [Fact]
        public void IRepository_HasDeleteAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IRepository<>).GetMethod("DeleteAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task));
        }

        [Fact]
        public void IRepository_HasExistsAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IRepository<>).GetMethod("ExistsAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<bool>));
        }

        [Fact]
        public void IRepository_IsGenericInterface()
        {
            // Arrange & Act
            var type = typeof(IRepository<>);

            // Assert
            type.IsInterface.Should().BeTrue();
            type.IsGenericType.Should().BeTrue();
            type.GetGenericArguments().Should().HaveCount(1);
        }

        [Fact]
        public void IRepository_HasClassConstraint()
        {
            // Arrange & Act
            var type = typeof(IRepository<>);
            var genericParam = type.GetGenericArguments()[0];

            // Assert
            genericParam.GenericParameterAttributes.Should().HaveFlag(GenericParameterAttributes.ReferenceTypeConstraint);
        }

        #endregion

        #region Mock Implementation Tests

        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        [Fact]
        public async Task MockRepository_GetByKeyAsync_CanBeMocked()
        {
            // Arrange
            var mockRepo = new Mock<IRepository<TestEntity>>();
            var testKey = new Key("test", "test-key");
            var testEntity = new TestEntity { Id = 1, Name = "Test" };
            
            mockRepo.Setup(r => r.GetByKeyAsync(testKey)).ReturnsAsync(testEntity);

            // Act
            var result = await mockRepo.Object.GetByKeyAsync(testKey);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Test");
            mockRepo.Verify(r => r.GetByKeyAsync(testKey), Times.Once);
        }

        [Fact]
        public async Task MockRepository_GetAllAsync_CanBeMocked()
        {
            // Arrange
            var mockRepo = new Mock<IRepository<TestEntity>>();
            var entities = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Entity1" },
                new TestEntity { Id = 2, Name = "Entity2" }
            };
            
            mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(entities);

            // Act
            var result = await mockRepo.Object.GetAllAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(e => e.Name == "Entity1");
        }

        [Fact]
        public async Task MockRepository_AddAsync_CanBeMocked()
        {
            // Arrange
            var mockRepo = new Mock<IRepository<TestEntity>>();
            var entity = new TestEntity { Id = 1, Name = "New Entity" };
            
            mockRepo.Setup(r => r.AddAsync(It.IsAny<TestEntity>())).ReturnsAsync(entity);

            // Act
            var result = await mockRepo.Object.AddAsync(entity);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("New Entity");
        }

        [Fact]
        public async Task MockRepository_UpdateAsync_CanBeMocked()
        {
            // Arrange
            var mockRepo = new Mock<IRepository<TestEntity>>();
            var entity = new TestEntity { Id = 1, Name = "Updated Entity" };
            
            mockRepo.Setup(r => r.UpdateAsync(It.IsAny<TestEntity>())).Returns(Task.CompletedTask);

            // Act
            await mockRepo.Object.UpdateAsync(entity);

            // Assert
            mockRepo.Verify(r => r.UpdateAsync(It.IsAny<TestEntity>()), Times.Once);
        }

        [Fact]
        public async Task MockRepository_DeleteAsync_CanBeMocked()
        {
            // Arrange
            var mockRepo = new Mock<IRepository<TestEntity>>();
            var key = new Key("test", "test-key");
            
            mockRepo.Setup(r => r.DeleteAsync(key)).Returns(Task.CompletedTask);

            // Act
            await mockRepo.Object.DeleteAsync(key);

            // Assert
            mockRepo.Verify(r => r.DeleteAsync(key), Times.Once);
        }

        [Fact]
        public async Task MockRepository_ExistsAsync_CanBeMocked()
        {
            // Arrange
            var mockRepo = new Mock<IRepository<TestEntity>>();
            var key = new Key("test", "test-key");
            
            mockRepo.Setup(r => r.ExistsAsync(key)).ReturnsAsync(true);

            // Act
            var result = await mockRepo.Object.ExistsAsync(key);

            // Assert
            result.Should().BeTrue();
        }

        #endregion
    }
}
