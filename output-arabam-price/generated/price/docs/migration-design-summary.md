# Migration Design Summary - Price

- Bounded Context: `PriceContext`
- Readiness: `Low` (5)
- Cohesion/Coupling: `100` / `5`
- Boundary Confidence: `92%`
- Strategy: `ReadOnlyFirst`

## Validation
- Score: `100`
- Errors: `0`
- Warnings: `0`

## Key Blockers
- Anti-corruption layer required for legacy/shared dependencies.
- HttpContext usage: Direct HttpContext access leaks transport concerns into domain/application layers.
- HttpContext usage: Porting to microservices requires extensive rewiring of request-scoped context logic.
- Shared table coupling (6 table(s)).
