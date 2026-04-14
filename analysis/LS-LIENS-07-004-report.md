# LS-LIENS-07-004: Notifications Service Integration — Completion Report

## Objective
Integrate the Liens service with the Notifications service to emit fire-and-forget event notifications for key lien sale workflow actions.

## Implementation Summary

### Infrastructure Layer
- **`INotificationPublisher`** (`Liens.Application/Interfaces/INotificationPublisher.cs`): Single-method interface — `Task PublishAsync(string templateKey, Guid tenantId, Dictionary<string, string> templateData, CancellationToken ct)`.
- **`NotificationPublisher`** (`Liens.Infrastructure/Notifications/NotificationPublisher.cs`): Posts to `POST /v1/notifications` on the Notifications service (port 5008). Uses named `HttpClient` (`"NotificationsService"`). Channel=`"event"`, productType=`"synqliens"`. Logs warnings on failure; does not throw (fire-and-forget pattern).
- **DI Registration** (`Liens.Infrastructure/DependencyInjection.cs`): `HttpClient` named `"NotificationsService"` configured from `Services:NotificationsUrl` (default `http://localhost:5008`). `INotificationPublisher` registered as scoped.

### Notification Events (5 types)

| # | Template Key | Service | Trigger Point | Key Template Data |
|---|-------------|---------|---------------|-------------------|
| 1 | `lienoffer.submitted` | `LienOfferService` | After offer created + audit published | lienId, lienNumber, offerId, buyerOrgId, sellerOrgId, offerAmount |
| 2 | `lienoffer.accepted` | `LienSaleService` | After sale committed + audit published | lienId, lienNumber, offerId, billOfSaleId, billOfSaleNumber, amounts |
| 3 | `lienoffer.rejected` | `LienSaleService` | Per rejected competing offer in loop | offerId, lienId, lienNumber, buyerOrgId, sellerOrgId, acceptedOfferId |
| 4 | `lien.sale.finalized` | `LienSaleService` | After sale committed (alongside accepted) | Same as lienoffer.accepted |
| 5 | `billofsale.document.generated` | `LienSaleService` | After BOS document attached (post-commit) | billOfSaleId, billOfSaleNumber, documentId, lienId, lienNumber |

### Design Decisions
- **Fire-and-forget**: All notification calls use `_ = _notifications.PublishAsync(...)` — failures are logged but never block the business flow.
- **Tenant header propagation**: `X-Tenant-Id` header is set on every outgoing request to satisfy the Notifications API's `TenantMiddleware`.
- **Cancellation-safe**: Publisher uses `CancellationToken.None` for HTTP calls to prevent request cancellation from silently dropping fire-and-forget notifications.
- **Post-commit placement**: Notifications fire after transaction commit to avoid notifying on rolled-back operations.
- **Per-offer rejection**: Each rejected competing offer gets its own notification with specific offer/buyer context.
- **Nullable handling**: `DiscountPercent` (nullable decimal) uses `?.ToString("F2") ?? "0.00"`.
- **Consistent with audit pattern**: Notifications placed immediately after audit publish calls for consistent ordering.

## Build Status
- **Errors**: 0
- **Warnings**: 0 (in Liens projects)
- **Service health**: `GET /health` returns `{"status":"ok","service":"liens"}`

## Files Modified
1. `Liens.Application/Interfaces/INotificationPublisher.cs` — NEW
2. `Liens.Infrastructure/Notifications/NotificationPublisher.cs` — NEW
3. `Liens.Infrastructure/DependencyInjection.cs` — HttpClient + DI registration
4. `Liens.Application/Services/LienOfferService.cs` — `lienoffer.submitted` notification
5. `Liens.Application/Services/LienSaleService.cs` — 4 notification types (accepted, rejected loop, finalized, document generated)
