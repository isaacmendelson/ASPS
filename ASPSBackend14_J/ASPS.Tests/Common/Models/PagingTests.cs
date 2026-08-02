using Common.Models;
using FluentAssertions;

namespace ASPS.Tests.Common.Models;

public class PagedRequestNormalizeTests
{
    [Fact]
    public void Normalize_DefaultValues_Unchanged()
    {
        var request = new PagedRequest(); // Page=1, PageSize=25, SortDirection="asc"
        request.Normalize();
        request.Page.Should().Be(1);
        request.PageSize.Should().Be(25);
        request.SortDirection.Should().Be("asc");
    }

    [Fact]
    public void Normalize_PageLessThanOne_SetsPageToOne()
    {
        var request = new PagedRequest { Page = 0 };
        request.Normalize();
        request.Page.Should().Be(1);
    }

    [Fact]
    public void Normalize_NegativePage_SetsPageToOne()
    {
        var request = new PagedRequest { Page = -5 };
        request.Normalize();
        request.Page.Should().Be(1);
    }

    [Fact]
    public void Normalize_PageSizeLessThanOne_SetsPageSizeTo25()
    {
        var request = new PagedRequest { PageSize = 0 };
        request.Normalize();
        request.PageSize.Should().Be(25);
    }

    [Fact]
    public void Normalize_NegativePageSize_SetsPageSizeTo25()
    {
        var request = new PagedRequest { PageSize = -1 };
        request.Normalize();
        request.PageSize.Should().Be(25);
    }

    [Fact]
    public void Normalize_PageSizeExceedsMax_ClampsTo100()
    {
        var request = new PagedRequest { PageSize = 500 };
        request.Normalize();
        request.PageSize.Should().Be(100);
    }

    [Fact]
    public void Normalize_PageSizeAtMax_Unchanged()
    {
        var request = new PagedRequest { PageSize = 100 };
        request.Normalize();
        request.PageSize.Should().Be(100);
    }

    [Fact]
    public void Normalize_SortDirectionDesc_Unchanged()
    {
        var request = new PagedRequest { SortDirection = "desc" };
        request.Normalize();
        request.SortDirection.Should().Be("desc");
    }

    [Fact]
    public void Normalize_InvalidSortDirection_SetsToAsc()
    {
        var request = new PagedRequest { SortDirection = "INVALID" };
        request.Normalize();
        request.SortDirection.Should().Be("asc");
    }

    [Fact]
    public void Normalize_EmptySortDirection_SetsToAsc()
    {
        var request = new PagedRequest { SortDirection = "" };
        request.Normalize();
        request.SortDirection.Should().Be("asc");
    }

    [Fact]
    public void Normalize_SortDirectionCaseSensitive_InvalidCaseSetsToAsc()
    {
        // "DESC" (uppercase) is not "desc" — per spec, only lowercase "asc"/"desc" are valid
        var request = new PagedRequest { SortDirection = "DESC" };
        request.Normalize();
        request.SortDirection.Should().Be("asc");
    }

    [Fact]
    public void Normalize_ValidPageAndPageSize_Unchanged()
    {
        var request = new PagedRequest { Page = 5, PageSize = 50 };
        request.Normalize();
        request.Page.Should().Be(5);
        request.PageSize.Should().Be(50);
    }

    [Fact]
    public void Normalize_SearchPreserved()
    {
        var request = new PagedRequest { Search = "hello" };
        request.Normalize();
        request.Search.Should().Be("hello");
    }

    [Fact]
    public void Normalize_NullSearchPreserved()
    {
        var request = new PagedRequest { Search = null };
        request.Normalize();
        request.Search.Should().BeNull();
    }
}

public class PagedResultTotalPagesTests
{
    [Fact]
    public void TotalPages_ExactDivision_ReturnsQuotient()
    {
        var result = new PagedResult<string> { TotalCount = 100, PageSize = 25 };
        result.TotalPages.Should().Be(4);
    }

    [Fact]
    public void TotalPages_Remainder_CeilsUp()
    {
        var result = new PagedResult<string> { TotalCount = 101, PageSize = 25 };
        result.TotalPages.Should().Be(5);
    }

    [Fact]
    public void TotalPages_ZeroTotal_ReturnsZero()
    {
        var result = new PagedResult<string> { TotalCount = 0, PageSize = 25 };
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void TotalPages_OnePage_ReturnsOne()
    {
        var result = new PagedResult<string> { TotalCount = 1, PageSize = 25 };
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public void TotalPages_OneItemAtPageSizeBoundary_ReturnsOne()
    {
        var result = new PagedResult<string> { TotalCount = 25, PageSize = 25 };
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public void TotalPages_OneOverPageSizeBoundary_ReturnsTwo()
    {
        var result = new PagedResult<string> { TotalCount = 26, PageSize = 25 };
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public void TotalPages_SingleItemPageSize_EqualsTotalCount()
    {
        var result = new PagedResult<string> { TotalCount = 7, PageSize = 1 };
        result.TotalPages.Should().Be(7);
    }

    [Fact]
    public void TotalPages_DefaultItemsIsEmptyList()
    {
        var result = new PagedResult<string>();
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }
}
