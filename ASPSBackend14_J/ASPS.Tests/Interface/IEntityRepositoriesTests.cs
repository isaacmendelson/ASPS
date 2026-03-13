using Xunit;
using Moq;
using FluentAssertions;
using Interface.Repositories;
using Common.Entities;
using Common.Models;

namespace ASPS.Tests.Interface
{
    /// <summary>
    /// Tests for entity-specific repository interfaces.
    /// Validates that specialized repositories extend IRepository and add domain-specific methods.
    /// Note: These are CONTRACT tests - we verify method signatures, not implementations.
    /// </summary>
    public class IEntityRepositoriesTests
    {
        #region IUserRepository Tests

        [Fact]
        public void IUserRepository_InheritsFromIRepository()
        {
            // Arrange & Act
            var type = typeof(IUserRepository);

            // Assert
            type.Should().BeAssignableTo<IRepository<User>>();
        }

        [Fact]
        public void IUserRepository_HasGetByKeycloakIdAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserRepository).GetMethod("GetByKeycloakIdAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<User?>));
        }

        [Fact]
        public void IUserRepository_HasGetUserWithDetailsAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserRepository).GetMethod("GetUserWithDetailsAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<User?>));
        }

        [Fact]
        public void IUserRepository_HasGetActiveUsersAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserRepository).GetMethod("GetActiveUsersAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<User>>));
        }

        #endregion

        #region IUserDeviceRepository Tests

        [Fact]
        public void IUserDeviceRepository_InheritsFromIRepository()
        {
            // Arrange & Act
            var type = typeof(IUserDeviceRepository);

            // Assert
            type.Should().BeAssignableTo<IRepository<UserDevice>>();
        }

        [Fact]
        public void IUserDeviceRepository_HasGetByUserKeyAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserDeviceRepository).GetMethod("GetByUserKeyAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<UserDevice>>));
        }

        [Fact]
        public void IUserDeviceRepository_HasGetByDeviceUidAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserDeviceRepository).GetMethod("GetByDeviceUidAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<UserDevice?>));
        }

        [Fact]
        public void IUserDeviceRepository_HasGetMonitoredDevicesAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserDeviceRepository).GetMethod("GetMonitoredDevicesAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<UserDevice>>));
        }

        #endregion

        #region IUserAccountRepository Tests

        [Fact]
        public void IUserAccountRepository_InheritsFromIRepository()
        {
            // Arrange & Act
            var type = typeof(IUserAccountRepository);

            // Assert
            type.Should().BeAssignableTo<IRepository<UserAccount>>();
        }

        [Fact]
        public void IUserAccountRepository_HasGetByUserKeyAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserAccountRepository).GetMethod("GetByUserKeyAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<UserAccount>>));
        }

        [Fact]
        public void IUserAccountRepository_HasGetByUserNameAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IUserAccountRepository).GetMethod("GetByUserNameAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<UserAccount?>));
        }

        #endregion

        #region IDeviceAlertRepository Tests

        [Fact]
        public void IDeviceAlertRepository_InheritsFromIRepository()
        {
            // Arrange & Act
            var type = typeof(IDeviceAlertRepository);

            // Assert
            type.Should().BeAssignableTo<IRepository<DeviceAlertEntity>>();
        }

        [Fact]
        public void IDeviceAlertRepository_HasGetAlertsByDeviceUidAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IDeviceAlertRepository).GetMethod("GetAlertsByDeviceUidAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<DeviceAlertEntity>>));
        }

        [Fact]
        public void IDeviceAlertRepository_HasGetAlertsByUserKeyAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IDeviceAlertRepository).GetMethod("GetAlertsByUserKeyAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<DeviceAlertEntity>>));
        }

        [Fact]
        public void IDeviceAlertRepository_HasGetRecentAlertsAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IDeviceAlertRepository).GetMethod("GetRecentAlertsAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<DeviceAlertEntity>>));
        }

        #endregion

        #region IAnalysisResultRepository Tests

        [Fact]
        public void IAnalysisResultRepository_InheritsFromIRepository()
        {
            // Arrange & Act
            var type = typeof(IAnalysisResultRepository);

            // Assert
            type.Should().BeAssignableTo<IRepository<AnalysisResultContainer>>();
        }

        [Fact]
        public void IAnalysisResultRepository_HasGetByUserKeyAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IAnalysisResultRepository).GetMethod("GetByUserKeyAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<AnalysisResultContainer>>));
        }

        [Fact]
        public void IAnalysisResultRepository_HasGetLatestAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IAnalysisResultRepository).GetMethod("GetLatestAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<AnalysisResultContainer?>));
        }

        #endregion

        #region IAlertFlagRepository Tests

        [Fact]
        public void IAlertFlagRepository_HasAddAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IAlertFlagRepository).GetMethod("AddAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<AlertFlag>));
        }

        [Fact]
        public void IAlertFlagRepository_HasUpdateAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IAlertFlagRepository).GetMethod("UpdateAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task));
        }

        [Fact]
        public void IAlertFlagRepository_HasGetOpenFlagsByUserAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IAlertFlagRepository).GetMethod("GetOpenFlagsByUserAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<AlertFlag>>));
        }

        [Fact]
        public void IAlertFlagRepository_HasCloseFlagMethod()
        {
            // Arrange & Act
            var method = typeof(IAlertFlagRepository).GetMethod("CloseFlag");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task));
        }

        #endregion

        #region ISafeDomainRepository Tests

        [Fact]
        public void ISafeDomainRepository_HasGetAllActiveAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(ISafeDomainRepository).GetMethod("GetAllActiveAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<SafeDomain>>));
        }

        [Fact]
        public void ISafeDomainRepository_HasIsSafeDomainAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(ISafeDomainRepository).GetMethod("IsSafeDomainAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<bool>));
        }

        #endregion

        #region IKnownPhishingWebsiteRepository Tests

        [Fact]
        public void IKnownPhishingWebsiteRepository_HasGetByIdAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IKnownPhishingWebsiteRepository).GetMethod("GetByIdAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<KnownPhishingWebsite?>));
        }

        [Fact]
        public void IKnownPhishingWebsiteRepository_HasIsPhishingUrlAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IKnownPhishingWebsiteRepository).GetMethod("IsPhishingUrlAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<bool>));
        }

        [Fact]
        public void IKnownPhishingWebsiteRepository_HasIsPhishingDomainAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IKnownPhishingWebsiteRepository).GetMethod("IsPhishingDomainAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<bool>));
        }

        [Fact]
        public void IKnownPhishingWebsiteRepository_HasAddAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IKnownPhishingWebsiteRepository).GetMethod("AddAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<int>));
        }

        [Fact]
        public void IKnownPhishingWebsiteRepository_HasAddRangeAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(IKnownPhishingWebsiteRepository).GetMethod("AddRangeAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<int>));
        }

        #endregion

        #region ITrackUrlAlertRepository Tests

        [Fact]
        public void ITrackUrlAlertRepository_InheritsFromIRepository()
        {
            // Arrange & Act
            var type = typeof(ITrackUrlAlertRepository);

            // Assert
            type.Should().BeAssignableTo<IRepository<TrackUrlAlertEntity>>();
        }

        [Fact]
        public void ITrackUrlAlertRepository_HasGetAlertsByUrlAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(ITrackUrlAlertRepository).GetMethod("GetAlertsByUrlAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<TrackUrlAlertEntity>>));
        }

        [Fact]
        public void ITrackUrlAlertRepository_HasGetAlertsByUserKeyAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(ITrackUrlAlertRepository).GetMethod("GetAlertsByUserKeyAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<TrackUrlAlertEntity>>));
        }

        #endregion

        #region ITrackedDomainRepository Tests

        [Fact]
        public void ITrackedDomainRepository_HasGetByIdAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(ITrackedDomainRepository).GetMethod("GetByIdAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<TrackedDomain?>));
        }

        [Fact]
        public void ITrackedDomainRepository_HasGetAllActiveAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(ITrackedDomainRepository).GetMethod("GetAllActiveAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<IEnumerable<TrackedDomain>>));
        }

        [Fact]
        public void ITrackedDomainRepository_HasIsTrackedDomainAsyncMethod()
        {
            // Arrange & Act
            var method = typeof(ITrackedDomainRepository).GetMethod("IsTrackedDomainAsync");

            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<bool>));
        }

        #endregion
    }
}
