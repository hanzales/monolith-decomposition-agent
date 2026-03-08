# Migration Design: Price

## Service Blueprint
- Bounded Context: `PriceContext`
- Cohesion Score: `100`
- Coupling Score: `5`
- Migration Readiness: `Low` (5)
- Description: Likely business boundary inferred from controller, repository, table, and dependency co-occurrence.

## Boundary
- Confidence: `92%`
- Rationale: Price boundary inferred from 7 controllers, 3 services, 23 repositories and 45 tables (high-confidence).
- Controllers: ApprovePriceOfferController, PriceOfferController, PriceOfferRestrictedController, PriceOfferTestimonialController, PriceSuggestionController, PricingApiController, PricingController
- Services: PriceOfferReservationService, PriceOfferService, PriceOfferTestimonialService
- Repositories: BundlePriceRepository, PriceCheckStepInformationRepository, PriceOfferActionRepository, PriceOfferCodeRepository, PriceOfferContentRepository, PriceOfferConversationRepository, PriceOfferDamageQueryRepository, PriceOfferFeatureRepository, PriceOfferFeedbackRepository, PriceOfferKilometerImageRepository, PriceOfferKilometerRepository, PriceOfferLiveActivityLogRepository, PriceOfferNotificationLogRepository, PriceOfferOfferFirmRepository, PriceOfferOtpCodeRepository, PriceOfferRecomputeRepository, PriceOfferRepository, PriceOfferReservationCancellationComponentRepository, PriceOfferReservationRepository, PriceOfferTestimonialRepository, PriceOfferUnavailableRecomputeRepository, PriceOfferUnavailableRepository, PriceZoneRepository
- Tables: Advert, AgreementAction, Brands, BundlePrice, ContentMedia, Conversation, DamageQuery, Feedback, Firm, FnSplit, GovdeSekilleri, LastOfferDisplayCancellations, Markalar, Members, ModelGroups, Models, NotificationLog, Offer, OfferFirm, OfferHistory, OfferLiveActivityLog, OfferUnavailable, OfferVehicleInfo, OfferWorkflowHistory, PreReservationControlsUserAnswers, PriceCheckStepInformation, PriceOffer, PriceOfferAction, PriceOfferFeature, PriceOfferReservation, PriceOfferTestimonial, PriceZone, Recompute, RecomputeInsiderEventTracker, RecomputeNotificationPeriodRule, RecomputeNotificationRule, Renkler, Reservation, ReservationLiveActivityLog, ReservationNoShowOffer, SalesPoint, TestMember, UnavailableRecompute, Vitesler, Yakitlar

## Contracts
- Public APIs: `69`
- Admin APIs: `4`
- Internal APIs: `0`
- Event Contracts: `19`
- Contract Completeness: `76%`

## Data Ownership
- Owned Tables: `39`
- Shared Tables: `6`
- Referenced Tables: `0`
- Strategy: Move owned tables first, keep shared tables in monolith DB temporarily, and phase ownership split with outbox/replication.

## Integrations
- Outbound Integrations: `84`
- Inbound Integrations: `7`
- Internal Dependencies: `27`
- Needs ACL: `True`

## Strangler Plan
- Extraction Strategy: `ReadOnlyFirst`
- Read-Only First: `True`
- Staged Migration: `True`
- Phase Count: `4`

## Blockers
- Anti-corruption layer required for legacy/shared dependencies.
- HttpContext usage: Direct HttpContext access leaks transport concerns into domain/application layers.
- HttpContext usage: Porting to microservices requires extensive rewiring of request-scoped context logic.
- Shared table coupling (6 table(s)).

## Readiness Notes
- Boundary confidence: 92%
- Contract completeness: 76%
- Move owned tables first, keep shared tables in monolith DB temporarily, and phase ownership split with outbox/replication.
- 84 outbound and 7 inbound integration links detected; 3 anti-corruption requirement(s) inferred.
- Extraction strategy: ReadOnlyFirst
- Readiness (Low): Readiness=5. Cohesion=100, Coupling=5, SharedTables=6, ExternalDeps=75, LegacyRisks=1, CrossDomainWorkflows=7, UnknownChains=0, BackgroundJobs=30, ConsumerJobs=13, LegacyHostedJobs=17, EndpointOverlap=no, EndpointOwnershipConfidence=0.93.

## Phases
### 1. Stabilize Service Boundary
- Objective: Freeze and validate contracts for Price before extraction.
- Can Rollback: `True`
- Rollback Strategy: Disable route switch and keep all traffic in monolith.
- Work Items:
  - Validate endpoint ownership and route coverage.
  - Capture baseline integration and dependency tests.
  - Introduce feature flags for traffic redirection.
- Exit Criteria:
  - Contract tests are green for public/admin/internal endpoints.
  - Canary routing switch is available.

### 2. Read Path Extraction
- Objective: Move read-only APIs first to reduce data-write risk.
- Can Rollback: `True`
- Rollback Strategy: Redirect read traffic back to monolith handlers.
- Work Items:
  - Extract GET/HEAD endpoint handlers.
  - Route read traffic through service facade.
  - Keep writes in monolith and validate parity.
- Exit Criteria:
  - Read API parity and latency targets are met.
  - Fallback route switch is verified.

### 3. Write Path and Data Transition
- Objective: Transition writes with controlled shared database period.
- Can Rollback: `True`
- Rollback Strategy: Switch write route and job dispatch back to monolith path.
- Work Items:
  - Migrate command/write handlers and validation logic.
  - Introduce ownership-aware access control for shared tables.
  - Enable anti-corruption APIs for legacy/shared dependencies.
- Exit Criteria:
  - Write consistency checks are green.
  - Operational rollback path is validated.

### 4. Background Jobs and Monolith Decommission
- Objective: Migrate legacy-hosted jobs and remove monolith runtime dependency.
- Can Rollback: `False`
- Rollback Strategy: N/A after monolith code path decommission.
- Work Items:
  - Move compatible jobs to service host with new scheduling bootstrap.
  - Keep legacy-hosted jobs in monolith until config/runtime refactor is complete.
  - Delete dead code paths and redundant adapters.
- Exit Criteria:
  - No production traffic executes in extracted monolith modules.
  - Runbook and SLO ownership transferred to service team.

