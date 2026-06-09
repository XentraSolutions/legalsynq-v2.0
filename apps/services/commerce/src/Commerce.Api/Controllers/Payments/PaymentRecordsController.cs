using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Payments;

[ApiController]
[Route("api/commerce/payments")]
public sealed class PaymentRecordsController : ControllerBase
{
    private readonly IPaymentRecordService _service;
    public PaymentRecordsController(IPaymentRecordService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>> List(
        [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _service.ListAsync(take, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));
}

[ApiController]
[Route("api/commerce/payment-attempts")]
public sealed class PaymentAttemptsController : ControllerBase
{
    private readonly IPaymentRecordService _service;
    public PaymentAttemptsController(IPaymentRecordService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentAttemptResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentAttemptResponse>>> List(
        [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _service.ListAttemptsAsync(take, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentAttemptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentAttemptResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAttemptAsync(id, ct));
}

[ApiController]
[Route("api/commerce/billing-accounts/{billingAccountId:guid}/payments")]
public sealed class BillingAccountPaymentsController : ControllerBase
{
    private readonly IPaymentRecordService _service;
    public BillingAccountPaymentsController(IPaymentRecordService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>> List(
        Guid billingAccountId, CancellationToken ct)
        => Ok(await _service.ListForBillingAccountAsync(billingAccountId, ct));
}

[ApiController]
[Route("api/commerce/subscriptions/{subscriptionId:guid}/payments")]
public sealed class SubscriptionPaymentsController : ControllerBase
{
    private readonly IPaymentRecordService _service;
    public SubscriptionPaymentsController(IPaymentRecordService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>> List(
        Guid subscriptionId, CancellationToken ct)
        => Ok(await _service.ListForSubscriptionAsync(subscriptionId, ct));
}

[ApiController]
[Route("api/commerce/payments/event-logs")]
public sealed class ProviderEventsReprocessController : ControllerBase
{
    private readonly IProviderEventReplayService _service;
    public ProviderEventsReprocessController(IProviderEventReplayService service) => _service = service;

    [HttpPost("{id:guid}/reprocess")]
    [ProducesResponseType(typeof(ReprocessProviderEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReprocessProviderEventResponse>> Reprocess(
        Guid id, CancellationToken ct)
        => Ok(await _service.ReprocessAsync(id, ct));
}

[ApiController]
[Route("api/commerce/invoices/{invoiceId:guid}/manual-payments")]
public sealed class InvoiceManualPaymentsController : ControllerBase
{
    private readonly IManualPaymentRecordingService _service;
    public InvoiceManualPaymentsController(IManualPaymentRecordingService service)
        => _service = service;

    /// <summary>
    /// Records an out-of-band ("manual") payment against an invoice
    /// (cash, check, ACH, wire, etc.). Creates a Payment row with
    /// provider=Manual and applies it to the invoice via
    /// Invoice.RegisterPayment in a single unit of work. Idempotent
    /// behavior is intentionally NOT provided — admins entering data
    /// manually expect each submission to produce a row.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PaymentResponse>> Record(
        Guid invoiceId,
        [FromBody] RecordManualPaymentRequest request,
        CancellationToken ct)
    {
        var payment = await _service.RecordAsync(invoiceId, request, ct);
        return CreatedAtAction(
            nameof(PaymentRecordsController.Get),
            controllerName: "PaymentRecords",
            routeValues: new { id = payment.Id },
            value: payment);
    }
}
