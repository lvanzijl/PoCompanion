# Test Failure Census

## 1. Execution Metadata
- Commit hash: `2d29629b3235b784b0678cd7b1948df6f4d678de`
- Timestamp: `2026-04-02T20:43:06.718Z`
- Exact command: `dotnet test PoTool.sln --configuration Release --nologo --logger "trx;LogFileName=2026-04-02-test-failure-census.trx"`
- Environment: Linux sandbox, .NET test runner via `dotnet test`
- Run status: completed (test command exited with failures, not interrupted)
- Raw log: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/test-failure-census/2026-04-02-test-failure-census.log`
- TRX artifacts: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/TestResults/2026-04-02-test-failure-census.trx`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/TestResults/2026-04-02-test-failure-census.trx`
- Failure table CSV: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/test-failure-census/2026-04-02-test-failure-census-failures.csv`

## 2. Summary
- Total tests: 1799
- Total failures: 17
- Total skipped: 0
- High-level distribution by category:
  - 4. Routing / endpoint classification: 1
  - 9. Outdated expectation (test no longer matches intended behavior): 4
  - 6. Architecture / audit / rule enforcement: 10
  - 5. API contract / NSwag / client generation: 2

## 3. Full Failure Inventory

| Fully qualified test name | Project | Error type | Primary error message | Top 3 relevant frames | Deterministic | Category | Suspected root cause |
|---|---|---|---|---|---|---|---|
| PoTool.Tests.Unit.Configuration.DataSourceModeConfigurationTests.GetRouteIntent_PortfolioReadRoute_IsLiveAllowed | PoTool.Tests.Unit | assertion | Assert.AreEqual failed. Expected:&lt;LiveAllowed&gt;. Actual:&lt;CacheOnlyAnalyticalRead&gt;. 'expected' expression: 'DataSourceModeConfiguration.RouteIntent.LiveAllowed', 'actual' expression: 'intent'. | at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertAreEqualFailed(Object expected, Object actual, String userMessage) in /_/src/TestFramework/TestFramework/Assertions/Assert.AreEqual.cs:line 665 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual[T] (T expected, T actual, IEqualityComparer`1 comparer, String message, String expectedExpression, String actualExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.AreEqual.cs:line 492 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual[T] (T expected, T actual, String message, String expectedExpression, String actualExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.AreEqual.cs:line 405 | Best effort: deterministic | 4. Routing / endpoint classification | Route intent expectation no longer matches current data-source classification |
| PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsLiveProvider_WhenModeIsLive | PoTool.Tests.Unit | assertion | Test method PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsLiveProvider_WhenModeIsLive threw exception: | at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateArgumentCallSites(ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain, ParameterInfo[] parameters, Boolean throwIfCallSiteNotFound) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateConstructorCallSite(ResultCache lifetime, ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateExact(ServiceDescriptor descriptor, ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain, Int32 slot) | Best effort: deterministic | 9. Outdated expectation (test no longer matches intended behavior) | Assertion baseline no longer matches current implementation |
| PoTool.Tests.Unit.Audits.CdcGeneratedDomainMapDocumentTests.GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource | PoTool.Tests.Unit | assertion | StringAssert.Contains failed. String '# CDC Domain Map — Generated | at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring, StringComparison comparisonType, String message) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 125 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 48 <br> at PoTool.Tests.Unit.Audits.CdcGeneratedDomainMapDocumentTests.GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CdcGeneratedDomainMapDocumentTests.cs:line 132 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Audits.DocumentationComplianceBatch5Tests.DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames | PoTool.Tests.Unit | assertion | Assert.IsTrue failed. 'condition' expression: 'CanonicalFolders.Contains(segments[1], StringComparer.Ordinal)'. Non-canonical docs folder: docs/reports/2026-03-31-ui-exploratory-screenshot-run.md | at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertIsTrueFailed(String message) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 161 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AssertIsTrueInterpolatedStringHandler.ComputeAssertion(String conditionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 40 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(Nullable`1 condition, AssertIsTrueInterpolatedStringHandler& message, String conditionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 129 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Audits.UiSemanticLabelsTests.StoryPointSurfaces_UseExplicitStoryPointLabels | PoTool.Tests.Unit | assertion | StringAssert.Contains failed. String '@using PoTool.Client.Components.Common | at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring, StringComparison comparisonType, String message) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 125 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 48 <br> at PoTool.Tests.Unit.Audits.UiSemanticLabelsTests.StoryPointSurfaces_UseExplicitStoryPointLabels() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/UiSemanticLabelsTests.cs:line 15 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors | PoTool.Tests.Unit | assertion | StringAssert.Contains failed. String 'using System.Text.Json; | at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring, StringComparison comparisonType, String message) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 125 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 48 <br> at PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CdcUsageCoverageDocumentTests.cs:line 85 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Audits.DocumentationComplianceBatch5Tests.DocumentationCompliance_ReportFilesUseDatedNaming | PoTool.Tests.Unit | assertion | Assert.IsTrue failed. 'condition' expression: 'datedReport.IsMatch(fileName)'. Non-dated report filename: 2026-04-02-di-activation-audit-and-fix.md | at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertIsTrueFailed(String message) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 161 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AssertIsTrueInterpolatedStringHandler.ComputeAssertion(String conditionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 40 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(Nullable`1 condition, AssertIsTrueInterpolatedStringHandler& message, String conditionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 129 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Architecture.BuildQualityArchitectureGuardTests.BuildQualityArchitectureGuard_NoConfidenceLogicInClient | PoTool.Tests.Unit | assertion | Assert.IsFalse failed. 'condition' expression: 'violations.Any()'. Confidence comparisons must not exist in PoTool.Client. Confidence semantics belong to backend/shared BuildQuality logic, not the presentation layer. | at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertIsFalseFailed(String userMessage) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 200 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsFalse(Nullable`1 condition, String message, String conditionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.IsTrue.cs:line 191 <br> at PoTool.Tests.Unit.Architecture.BuildQualityArchitectureGuardTests.AssertRule(ArchitectureGuardRule rule) in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Architecture/BuildQualityArchitectureGuardTests.cs:line 110 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Architecture guard detects client-side confidence logic |
| PoTool.Tests.Unit.Audits.NswagGovernanceTests.CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource | PoTool.Tests.Unit | assertion | Assert.Contains failed. Expected collection to contain the specified item. 'expected' expression: 'requiredType', 'collection' expression: 'excludedTypeNames.ToList()'. NSwag must exclude shared contract type 'DataStateDto'. | at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertContainsItemFailed(String userMessage) in /_/src/TestFramework/TestFramework/Assertions/Assert.Contains.cs:line 713 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Contains[T] (T expected, IEnumerable`1 collection, String message, String expectedExpression, String collectionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.Contains.cs:line 195 <br> at PoTool.Tests.Unit.Audits.NswagGovernanceTests.CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs:line 44 | Best effort: deterministic | 5. API contract / NSwag / client generation | Generated/openapi contract governance expectations diverged from current source |
| PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsCachedProvider_WhenModeIsCache | PoTool.Tests.Unit | assertion | Test method PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsCachedProvider_WhenModeIsCache threw exception: | at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateArgumentCallSites(ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain, ParameterInfo[] parameters, Boolean throwIfCallSiteNotFound) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateConstructorCallSite(ResultCache lifetime, ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateExact(ServiceDescriptor descriptor, ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain, Int32 slot) | Best effort: deterministic | 9. Outdated expectation (test no longer matches intended behavior) | Assertion baseline no longer matches current implementation |
| PoTool.Tests.Unit.Audits.DocumentationVerificationBatch6Tests.DocumentationVerification_AnalysisFilesWithLegacyTermsCarryHistoricalNote | PoTool.Tests.Unit | assertion | Assert.IsEmpty failed. Expected collection of size 0. Actual: 2. 'collection' expression: 'missingNotes'. Analysis files with legacy terms must carry a historical note: | at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertCountFailed(String assertionName, Int32 expectedCount, Int32 actualCount, String userMessage) in /_/src/TestFramework/TestFramework/Assertions/Assert.Count.cs:line 345 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AssertCountInterpolatedStringHandler`1.ComputeAssertion(String assertionName, String collectionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.Count.cs:line 51 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsEmpty[T] (IEnumerable`1 collection, AssertCountInterpolatedStringHandler`1& message, String collectionExpression) in /_/src/TestFramework/TestFramework/Assertions/Assert.Count.cs:line 292 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Audits.DocumentationVerificationBatch6Tests.DocumentationVerification_RuleMirrorsExposeTrustClosureLanguage | PoTool.Tests.Unit | assertion | StringAssert.Contains failed. String '# Persistence contract | at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring, StringComparison comparisonType, String message) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 125 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 48 <br> at PoTool.Tests.Unit.Audits.DocumentationVerificationBatch6Tests.DocumentationVerification_RuleMirrorsExposeTrustClosureLanguage() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationVerificationBatch6Tests.cs:line 161 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Audits.NswagGovernanceTests.ManualApiClientExtensions_AreLimitedToGovernedEnvelopeWrappersAndJsonSettings | PoTool.Tests.Unit | assertion | CollectionAssert.AreEquivalent failed. The number of elements in the collections do not match. Expected:&lt;6&gt;. Actual:&lt;7&gt;. | at Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.AreEquivalent[T] (IEnumerable`1 expected, IEnumerable`1 actual, IEqualityComparer`1 comparer, String message) in /_/src/TestFramework/TestFramework/Assertions/CollectionAssert.cs:line 503 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.AreEquivalent(ICollection expected, ICollection actual) in /_/src/TestFramework/TestFramework/Assertions/CollectionAssert.cs:line 387 <br> at PoTool.Tests.Unit.Audits.NswagGovernanceTests.ManualApiClientExtensions_AreLimitedToGovernedEnvelopeWrappersAndJsonSettings() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs:line 84 | Best effort: deterministic | 5. API contract / NSwag / client generation | Generated/openapi contract governance expectations diverged from current source |
| PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_ServiceAnchorsMatchCurrentCdcBoundaries | PoTool.Tests.Unit | assertion | StringAssert.Contains failed. String 'using PoTool.Core.Domain.Forecasting.Components.DeliveryForecast; | at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring, StringComparison comparisonType, String message) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 125 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 48 <br> at PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_ServiceAnchorsMatchCurrentCdcBoundaries() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CdcUsageCoverageDocumentTests.cs:line 123 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_RespectsLiveMode | PoTool.Tests.Unit | assertion | Test method PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_RespectsLiveMode threw exception: | at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateArgumentCallSites(ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain, ParameterInfo[] parameters, Boolean throwIfCallSiteNotFound) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateConstructorCallSite(ResultCache lifetime, ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateExact(ServiceDescriptor descriptor, ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain, Int32 slot) | Best effort: deterministic | 9. Outdated expectation (test no longer matches intended behavior) | Assertion baseline no longer matches current implementation |
| PoTool.Tests.Unit.Audits.PortfolioCdcUiAuditTests.PortfolioCdcReadOnlyPanel_ConsumesReadOnlyDtosWithoutUiAggregations | PoTool.Tests.Unit | assertion | StringAssert.Contains failed. String '@using PoTool.Client.Components.Common | at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring, StringComparison comparisonType, String message) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 125 <br> at Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(String value, String substring) in /_/src/TestFramework/TestFramework/Assertions/StringAssert.cs:line 48 <br> at PoTool.Tests.Unit.Audits.PortfolioCdcUiAuditTests.PortfolioCdcReadOnlyPanel_ConsumesReadOnlyDtosWithoutUiAggregations() in /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/PortfolioCdcUiAuditTests.cs:line 21 | Best effort: deterministic | 6. Architecture / audit / rule enforcement | Documentation or audit snapshots no longer match current repository content |
| PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_DelegatesCallsToFactory | PoTool.Tests.Unit | assertion | Test method PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_DelegatesCallsToFactory threw exception: | at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateArgumentCallSites(ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain, ParameterInfo[] parameters, Boolean throwIfCallSiteNotFound) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateConstructorCallSite(ResultCache lifetime, ServiceIdentifier serviceIdentifier, Type implementationType, CallSiteChain callSiteChain) <br> at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory.CreateExact(ServiceDescriptor descriptor, ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain, Int32 slot) | Best effort: deterministic | 9. Outdated expectation (test no longer matches intended behavior) | Assertion baseline no longer matches current implementation |

## 4. Clustered Failures

### Route intent classification drift
- Number of tests: 1
- Category: 4. Routing / endpoint classification
- Root cause: Route intent expectation no longer matches current data-source classification
- Severity: HIGH
- Estimated age: UNKNOWN_AGE
- Representative tests:
  - `PoTool.Tests.Unit.Configuration.DataSourceModeConfigurationTests.GetRouteIntent_PortfolioReadRoute_IsLiveAllowed`

### Other outdated expectations
- Number of tests: 4
- Category: 9. Outdated expectation (test no longer matches intended behavior)
- Root cause: Assertion baseline no longer matches current implementation
- Severity: LOW
- Estimated age: UNKNOWN_AGE
- Representative tests:
  - `PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsLiveProvider_WhenModeIsLive`
  - `PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsCachedProvider_WhenModeIsCache`
  - `PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_RespectsLiveMode`
  - `PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_DelegatesCallsToFactory`

### Documentation and audit baseline drift
- Number of tests: 9
- Category: 6. Architecture / audit / rule enforcement
- Root cause: Documentation or audit snapshots no longer match current repository content
- Severity: LOW
- Estimated age: MIXED/UNKNOWN_AGE
- Representative tests:
  - `PoTool.Tests.Unit.Audits.CdcGeneratedDomainMapDocumentTests.GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource`
  - `PoTool.Tests.Unit.Audits.DocumentationComplianceBatch5Tests.DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames`
  - `PoTool.Tests.Unit.Audits.UiSemanticLabelsTests.StoryPointSurfaces_UseExplicitStoryPointLabels`
  - `PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors`
  - `PoTool.Tests.Unit.Audits.DocumentationComplianceBatch5Tests.DocumentationCompliance_ReportFilesUseDatedNaming`

### Build-quality architecture guard drift
- Number of tests: 1
- Category: 6. Architecture / audit / rule enforcement
- Root cause: Architecture guard detects client-side confidence logic
- Severity: LOW
- Estimated age: f69e04dd93d1540b11c82d6baeec75e491490e7c 2026-04-02T19:58:58Z
- Representative tests:
  - `PoTool.Tests.Unit.Architecture.BuildQualityArchitectureGuardTests.BuildQualityArchitectureGuard_NoConfidenceLogicInClient`

### NSwag and client contract drift
- Number of tests: 2
- Category: 5. API contract / NSwag / client generation
- Root cause: Generated/openapi contract governance expectations diverged from current source
- Severity: MEDIUM
- Estimated age: f69e04dd93d1540b11c82d6baeec75e491490e7c 2026-04-02T19:58:58Z
- Representative tests:
  - `PoTool.Tests.Unit.Audits.NswagGovernanceTests.CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource`
  - `PoTool.Tests.Unit.Audits.NswagGovernanceTests.ManualApiClientExtensions_AreLimitedToGovernedEnvelopeWrappersAndJsonSettings`

## 5. Category Breakdown

| Category | Count | Percentage |
|---|---:|---:|
| 4. Routing / endpoint classification | 1 | 5.9% |
| 9. Outdated expectation (test no longer matches intended behavior) | 4 | 23.5% |
| 6. Architecture / audit / rule enforcement | 10 | 58.8% |
| 5. API contract / NSwag / client generation | 2 | 11.8% |

## 6. Governance Assessment

- Signal quality: medium
- Failures concentrated or fragmented: concentrated into 5 main clusters with audit/governance drift dominant
- Shared infrastructure dominant source: no; audit/rule expectations dominate, with one DI host setup gap and one routing classification mismatch
- Signal-to-noise ratio acceptable: limited; runtime signal is diluted by governance/audit failures
- Can this suite currently act as a merge gate: no; 17 failures remain and most are unmanaged baseline/governance drift rather than clearly triaged feature regressions
- Key weaknesses:
  - audit/rules and documentation baselines are mixed into the same gate as runtime tests
  - DI fixture setup gaps remain in test infrastructure
  - chronology is mostly UNKNOWN_AGE at cluster level without CI/pass-history evidence

## 7. Immediate Risks

- **Route intent classification drift** (HIGH) — Route intent expectation no longer matches current data-source classification

## 8. Recommended Next Actions (ordered)

1. Create a checked-in failure baseline from this census and make it the authoritative unmanaged-failure inventory.
2. Split runtime/integration behavior tests from audit/governance/documentation tests into separate CI jobs and reporting channels.
3. Assign ownership per cluster, starting with routing classification and DI test-host setup, then audit/NSwag/doc baselines.
4. Add CI trend tracking for each cluster so new failures can be distinguished from known legacy failures.
5. Isolate audit/rules tests behind an explicit governance gate rather than mixing them into the same trust signal as runtime correctness.
6. Mark chronology as UNKNOWN_AGE unless CI history or git evidence can prove introduction windows.
7. Keep raw logs, TRX, and failure CSV as artifacts for future baseline comparisons.

## Supporting Compact Failure Table

| Test | Cluster | Severity |
|---|---|---|
| PoTool.Tests.Unit.Configuration.DataSourceModeConfigurationTests.GetRouteIntent_PortfolioReadRoute_IsLiveAllowed | Route intent classification drift | HIGH |
| PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsLiveProvider_WhenModeIsLive | Other outdated expectations | LOW |
| PoTool.Tests.Unit.Audits.CdcGeneratedDomainMapDocumentTests.GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Audits.DocumentationComplianceBatch5Tests.DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Audits.UiSemanticLabelsTests.StoryPointSurfaces_UseExplicitStoryPointLabels | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Audits.DocumentationComplianceBatch5Tests.DocumentationCompliance_ReportFilesUseDatedNaming | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Architecture.BuildQualityArchitectureGuardTests.BuildQualityArchitectureGuard_NoConfidenceLogicInClient | Build-quality architecture guard drift | LOW |
| PoTool.Tests.Unit.Audits.NswagGovernanceTests.CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource | NSwag and client contract drift | MEDIUM |
| PoTool.Tests.Unit.Services.DataSourceAwareReadProviderFactoryTests.GetWorkItemReadProvider_ReturnsCachedProvider_WhenModeIsCache | Other outdated expectations | LOW |
| PoTool.Tests.Unit.Audits.DocumentationVerificationBatch6Tests.DocumentationVerification_AnalysisFilesWithLegacyTermsCarryHistoricalNote | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Audits.DocumentationVerificationBatch6Tests.DocumentationVerification_RuleMirrorsExposeTrustClosureLanguage | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Audits.NswagGovernanceTests.ManualApiClientExtensions_AreLimitedToGovernedEnvelopeWrappersAndJsonSettings | NSwag and client contract drift | MEDIUM |
| PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_ServiceAnchorsMatchCurrentCdcBoundaries | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_RespectsLiveMode | Other outdated expectations | LOW |
| PoTool.Tests.Unit.Audits.PortfolioCdcUiAuditTests.PortfolioCdcReadOnlyPanel_ConsumesReadOnlyDtosWithoutUiAggregations | Documentation and audit baseline drift | LOW |
| PoTool.Tests.Unit.Services.LazyReadProviderTests.LazyWorkItemReadProvider_DelegatesCallsToFactory | Other outdated expectations | LOW |