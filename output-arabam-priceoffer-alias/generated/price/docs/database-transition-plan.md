# Database Transition Plan - Price

- Strategy: Move owned tables first, keep shared tables in monolith DB temporarily, and phase ownership split with outbox/replication.

| Table | Role | Access | Shared | Confidence | Can Move |
| --- | --- | --- | --- | ---: | --- |
| `Advert` | Shared | ReadWrite | `True` | `0.65` | `False` |
| `AgreementAction` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Brands` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `BundlePrice` | Owned | Unknown | `False` | `1.00` | `True` |
| `ContentMedia` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Conversation` | Shared | ReadWrite | `True` | `0.33` | `False` |
| `DamageQuery` | Owned | Read | `False` | `1.00` | `True` |
| `Feedback` | Owned | Read | `False` | `1.00` | `True` |
| `Firm` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `FnSplit` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `GovdeSekilleri` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `LastOfferDisplayCancellations` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Markalar` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Members` | Shared | ReadWrite | `True` | `0.50` | `False` |
| `ModelGroups` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Models` | Shared | ReadWrite | `True` | `0.60` | `False` |
| `NotificationLog` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Offer` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `OfferFirm` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `OfferHistory` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `OfferLiveActivityLog` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `OfferUnavailable` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `OfferVehicleInfo` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `OfferWorkflowHistory` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `PreReservationControlsUserAnswers` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `PriceCheckStepInformation` | Owned | Unknown | `False` | `1.00` | `True` |
| `PriceOffer` | Shared | ReadWrite | `True` | `0.78` | `False` |
| `PriceOfferAction` | Owned | Unknown | `False` | `1.00` | `True` |
| `PriceOfferFeature` | Owned | Unknown | `False` | `1.00` | `True` |
| `PriceOfferReservation` | Owned | Unknown | `False` | `1.00` | `True` |
| `PriceOfferTestimonial` | Owned | Unknown | `False` | `1.00` | `True` |
| `PriceZone` | Owned | Unknown | `False` | `1.00` | `True` |
| `Recompute` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `RecomputeInsiderEventTracker` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `RecomputeNotificationPeriodRule` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `RecomputeNotificationRule` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Renkler` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Reservation` | Shared | ReadWrite | `True` | `0.67` | `False` |
| `ReservationLiveActivityLog` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `ReservationNoShowOffer` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `SalesPoint` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `TestMember` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `UnavailableRecompute` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Vitesler` | Owned | ReadWrite | `False` | `1.00` | `True` |
| `Yakitlar` | Owned | ReadWrite | `False` | `1.00` | `True` |
