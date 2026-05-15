using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Payments;

[ApiController]
[Route("api/commerce/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentCheckoutService _checkout;

    public PaymentsController(IPaymentCheckoutService checkout) => _checkout = checkout;

    [HttpPost("checkout-sessions")]
    [ProducesResponseType(typeof(CheckoutSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CheckoutSessionResponse>> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequest request, CancellationToken ct)
    {
        var result = await _checkout.CreateCheckoutSessionAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

[ApiController]
[Route("api/commerce/payments/event-logs")]
public sealed class PaymentEventLogsController : ControllerBase
{
    private readonly IPaymentWebhookService _webhooks;

    public PaymentEventLogsController(IPaymentWebhookService webhooks) => _webhooks = webhooks;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentProviderEventLogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentProviderEventLogResponse>>> List(
        [FromQuery] PaymentProviderType? provider,
        [FromQuery] PaymentProviderEventProcessingStatus? status,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await _webhooks.ListAsync(provider, status, take, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentProviderEventLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentProviderEventLogResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _webhooks.GetAsync(id, ct));
}

[ApiController]
[Route("api/commerce/billing-accounts/{billingAccountId:guid}/payment-methods")]
public sealed class BillingAccountPaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodReferenceService _service;
    public BillingAccountPaymentMethodsController(IPaymentMethodReferenceService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentMethodReferenceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodReferenceResponse>>> List(
        Guid billingAccountId, CancellationToken ct)
        => Ok(await _service.ListForAccountAsync(billingAccountId, ct));

    [HttpPost("{paymentMethodId:guid}/make-default")]
    [ProducesResponseType(typeof(PaymentMethodReferenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentMethodReferenceResponse>> MakeDefault(
        Guid billingAccountId, Guid paymentMethodId, CancellationToken ct)
        => Ok(await _service.MakeDefaultAsync(billingAccountId, paymentMethodId, ct));
}

[ApiController]
[Route("api/commerce/billing-accounts/{billingAccountId:guid}/payment-customers")]
public sealed class BillingAccountPaymentCustomersController : ControllerBase
{
    private readonly IPaymentProviderCustomerService _service;
    public BillingAccountPaymentCustomersController(IPaymentProviderCustomerService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentProviderCustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentProviderCustomerResponse>>> List(
        Guid billingAccountId, CancellationToken ct)
        => Ok(await _service.ListForAccountAsync(billingAccountId, ct));
}
