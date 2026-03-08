# Migration Intelligence Report

## Execution
- Executed At (UTC): `2026-03-07T14:41:46.9125317+00:00`
- Source Path: `C:\Users\ilhan.emir\RiderProjects\monolith-decomposition-agent`
- Target Path: `C:\Users\ilhan.emir\RiderProjects\monolith-decomposition-agent\output-design-smoke`
- Dry Run: `True`

## Inventory / Discovery
- Solutions: `1`
- Projects: `11`
- Source Files: `148`
- Markdown Files: `16`
- Controller-like Files: `2`
- Repository-like Files: `4`
- Endpoint Candidates: `0`
- Dependency Signals: `232`
- Legacy Risk Signals: `5`

### Projects and Classification
- `Migration.Intelligence.Agents` (`net10.0`) - `Migration.Intelligence.Agents\Migration.Intelligence.Agents.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.Cli` (`net10.0`) - `Migration.Intelligence.Cli\Migration.Intelligence.Cli.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.CodeAnalysis` (`net10.0`) - `Migration.Intelligence.CodeAnalysis\Migration.Intelligence.CodeAnalysis.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.Contracts` (`net10.0`) - `Migration.Intelligence.Contracts\Migration.Intelligence.Contracts.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.Core` (`net10.0`) - `Migration.Intelligence.Core\Migration.Intelligence.Core.csproj` -> `InfrastructureComponent`
- `Migration.Intelligence.Design` (`net10.0`) - `Migration.Intelligence.Design\Migration.Intelligence.Design.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.DomainInference` (`net10.0`) - `Migration.Intelligence.DomainInference\Migration.Intelligence.DomainInference.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.Generation` (`net10.0`) - `Migration.Intelligence.Generation\Migration.Intelligence.Generation.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.Reporting` (`net10.0`) - `Migration.Intelligence.Reporting\Migration.Intelligence.Reporting.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.Scanner` (`net10.0`) - `Migration.Intelligence.Scanner\Migration.Intelligence.Scanner.csproj` -> `BusinessDomainCandidate`
- `Migration.Intelligence.Validation` (`net10.0`) - `Migration.Intelligence.Validation\Migration.Intelligence.Validation.csproj` -> `BusinessDomainCandidate`

## Structural Analysis
- Consolidated Domain Candidates: `1`
- Domain Hierarchies: `1`
- Endpoint Mappings: `0`
- Repository-to-Table Mappings: `0`
- Execution Chains: `0`
- Hangfire Jobs: `0`
- Producer->Consumer Relationships: `0`
- Scheduled Jobs (Resolved/Unresolved): `0`/`0`
- Workflows: `0`

### Component Layer Summary
- `InfrastructureComponent`: `1` component(s)
- `BusinessDomainCandidate`: `11` component(s)

### External Integration Snapshot
- `Intelligence`: `0` external signal(s)

## Repository -> Table Mapping
- No repository-to-table mappings detected.

## Execution Chains
- No controller->service->repository->table chains detected.

## Hangfire Job Analysis
- No Hangfire-related job signals detected.

## Endpoint Clusters
| Domain | Controllers | Route Prefixes | Endpoint Count | Public | Admin | Internal |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| `Intelligence` | `none` | `none` | `0` | `0` | `0` | `0` |

### Endpoint Ownership Details
- No endpoint ownership mapping detected.

## Domain Consolidation
- Final candidates are consolidated bounded-context roots, not raw entity-level fragments.
- Subdomain/components remain visible for planning but extraction decisions should be made at root-domain level.

### Intelligence
- Subdomains/components: Intelligence

### Missing / Unrendered Domains
- none

## Domain Enumeration Validation
- Inferred Root Domains: `1`
- Rendered Root Domains: `1`
- Domains with Dossiers: `1`
- Domains with Endpoint Clusters: `0`
- Domains with Dependency Entries: `0`
- Validation Status: `warning`

### Validation Warnings
- Domains without endpoint clusters: Intelligence.
- Domains without dependency edges: Intelligence.

### Missing / Unrendered Domains
- none

## Shared Data Analysis
- Table Ownership Entries: `0`
- Shared Tables: `0`
- Ownerless/Ambiguous Tables: `0`


### Shared Table List
- No strongly shared tables inferred.

### Ownerless Table List
- No ownerless/ambiguous tables inferred.

### Shared Kernel Detection
- No shared kernel items inferred.

## Database Split Preparation
- No table ownership data available for split preparation.

## Dependency Matrix
- No cross-domain dependency edges inferred.

## Legacy Risk Analysis
- Risk Types Detected: `7`

### HttpContext usage
- Why Risky: Direct HttpContext access leaks transport concerns into domain/application layers.
- Migration Impact: Porting to microservices requires extensive rewiring of request-scoped context logic.
- Recommended Remediation: Introduce request context interfaces and move HttpContext access to edge adapters/controllers.
- Affected Domain Candidates: Intelligence
- Blocks Extraction: `no`
- ACL/Refactor Needed: `yes`
- Impacted Files:
  - `Migration.Intelligence.CodeAnalysis\Heuristics\LegacyFrameworkHeuristics.cs`
  - `Migration.Intelligence.DomainInference\Services\MigrationIntelligenceAnalyzer.cs`

### static/global context usage
- Why Risky: Global mutable state creates hidden dependencies and thread-safety risks.
- Migration Impact: Service boundary isolation and independent scaling are compromised.
- Recommended Remediation: Refactor to explicit dependency injection, remove global state, and model context per request/workflow.
- Affected Domain Candidates: Intelligence
- Blocks Extraction: `no`
- ACL/Refactor Needed: `yes`
- Impacted Files:
  - `Migration.Intelligence.CodeAnalysis\Heuristics\LegacyFrameworkHeuristics.cs`
  - `Migration.Intelligence.DomainInference\Services\MigrationIntelligenceAnalyzer.cs`

### System.Web dependency
- Why Risky: System.Web pipeline abstractions are tightly coupled to classic ASP.NET hosting.
- Migration Impact: Direct extraction to ASP.NET Core/.NET 10 is blocked until host pipeline concerns are isolated.
- Recommended Remediation: Encapsulate System.Web usages behind adapter interfaces and migrate endpoint surface to ASP.NET Core abstractions first.
- Affected Domain Candidates: Intelligence
- Blocks Extraction: `no`
- ACL/Refactor Needed: `yes`
- Impacted Files:
  - `Migration.Intelligence.CodeAnalysis\Heuristics\LegacyFrameworkHeuristics.cs`
  - `Migration.Intelligence.DomainInference\Services\MigrationIntelligenceAnalyzer.cs`

### .NET Framework-only APIs
- Why Risky: .NET Framework-specific APIs have no direct equivalent or require different hosting/runtime models in .NET 10.
- Migration Impact: Extraction may fail at runtime and block deployment to modern service hosts.
- Recommended Remediation: Replace APIs with .NET cross-platform alternatives and add compatibility wrappers where needed.
- Affected Domain Candidates: Intelligence
- Blocks Extraction: `no`
- ACL/Refactor Needed: `yes`
- Impacted Files:
  - `Migration.Intelligence.DomainInference\Services\MigrationIntelligenceAnalyzer.cs`

### legacy IoC container usage
- Why Risky: Legacy DI container composition roots are often framework-specific and globally coupled.
- Migration Impact: Service bootstrapping for .NET 10 microservices becomes risky and error-prone.
- Recommended Remediation: Move registrations to Microsoft.Extensions.DependencyInjection-compatible modules and remove static container access.
- Affected Domain Candidates: Intelligence
- Blocks Extraction: `no`
- ACL/Refactor Needed: `yes`
- Impacted Files:
  - `Migration.Intelligence.DomainInference\Services\MigrationIntelligenceAnalyzer.cs`

### Razor rendering dependency
- Why Risky: Razor rendering dependencies indicate mixed UI and business logic.
- Migration Impact: Service extraction is delayed because rendering concerns do not belong to domain services.
- Recommended Remediation: Split rendering from business flow and expose API contracts before extraction.
- Affected Domain Candidates: Intelligence
- Blocks Extraction: `no`
- ACL/Refactor Needed: `no`
- Impacted Files:
  - `Migration.Intelligence.DomainInference\Services\MigrationIntelligenceAnalyzer.cs`

### tightly coupled shared config
- Why Risky: Shared configuration access patterns create hidden coupling across domains.
- Migration Impact: Extracted services cannot evolve configuration independently.
- Recommended Remediation: Introduce bounded, versioned config contracts and isolate per-service settings.
- Affected Domain Candidates: Intelligence
- Blocks Extraction: `no`
- ACL/Refactor Needed: `no`
- Impacted Files:
  - `Migration.Intelligence.DomainInference\Services\MigrationIntelligenceAnalyzer.cs`

## Migration Recommendations
### Migration Order Recommendation
| Rank | Candidate | Readiness | Level | Why First/Later | Major Blockers | Read-only First | Staged |
| ---: | --- | ---: | --- | --- | --- | --- | --- |
| `1` | `Intelligence` | `5` | `Low` | Candidate should be postponed until unknown chains, shared data, and legacy blockers are reduced. | `Endpoint ownership confidence is low; controller and route ownership mapping should be validated.; Legacy risk types detected: .NET Framework-only APIs, HttpContext usage, legacy IoC container usage, Razor rendering dependency, static/global context usage, System.Web dependency, tightly coupled shared config.` | `no` | `yes` |

### Service Dossiers
#### Intelligence
- Why Detected: Likely business boundary inferred from controller, repository, table, and dependency co-occurrence.
- Subdomains/Components: none
- Dossier Completeness: `partial (controllers, endpoints, repositories, tables, external dependencies)`
- Controllers: none
- Endpoints: `0` (public `0`, admin `0`, internal `0`)
- Services: none
- Repositories: none
- Entities: ConsolidationResult, DiRegistrationObservation, EndpointObservation, FileObservation, LegacyHostingEvaluation, Name, Naming, OwnershipResult, RegistrationHint, ScheduleConstantHint, ScheduleHint, ScheduleResolutionResult
- Tables: none
- Background Jobs: `0` (consumer `0`, scheduled `0`, triggered `0`, producer `0`, normal `0`, legacy-hosted `0`)
- Consumer Jobs: none
- Scheduled Jobs: none
- Triggered Jobs: none
- Producer Jobs: none
- Normal Jobs: none
- Job Scheduling Dependencies: none
- External Dependencies: none
- Shared Dependencies: none
- Legacy Risks: .NET Framework-only APIs, HttpContext usage, legacy IoC container usage, Razor rendering dependency, static/global context usage, System.Web dependency, tightly coupled shared config
- Coupling Score: `100`
- Cohesion Score: `30`
- Unknown Chain Count: `0`
- Migration Readiness: `5` (Low)
- Readiness Explanation: Readiness=5. Cohesion=30, Coupling=100, SharedTables=0, ExternalDeps=0, LegacyRisks=7, CrossDomainWorkflows=0, UnknownChains=0, BackgroundJobs=0, ConsumerJobs=0, LegacyHostedJobs=0, EndpointOverlap=no, EndpointOwnershipConfidence=0.45.
- Extraction Strategy: Do not extract directly yet. Address legacy and coupling blockers first, then re-evaluate.
- Read-only First Extraction: `no`
- Staged Migration: `yes`
- Major Blockers: Endpoint ownership confidence is low; controller and route ownership mapping should be validated., Legacy risk types detected: .NET Framework-only APIs, HttpContext usage, legacy IoC container usage, Razor rendering dependency, static/global context usage, System.Web dependency, tightly coupled shared config.

### Conclusion Notes
- Initial candidate detected from naming/path signals should be validated with dependency and table ownership evidence before extraction decisions.
- Likely business boundaries with shared data and unknown chains should be migrated in staged increments, not in a single cutover.

## Migration Design
### Domain Boundaries
- Final domains detected: `1`
- `Intelligence`

### Background Job Migration
- No Hangfire jobs detected for migration design.

### Domain Dependencies
- No strong domain dependencies inferred.

### Database Split Strategy
- Shared tables requiring staged strategy: `0`
- none

### Recommended Extraction Order
- `1`. `Intelligence` - Candidate should be postponed until unknown chains, shared data, and legacy blockers are reduced.

### Migration Risks
- `.NET Framework-only APIs`
- `HttpContext usage`
- `legacy IoC container usage`
- `Razor rendering dependency`
- `static/global context usage`
- `System.Web dependency`
- `tightly coupled shared config`
