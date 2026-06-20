# DevRelease Reporter

DevRelease Reporter is a cross-platform .NET solution for generating business-friendly release reports from Azure DevOps repositories.

## 1) Proposed application architecture

Use a pragmatic layered architecture (clean/onion style):

- **Domain layer** (`DevReleaseReporter.Domain`)
  - Core business models (`CommitInfo`, `PullRequestInfo`, `WorkItemInfo`, `ReleaseQuery`, `ReleaseReportDocument`)
  - No UI or external API dependencies
- **Application layer** (`DevReleaseReporter.Application`)
  - Use-case orchestration (`ReleaseReportService`)
  - Abstractions (`IReleaseDataSource`, `ICommitClassifier`, `IReportRenderer`)
  - Classification + report rendering logic
- **Infrastructure layer** (`DevReleaseReporter.Infrastructure.AzureDevOps`)
  - Azure DevOps REST integration (`AzureDevOpsDataSource`)
  - Mapping API DTOs to domain models
- **Presentation layer(s)**
  - CLI (`DevReleaseReporter.Cli`)
  - Desktop shell (`DevReleaseReporter.Desktop`, Avalonia-ready composition point)

This gives clean separation, testability, and allows adding GitHub/Jira providers by implementing `IReleaseDataSource`.

## 2) Recommended project structure

```text
DevReleaseReporter.slnx
src/
  DevReleaseReporter.Domain/
  DevReleaseReporter.Application/
  DevReleaseReporter.Infrastructure.AzureDevOps/
  DevReleaseReporter.Cli/
  DevReleaseReporter.Desktop/
tests/
  DevReleaseReporter.Application.Tests/
```

## 3) Key design patterns used

- **Ports & Adapters (Hexagonal)** for data-source integrations
- **Strategy** for commit classification (`ICommitClassifier`)
- **Template/Renderer abstraction** for output formats (`IReportRenderer`) enabling HTML now, PDF/Word later
- **Application Service** for orchestration (`ReleaseReportService`)

## 4) Core logic design (classification + report generation)

- Ingest commit/PR/work item snapshot through `IReleaseDataSource`
- Classify commits with `KeywordCommitClassifier` into:
  - Feature, BugFix, Improvement, Documentation, Maintenance, Unknown
- Build `ReleaseReportDocument` with summary counts and detailed items
- Render via `HtmlReportRenderer` into stakeholder-friendly HTML

The current MVP is resilient to inconsistent data by:
- Not requiring work items
- Falling back to `Unknown` category when commit messages are ambiguous

## 5) Azure DevOps integration structure

`AzureDevOpsDataSource`:
- Uses `HttpClient` + PAT-based auth header
- Fetches commits with pagination (`$top`/`$skip`) for large histories
- Fetches PRs and filters by release window
- Returns normalized domain models for use by both CLI and Desktop UI

This keeps Azure-specific concerns isolated and replaceable.

## 6) Libraries/framework recommendations

Current MVP (implemented):
- .NET 10, xUnit
- `System.Text.Json`, `HttpClient`

Recommended next additions:
- **Avalonia UI** for desktop UX
- **Spectre.Console.Cli** for richer CLI commands
- **Polly** for API retry/backoff policies
- **QuestPDF** or **Open XML SDK** for PDF/Word exports
- **FluentValidation** for query/config validation
- **Microsoft.Extensions.DependencyInjection** for composition root setup

## Running

Generate a sample local report:

```bash
dotnet run --project /home/runner/work/DevRelease-Reporter/DevRelease-Reporter/src/DevReleaseReporter.Cli/DevReleaseReporter.Cli.csproj -- --sample
```

Generate from Azure DevOps:

```bash
export AZDO_ORGANIZATION="your-org"
export AZDO_PROJECT="your-project"
export AZDO_REPOSITORY="your-repo"
export AZDO_PAT="your-pat"

dotnet run --project /home/runner/work/DevRelease-Reporter/DevRelease-Reporter/src/DevReleaseReporter.Cli/DevReleaseReporter.Cli.csproj
```
