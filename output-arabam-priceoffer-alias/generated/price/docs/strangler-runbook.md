# Strangler Runbook - Price

## 1. Stabilize Service Boundary
- Objective: Freeze and validate contracts for Price before extraction.
- Rollback: Yes - Disable route switch and keep all traffic in monolith.
- Work Items:
  - Validate endpoint ownership and route coverage.
  - Capture baseline integration and dependency tests.
  - Introduce feature flags for traffic redirection.
- Exit Criteria:
  - Contract tests are green for public/admin/internal endpoints.
  - Canary routing switch is available.

## 2. Read Path Extraction
- Objective: Move read-only APIs first to reduce data-write risk.
- Rollback: Yes - Redirect read traffic back to monolith handlers.
- Work Items:
  - Extract GET/HEAD endpoint handlers.
  - Route read traffic through service facade.
  - Keep writes in monolith and validate parity.
- Exit Criteria:
  - Read API parity and latency targets are met.
  - Fallback route switch is verified.

## 3. Write Path and Data Transition
- Objective: Transition writes with controlled shared database period.
- Rollback: Yes - Switch write route and job dispatch back to monolith path.
- Work Items:
  - Migrate command/write handlers and validation logic.
  - Introduce ownership-aware access control for shared tables.
  - Enable anti-corruption APIs for legacy/shared dependencies.
- Exit Criteria:
  - Write consistency checks are green.
  - Operational rollback path is validated.

## 4. Background Jobs and Monolith Decommission
- Objective: Migrate legacy-hosted jobs and remove monolith runtime dependency.
- Rollback: No - N/A after monolith code path decommission.
- Work Items:
  - Move compatible jobs to service host with new scheduling bootstrap.
  - Keep legacy-hosted jobs in monolith until config/runtime refactor is complete.
  - Delete dead code paths and redundant adapters.
- Exit Criteria:
  - No production traffic executes in extracted monolith modules.
  - Runbook and SLO ownership transferred to service team.

