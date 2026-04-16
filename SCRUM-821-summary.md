# SCRUM-821: Replace WebsiteType enum with WebsiteCategoryViews

## Summary
Successfully replaced WebsiteType enum with WebsiteCategoryViews infrastructure throughout the codebase.

## Changes Made

### 1. Marked WebsiteType enum as Obsolete
**File:** `Common/Enums/WebsiteType.cs`
- Added `[Obsolete("Use WebsiteCategoryViews from ASView instead")]` attribute
- Kept enum definition for backward compatibility

### 2. Updated Purpose Class
**File:** `Business/RealtimeAnalysis/UserDomain/UrlAnalysisViewModels.cs`
- **Before:** `public WebsiteType Category { get; set; } = WebsiteType.Unknown;`
- **After:** `public string CategoryName { get; set; } = "unknown";`
- Added documentation referencing SCRUM-821

### 3. Updated WebsiteTypeIndicator
**File:** `Business/RealtimeAnalysis/Indicators/WebsiteTypeIndicator.cs`
- Constructor now accepts `string categoryName` instead of `WebsiteType websiteType`
- Property changed from `WebsiteType WebsiteType` to `string CategoryName`
- Property changed from `WebsiteType TypedValue` to `string TypedValue`
- SetValue method now accepts `string value` instead of `WebsiteType value`
- Added documentation with JIRA reference

### 4. Updated IndicatorFactory
**File:** `Business/RealtimeAnalysis/IndicatorFactory.cs`
- **Before:** `if (vm.Purpose is not null && vm.Purpose.Category != WebsiteType.Unknown)`
- **After:** `if (vm.Purpose is not null && !string.IsNullOrEmpty(vm.Purpose.CategoryName) && vm.Purpose.CategoryName != "unknown")`
- Constructor call updated to pass `vm.Purpose.CategoryName`

### 5. Updated Commented Code
**File:** `Business/RealtimeAnalysis/UserDomain/UDUserAnalyzer.cs`
- Updated 2 commented lines (lines 238 and 319)
- **Before:** `u.Purpose?.Category == WebsiteType.Banking`
- **After:** `u.Purpose?.CategoryName == "banking"`

### 6. Updated Unit Tests
**File:** `ASPS.Tests/IndicatorFactoryTests.cs`
- Updated 3 test methods to use string category names:
  - `CreateIndicators_WithWebsiteType_ReturnsWebsiteTypeIndicator`
  - `CreateIndicators_WithUnknownWebsiteType_DoesNotCreateWebsiteTypeIndicator`
  - `CreateIndicators_WithMultipleIndicators_ReturnsAllApplicable`
  - `CreateIndicators_KnownPhishingTakesPrecedence_ReturnsOnlyPhishingIndicator`
- Added assertion to verify `CategoryName` property

## Test Results
✅ **All tests passing: 22/22 IndicatorFactoryTests**

```
Test Run Successful.
Total tests: 22
     Passed: 22
```

## Build Status
✅ **Build successful with 0 errors**

## Usage Example

### Old Way (Deprecated)
```csharp
Purpose = new Purpose
{
    Category = WebsiteType.Banking,
    Confidence = 0.85f
}
```

### New Way
```csharp
Purpose = new Purpose
{
    CategoryName = "banking",
    Confidence = 0.85f
}

// Get full category details:
var categoryView = asView.GetCategoryView("banking");
```

## Migration Notes

1. **WebsiteType enum is preserved** with `[Obsolete]` attribute for backward compatibility
2. **String category names** match lowercase enum values:
   - `WebsiteType.Banking` → `"banking"`
   - `WebsiteType.ECommerce` → `"ecommerce"`
   - `WebsiteType.Unknown` → `"unknown"`
3. **Full category data** available via `ASView.GetCategoryView(categoryName)`
4. **WebsiteCategoryViews** loaded from database into ASView on initialization

## Files Modified
1. `Common/Enums/WebsiteType.cs`
2. `Business/RealtimeAnalysis/UserDomain/UrlAnalysisViewModels.cs`
3. `Business/RealtimeAnalysis/Indicators/WebsiteTypeIndicator.cs`
4. `Business/RealtimeAnalysis/IndicatorFactory.cs`
5. `Business/RealtimeAnalysis/UserDomain/UDUserAnalyzer.cs`
6. `ASPS.Tests/IndicatorFactoryTests.cs`

## Commit
Branch: `feature/website-category-infrastructure`
Commit: 20c0b2c - "SCRUM-821: Replace WebsiteType enum with WebsiteCategoryViews"

## Next Steps
- Task complete and ready for QA review
- Will add "ready-for-qa" label after confirming with team
