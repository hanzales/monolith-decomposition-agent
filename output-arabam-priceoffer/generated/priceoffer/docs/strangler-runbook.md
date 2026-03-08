# Strangler Runbook - PriceOffer

## 1. Stabilize Service Boundary
- Objective: Freeze and validate contracts for PriceOffer before extraction.
- Rollback: Yes - Disable route switch and keep all traffic in monolith.
- Work Items:
  - Validate endpoint ownership and route coverage.
  - Capture baseline integration and dependency tests.
  - Introduce feature flags for traffic redirection.
- Exit Criteria:
  - Contract tests are green for public/admin/internal endpoints.
  - Canary routing switch is available.

## 2. Capability Carve-Out
- Objective: Extract core PriceOffer use-cases behind an anti-corruption boundary.
- Rollback: Yes - Use monolith compatibility facade to re-enable old execution path.
- Work Items:
  - Move service orchestration and repositories for selected capability.
  - Expose stable service contracts to dependent domains.
  - Keep compatibility facade in monolith host.
- Exit Criteria:
  - Core capability runs independently in .NET 10 host.
  - Dependent consumers switched to new service contract.

## 3. Write Path and Data Transition
- Objective: Move write path and ownership to service database.
- Rollback: Yes - Switch write route and job dispatch back to monolith path.
- Work Items:
  - Migrate command/write handlers and validation logic.
  - Cut over table ownership to isolated schema/database.
  - Remove temporary integration adapters after cutover.
- Exit Criteria:
  - Write consistency checks are green.
  - Operational rollback path is validated.

## 4. Background Jobs and Monolith Decommission
- Objective: Finalize cutover and remove obsolete monolith components.
- Rollback: No - N/A after monolith code path decommission.
- Work Items:
  - Move compatible jobs to service host with new scheduling bootstrap.
  - Keep legacy-hosted jobs in monolith until config/runtime refactor is complete.
  - Delete dead code paths and redundant adapters.
- Exit Criteria:
  - No production traffic executes in extracted monolith modules.
  - Runbook and SLO ownership transferred to service team.

