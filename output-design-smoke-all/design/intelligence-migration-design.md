# Migration Design: Intelligence

## Service Blueprint
- Bounded Context: `IntelligenceContext`
- Cohesion Score: `30`
- Coupling Score: `100`
- Migration Readiness: `Low` (5)
- Description: Likely business boundary inferred from controller, repository, table, and dependency co-occurrence.

## Boundary
- Confidence: `57%`
- Rationale: Intelligence boundary inferred from 0 controllers, 0 services, 0 repositories and 0 tables (low-confidence).
- Controllers: 
- Services: 
- Repositories: 
- Tables: 

## Contracts
- Public APIs: `0`
- Admin APIs: `0`
- Internal APIs: `0`
- Event Contracts: `0`
- Contract Completeness: `24%`

## Data Ownership
- Owned Tables: `0`
- Shared Tables: `0`
- Referenced Tables: `0`
- Strategy: No clearly owned tables detected; start extraction with API contract and workflow isolation first.

## Integrations
- Outbound Integrations: `0`
- Inbound Integrations: `0`
- Internal Dependencies: `0`
- Needs ACL: `True`

## Strangler Plan
- Extraction Strategy: `DeferredDueToCoupling`
- Read-Only First: `False`
- Staged Migration: `True`
- Phase Count: `4`

## Blockers
- Anti-corruption layer required for legacy/shared dependencies.
- High coupling score: 100.

## Readiness Notes
- Boundary confidence: 57%
- Contract completeness: 24%
- No clearly owned tables detected; start extraction with API contract and workflow isolation first.
- 0 outbound and 0 inbound integration links detected; 1 anti-corruption requirement(s) inferred.
- Extraction strategy: DeferredDueToCoupling
- Readiness (Low): Readiness=5. Cohesion=30, Coupling=100, SharedTables=0, ExternalDeps=0, LegacyRisks=7, CrossDomainWorkflows=0, UnknownChains=0, BackgroundJobs=0, ConsumerJobs=0, LegacyHostedJobs=0, EndpointOverlap=no, EndpointOwnershipConfidence=0.45.

## Phases
### 1. Stabilize Service Boundary
- Objective: Freeze and validate contracts for Intelligence before extraction.
- Can Rollback: `True`
- Rollback Strategy: Disable route switch and keep all traffic in monolith.
- Work Items:
  - Validate endpoint ownership and route coverage.
  - Capture baseline integration and dependency tests.
  - Introduce feature flags for traffic redirection.
- Exit Criteria:
  - Contract tests are green for public/admin/internal endpoints.
  - Canary routing switch is available.

### 2. Capability Carve-Out
- Objective: Extract core Intelligence use-cases behind an anti-corruption boundary.
- Can Rollback: `True`
- Rollback Strategy: Use monolith compatibility facade to re-enable old execution path.
- Work Items:
  - Move service orchestration and repositories for selected capability.
  - Expose stable service contracts to dependent domains.
  - Keep compatibility facade in monolith host.
- Exit Criteria:
  - Core capability runs independently in .NET 10 host.
  - Dependent consumers switched to new service contract.

### 3. Write Path and Data Transition
- Objective: Move write path and ownership to service database.
- Can Rollback: `True`
- Rollback Strategy: Switch write route and job dispatch back to monolith path.
- Work Items:
  - Migrate command/write handlers and validation logic.
  - Cut over table ownership to isolated schema/database.
  - Enable anti-corruption APIs for legacy/shared dependencies.
- Exit Criteria:
  - Write consistency checks are green.
  - Operational rollback path is validated.

### 4. Background Jobs and Monolith Decommission
- Objective: Finalize cutover and remove obsolete monolith components.
- Can Rollback: `False`
- Rollback Strategy: N/A after monolith code path decommission.
- Work Items:
  - Move compatible jobs to service host with new scheduling bootstrap.
  - Keep legacy-hosted jobs in monolith until config/runtime refactor is complete.
  - Delete dead code paths and redundant adapters.
- Exit Criteria:
  - No production traffic executes in extracted monolith modules.
  - Runbook and SLO ownership transferred to service team.

