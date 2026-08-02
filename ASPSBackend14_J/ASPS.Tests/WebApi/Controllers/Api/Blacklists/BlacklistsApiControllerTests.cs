using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Business.Commands;
using Business.Queries;
using Common.Entities;
using Common.Models;
using WebApi.Controllers.Api.Blacklists;
using WebApi.Services;

namespace ASPS.Tests.WebApi.Controllers.Api.Blacklists;

/// <summary>
/// Unit tests for the Blacklists API controllers.
/// JIRA: ASPS-648
///
/// Note: [Authorize(Roles = "Admin")] is not enforced in unit tests — controllers
/// are instantiated directly without the HTTP pipeline (matching SystemControllerTests).
/// </summary>
public class BlacklistsApiControllerTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static Mock<ICQRSClient> MockCqrsClient() => new Mock<ICQRSClient>();

    private static Mock<ILogger<T>> MockLogger<T>() => new Mock<ILogger<T>>();

    // =========================================================================
    // PhishingWebsitesController
    // =========================================================================

    public class PhishingWebsitesControllerTests
    {
        private readonly Mock<ICQRSClient> _cqrs = MockCqrsClient();
        private readonly PhishingWebsitesController _sut;

        public PhishingWebsitesControllerTests()
        {
            _sut = new PhishingWebsitesController(_cqrs.Object, MockLogger<PhishingWebsitesController>().Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOk_WithPagedResult()
        {
            // Arrange
            var website = new KnownPhishingWebsite("http://evil.example.com", "Manual");
            _cqrs.Setup(c => c.SendQueryAsync<GetAllPhishingWebsitesQueryResult>(It.IsAny<GetAllPhishingWebsitesQuery>()))
                .ReturnsAsync(new GetAllPhishingWebsitesQueryResult
                {
                    Success = true,
                    PhishingWebsites = new List<KnownPhishingWebsite> { website },
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            var result = await _sut.GetAll(new PagedRequest());

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var paged = ok.Value.Should().BeOfType<PagedResult<PhishingWebsiteDto>>().Subject;
            paged.Items.Should().HaveCount(1);
            paged.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task GetAll_CallsNormalize_ClampingPage()
        {
            // Arrange — page = 0 should be normalized to 1
            GetAllPhishingWebsitesQuery? capturedQuery = null;
            _cqrs.Setup(c => c.SendQueryAsync<GetAllPhishingWebsitesQueryResult>(It.IsAny<GetAllPhishingWebsitesQuery>()))
                .Callback<global::Common.Messaging.Query>(q => capturedQuery = q as GetAllPhishingWebsitesQuery)
                .ReturnsAsync(new GetAllPhishingWebsitesQueryResult
                {
                    Success = true,
                    PhishingWebsites = new List<KnownPhishingWebsite>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            await _sut.GetAll(new PagedRequest { Page = 0 });

            // Assert
            capturedQuery!.Page.Should().Be(1, "Page 0 must be normalized to 1");
        }

        [Fact]
        public async Task GetAll_WhenQueryFails_ReturnsBadRequest()
        {
            // Arrange
            _cqrs.Setup(c => c.SendQueryAsync<GetAllPhishingWebsitesQueryResult>(It.IsAny<GetAllPhishingWebsitesQuery>()))
                .ReturnsAsync(new GetAllPhishingWebsitesQueryResult { Success = false, Message = "Backend error" });

            // Act
            var result = await _sut.GetAll(new PagedRequest());

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetAll_MapsIsActiveFromEntity()
        {
            // Arrange — a website with no DateDeleted should map as IsActive = true
            var website = new KnownPhishingWebsite("http://phish.example.com", "Test");
            _cqrs.Setup(c => c.SendQueryAsync<GetAllPhishingWebsitesQueryResult>(It.IsAny<GetAllPhishingWebsitesQuery>()))
                .ReturnsAsync(new GetAllPhishingWebsitesQueryResult
                {
                    Success = true,
                    PhishingWebsites = new List<KnownPhishingWebsite> { website },
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            var result = await _sut.GetAll(new PagedRequest());

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var paged = ok.Value.Should().BeOfType<PagedResult<PhishingWebsiteDto>>().Subject;
            paged.Items[0].IsActive.Should().BeTrue();
        }
    }

    // =========================================================================
    // PhoneNumbersController
    // =========================================================================

    public class PhoneNumbersControllerTests
    {
        private readonly Mock<ICQRSClient> _cqrs = MockCqrsClient();
        private readonly PhoneNumbersController _sut;

        public PhoneNumbersControllerTests()
        {
            _sut = new PhoneNumbersController(_cqrs.Object, MockLogger<PhoneNumbersController>().Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOk_WithPagedResult()
        {
            // Arrange
            var phone = new BlacklistedPhoneNumber { Id = 1, PhoneNumber = "+972501234567", DateCreated = DateTime.UtcNow };
            _cqrs.Setup(c => c.SendQueryAsync<GetAllBlacklistedPhoneNumbersQueryResult>(It.IsAny<GetAllBlacklistedPhoneNumbersQuery>()))
                .ReturnsAsync(new GetAllBlacklistedPhoneNumbersQueryResult
                {
                    Success = true,
                    PhoneNumbers = new List<BlacklistedPhoneNumber> { phone },
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            var result = await _sut.GetAll(new PagedRequest());

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var paged = ok.Value.Should().BeOfType<PagedResult<PhoneNumberDto>>().Subject;
            paged.Items.Should().HaveCount(1);
            paged.Items[0].PhoneNumber.Should().Be("+972501234567");
        }

        [Fact]
        public async Task GetAll_CallsNormalize_ClampingPageSize()
        {
            // Arrange — pageSize > 100 should be clamped to 100
            GetAllBlacklistedPhoneNumbersQuery? captured = null;
            _cqrs.Setup(c => c.SendQueryAsync<GetAllBlacklistedPhoneNumbersQueryResult>(It.IsAny<GetAllBlacklistedPhoneNumbersQuery>()))
                .Callback<global::Common.Messaging.Query>(q => captured = q as GetAllBlacklistedPhoneNumbersQuery)
                .ReturnsAsync(new GetAllBlacklistedPhoneNumbersQueryResult
                {
                    Success = true,
                    PhoneNumbers = new List<BlacklistedPhoneNumber>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = 100,
                });

            // Act
            await _sut.GetAll(new PagedRequest { PageSize = 200 });

            // Assert
            captured!.PageSize.Should().Be(100, "PageSize 200 must be clamped to 100");
        }

        [Fact]
        public async Task GetAll_WhenQueryFails_ReturnsBadRequest()
        {
            // Arrange
            _cqrs.Setup(c => c.SendQueryAsync<GetAllBlacklistedPhoneNumbersQueryResult>(It.IsAny<GetAllBlacklistedPhoneNumbersQuery>()))
                .ReturnsAsync(new GetAllBlacklistedPhoneNumbersQueryResult { Success = false, Message = "DB error" });

            // Act
            var result = await _sut.GetAll(new PagedRequest());

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }

    // =========================================================================
    // BankWebsitesController
    // =========================================================================

    public class BankWebsitesControllerTests
    {
        private readonly Mock<ICQRSClient> _cqrs = MockCqrsClient();
        private readonly BankWebsitesController _sut;

        public BankWebsitesControllerTests()
        {
            _sut = new BankWebsitesController(_cqrs.Object, MockLogger<BankWebsitesController>().Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOk_WithPagedResult()
        {
            // Arrange
            var bank = new BankWebsite { Id = 1, Domain = "bankleumi.co.il", BankName = "Bank Leumi", IsActive = true, DateCreated = DateTime.UtcNow };
            _cqrs.Setup(c => c.SendQueryAsync<GetAllBankWebsitesQueryResult>(It.IsAny<GetAllBankWebsitesQuery>()))
                .ReturnsAsync(new GetAllBankWebsitesQueryResult
                {
                    Success = true,
                    BankWebsites = new List<BankWebsite> { bank },
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            var result = await _sut.GetAll(new PagedRequest(), isActive: null);

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var paged = ok.Value.Should().BeOfType<PagedResult<BankWebsiteDto>>().Subject;
            paged.Items.Should().HaveCount(1);
            paged.Items[0].BankName.Should().Be("Bank Leumi");
        }

        [Fact]
        public async Task GetAll_PassesIsActiveFilter_ToQuery()
        {
            // Arrange
            GetAllBankWebsitesQuery? captured = null;
            _cqrs.Setup(c => c.SendQueryAsync<GetAllBankWebsitesQueryResult>(It.IsAny<GetAllBankWebsitesQuery>()))
                .Callback<global::Common.Messaging.Query>(q => captured = q as GetAllBankWebsitesQuery)
                .ReturnsAsync(new GetAllBankWebsitesQueryResult
                {
                    Success = true,
                    BankWebsites = new List<BankWebsite>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            await _sut.GetAll(new PagedRequest(), isActive: true);

            // Assert
            captured!.IsActive.Should().Be(true);
        }

        [Fact]
        public async Task GetAll_WhenQueryFails_ReturnsBadRequest()
        {
            // Arrange
            _cqrs.Setup(c => c.SendQueryAsync<GetAllBankWebsitesQueryResult>(It.IsAny<GetAllBankWebsitesQuery>()))
                .ReturnsAsync(new GetAllBankWebsitesQueryResult { Success = false, Message = "Error" });

            // Act
            var result = await _sut.GetAll(new PagedRequest(), isActive: null);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }

    // =========================================================================
    // WebsiteCategoriesController
    // =========================================================================

    public class WebsiteCategoriesControllerTests
    {
        private readonly Mock<ICQRSClient> _cqrs = MockCqrsClient();
        private readonly WebsiteCategoriesController _sut;

        public WebsiteCategoriesControllerTests()
        {
            _sut = new WebsiteCategoriesController(_cqrs.Object, MockLogger<WebsiteCategoriesController>().Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOk_WithPagedResult()
        {
            // Arrange
            var cat = new WebsiteCategory("Gaming", string.Empty, "test");
            cat.KeyField = "cat-key-1";
            _cqrs.Setup(c => c.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
                .ReturnsAsync(new GetAllWebsiteCategoriesQueryResult
                {
                    Success = true,
                    Categories = new List<WebsiteCategory> { cat },
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            var result = await _sut.GetAll(new PagedRequest());

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var paged = ok.Value.Should().BeOfType<PagedResult<WebsiteCategoryDto>>().Subject;
            paged.Items.Should().HaveCount(1);
            paged.Items[0].Name.Should().Be("Gaming");
        }

        [Fact]
        public async Task GetParents_ReturnsOk_WithList()
        {
            // Arrange
            var parent = new WebsiteCategory("Entertainment", string.Empty, null);
            parent.KeyField = "parent-1";
            _cqrs.Setup(c => c.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
                .ReturnsAsync(new GetParentCategoriesQueryResult
                {
                    Success = true,
                    Parents = new List<WebsiteCategory> { parent },
                });

            // Act
            var result = await _sut.GetParents();

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var list = ok.Value.Should().BeAssignableTo<IEnumerable<WebsiteCategoryDto>>().Subject;
            list.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetByName_WhenFound_ReturnsOk()
        {
            // Arrange
            var cat = new WebsiteCategory("News", string.Empty, null);
            cat.KeyField = "news-key";
            _cqrs.Setup(c => c.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(It.IsAny<GetWebsiteCategoryByNameQuery>()))
                .ReturnsAsync(new GetWebsiteCategoryByNameQueryResult
                {
                    Success = true,
                    Category = cat,
                });

            // Act
            var result = await _sut.GetByName("News");

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeOfType<WebsiteCategoryDto>()
                .Which.Name.Should().Be("News");
        }

        [Fact]
        public async Task GetByName_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _cqrs.Setup(c => c.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(It.IsAny<GetWebsiteCategoryByNameQuery>()))
                .ReturnsAsync(new GetWebsiteCategoryByNameQueryResult { Success = false, Category = null });

            // Act
            var result = await _sut.GetByName("DoesNotExist");

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Create_WithValidRequest_ReturnsOk()
        {
            // Arrange
            _cqrs.Setup(c => c.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()))
                .ReturnsAsync(new CreateWebsiteCategoryCommandResult
                {
                    Success = true,
                    Message = "Website category created successfully.",
                    CategoryId = 42,
                });

            // Act
            var result = await _sut.Create(new CreateWebsiteCategoryRequest { Name = "Finance" });

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Create_WithEmptyName_ReturnsBadRequest()
        {
            // Act
            var result = await _sut.Create(new CreateWebsiteCategoryRequest { Name = "   " });

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            _cqrs.Verify(c => c.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()), Times.Never);
        }

        [Fact]
        public async Task Create_WhenCommandFails_ReturnsBadRequest()
        {
            // Arrange
            _cqrs.Setup(c => c.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()))
                .ReturnsAsync(new CreateWebsiteCategoryCommandResult
                {
                    Success = false,
                    Message = "A category with the name 'Finance' already exists.",
                });

            // Act
            var result = await _sut.Create(new CreateWebsiteCategoryRequest { Name = "Finance" });

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Update_WithValidRequest_ReturnsOk()
        {
            // Arrange
            _cqrs.Setup(c => c.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()))
                .ReturnsAsync(new UpdateWebsiteCategoryCommandResult
                {
                    Success = true,
                    Message = "Website category updated successfully.",
                });

            // Act
            var result = await _sut.Update("Finance", new UpdateWebsiteCategoryRequest { ParentId = "parent-1" });

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Update_PassesKeyAsName_ToCommand()
        {
            // Arrange
            UpdateWebsiteCategoryCommand? captured = null;
            _cqrs.Setup(c => c.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()))
                .Callback<global::Common.Messaging.Command>(cmd => captured = cmd as UpdateWebsiteCategoryCommand)
                .ReturnsAsync(new UpdateWebsiteCategoryCommandResult { Success = true });

            // Act
            await _sut.Update("Sports", new UpdateWebsiteCategoryRequest());

            // Assert
            captured!.Name.Should().Be("Sports");
        }
    }

    // =========================================================================
    // TrackedDomainsController
    // =========================================================================

    public class TrackedDomainsControllerTests
    {
        private readonly Mock<ICQRSClient> _cqrs = MockCqrsClient();
        private readonly TrackedDomainsController _sut;

        public TrackedDomainsControllerTests()
        {
            _sut = new TrackedDomainsController(_cqrs.Object, MockLogger<TrackedDomainsController>().Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOk_WithPagedResult()
        {
            // Arrange
            var domain = new TrackedDomain("tracker.example.com", "Analytics");
            _cqrs.Setup(c => c.SendQueryAsync<GetAllTrackedDomainsQueryResult>(It.IsAny<GetAllTrackedDomainsQuery>()))
                .ReturnsAsync(new GetAllTrackedDomainsQueryResult
                {
                    Success = true,
                    TrackedDomains = new List<TrackedDomain> { domain },
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 25,
                });

            // Act
            var result = await _sut.GetAll(new PagedRequest());

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var paged = ok.Value.Should().BeOfType<PagedResult<TrackedDomainDto>>().Subject;
            paged.Items.Should().HaveCount(1);
            paged.Items[0].Domain.Should().Be("tracker.example.com");
        }

        [Fact]
        public async Task Create_WithValidRequest_ReturnsOk()
        {
            // Arrange
            _cqrs.Setup(c => c.SendCommandAsync<AddTrackedDomainCommandResult>(It.IsAny<AddTrackedDomainCommand>()))
                .ReturnsAsync(new AddTrackedDomainCommandResult
                {
                    Success = true,
                    Message = "Tracked domain added and synced to devices.",
                    TrackedDomainId = 99,
                });

            // Act
            var result = await _sut.Create(new CreateTrackedDomainRequest
            {
                Domain = "evil-tracker.com",
                Category = "Advertising",
            });

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Create_WithEmptyDomain_ReturnsBadRequest()
        {
            // Act
            var result = await _sut.Create(new CreateTrackedDomainRequest { Domain = string.Empty });

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            _cqrs.Verify(c => c.SendCommandAsync<AddTrackedDomainCommandResult>(It.IsAny<AddTrackedDomainCommand>()), Times.Never);
        }

        [Fact]
        public async Task Create_SetsNotifyOnlyFalse()
        {
            // Arrange
            AddTrackedDomainCommand? captured = null;
            _cqrs.Setup(c => c.SendCommandAsync<AddTrackedDomainCommandResult>(It.IsAny<AddTrackedDomainCommand>()))
                .Callback<global::Common.Messaging.Command>(cmd => captured = cmd as AddTrackedDomainCommand)
                .ReturnsAsync(new AddTrackedDomainCommandResult { Success = true });

            // Act
            await _sut.Create(new CreateTrackedDomainRequest { Domain = "example.com", Category = "Test" });

            // Assert
            captured!.NotifyOnly.Should().BeFalse("Create must persist the row");
        }

        [Fact]
        public async Task Update_WithValidId_ReturnsOk()
        {
            // Arrange
            _cqrs.Setup(c => c.SendCommandAsync<UpdateTrackedDomainCommandResult>(It.IsAny<UpdateTrackedDomainCommand>()))
                .ReturnsAsync(new UpdateTrackedDomainCommandResult
                {
                    Success = true,
                    Message = "Tracked domain updated and re-synced to devices.",
                    TrackedDomainId = 5,
                });

            // Act
            var result = await _sut.Update(5, new UpdateTrackedDomainRequest { IsActive = true });

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Update_PassesCorrectId_ToCommand()
        {
            // Arrange
            UpdateTrackedDomainCommand? captured = null;
            _cqrs.Setup(c => c.SendCommandAsync<UpdateTrackedDomainCommandResult>(It.IsAny<UpdateTrackedDomainCommand>()))
                .Callback<global::Common.Messaging.Command>(cmd => captured = cmd as UpdateTrackedDomainCommand)
                .ReturnsAsync(new UpdateTrackedDomainCommandResult { Success = true });

            // Act
            await _sut.Update(42, new UpdateTrackedDomainRequest());

            // Assert
            captured!.Id.Should().Be(42);
        }

        [Fact]
        public async Task Delete_WithValidId_ReturnsOk()
        {
            // Arrange
            _cqrs.Setup(c => c.SendCommandAsync<DeleteTrackedDomainCommandResult>(It.IsAny<DeleteTrackedDomainCommand>()))
                .ReturnsAsync(new DeleteTrackedDomainCommandResult
                {
                    Success = true,
                    Message = "Tracked domain deleted.",
                });

            // Act
            var result = await _sut.Delete(7, reason: null);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Delete_WhenCommandFails_ReturnsBadRequest()
        {
            // Arrange
            _cqrs.Setup(c => c.SendCommandAsync<DeleteTrackedDomainCommandResult>(It.IsAny<DeleteTrackedDomainCommand>()))
                .ReturnsAsync(new DeleteTrackedDomainCommandResult
                {
                    Success = false,
                    Message = "Tracked domain ID 999 not found.",
                });

            // Act
            var result = await _sut.Delete(999, reason: null);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task NotifyUser_WithEmptyUserKey_ReturnsBadRequest()
        {
            // Act
            var result = await _sut.NotifyUser(1, new NotifyUserRequest { UserKeyField = string.Empty });

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task NotifyAll_WithNoIdAndNoDomain_ReturnsBadRequest()
        {
            // Act
            var result = await _sut.NotifyAll(id: null, request: new NotifyAllRequest());

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task NotifyAll_WithDomainInBody_SetsNotifyOnlyAndNotifyAll()
        {
            // Arrange
            AddTrackedDomainCommand? captured = null;
            _cqrs.Setup(c => c.SendCommandAsync<AddTrackedDomainCommandResult>(It.IsAny<AddTrackedDomainCommand>()))
                .Callback<global::Common.Messaging.Command>(cmd => captured = cmd as AddTrackedDomainCommand)
                .ReturnsAsync(new AddTrackedDomainCommandResult { Success = true });

            // Act
            await _sut.NotifyAll(id: null, request: new NotifyAllRequest
            {
                Domain = "tracker.evil.com",
                Category = "Advertising",
                TrackMode = 1,
            });

            // Assert
            captured!.NotifyOnly.Should().BeTrue();
            captured.NotifyAllUsers.Should().BeTrue();
            captured.Domain.Should().Be("tracker.evil.com");
        }
    }

    // =========================================================================
    // DTO tests — pure construction, no CQRS dependency
    // =========================================================================

    public class DtoTests
    {
        [Fact]
        public void PhishingWebsiteDto_DefaultsAreEmpty()
        {
            var dto = new PhishingWebsiteDto();
            dto.Url.Should().BeEmpty();
            dto.Domain.Should().BeEmpty();
            dto.Source.Should().BeNull();
        }

        [Fact]
        public void PhoneNumberDto_DefaultsAreEmpty()
        {
            var dto = new PhoneNumberDto();
            dto.PhoneNumber.Should().BeEmpty();
            dto.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void BankWebsiteDto_DefaultsAreEmpty()
        {
            var dto = new BankWebsiteDto();
            dto.Domain.Should().BeEmpty();
            dto.BankName.Should().BeEmpty();
            dto.IsActive.Should().BeFalse(); // unset bool defaults to false
        }

        [Fact]
        public void WebsiteCategoryDto_DefaultsAreEmpty()
        {
            var dto = new WebsiteCategoryDto();
            dto.Key.Should().BeEmpty();
            dto.Name.Should().BeEmpty();
            dto.ParentId.Should().BeNull();
            dto.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void TrackedDomainDto_DefaultsAreEmpty()
        {
            var dto = new TrackedDomainDto();
            dto.Domain.Should().BeEmpty();
            dto.Category.Should().BeEmpty();
            dto.IsActive.Should().BeFalse();
        }

        [Fact]
        public void CreateTrackedDomainRequest_DefaultTrackMode_IsSurf()
        {
            var req = new CreateTrackedDomainRequest();
            req.TrackMode.Should().Be(1, "TrackMode 1 = Surf (default)");
        }

        [Fact]
        public void UpdateTrackedDomainRequest_DefaultIsActive_IsTrue()
        {
            var req = new UpdateTrackedDomainRequest();
            req.IsActive.Should().BeTrue();
        }
    }
}
