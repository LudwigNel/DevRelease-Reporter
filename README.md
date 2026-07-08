# DevRelease-Reporter

Turn commits into clear, business-ready release reports. DevRelease Reporter automatically summarizes development work into structured, stakeholder-friendly reports with a .NET backend and Avalonia desktop UI.

## Solution structure

- `src\DevReleaseReporter.Core` - domain and report logic
- `src\DevReleaseReporter.Infrastructure` - Azure DevOps and external integrations
- `src\DevReleaseReporter.Cli` - terminal entry point for early end-to-end flows
- `src\DevReleaseReporter.Ui` - Avalonia desktop application

## Current status

Phase 5 adds the Avalonia MVP UI with Azure DevOps inputs, named reusable profiles, a dedicated settings dialog, contributor selection, theme switching, saved UI preferences, secure PAT storage in Windows Credential Manager, richer progress feedback, preview output, and HTML/Excel export.

## CLI

```powershell
dotnet run --project src\DevReleaseReporter.Cli\DevReleaseReporter.Cli.csproj -- generate-report `
  --organization-url https://dev.azure.com/your-org `
  --project YourProject `
  --repository YourRepository `
  --from 2026-06-01 `
  --to 2026-06-23 `
  --user "Jane Doe" `
  --output .\artifacts\release-report.html `
  --pat-env AZDO_PAT
```

## Build

```powershell
dotnet build DevReleaseReporter.slnx
```
