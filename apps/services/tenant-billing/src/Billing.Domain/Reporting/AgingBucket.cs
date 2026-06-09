namespace Billing.Domain.Reporting;

/// <summary>
/// MS-BILL-WRITE-007 — accounts-receivable aging buckets used by the
/// invoice-aging report. The bucket is derived from
/// <c>(nowUtc.Date - invoice.DueDate.Date).Days</c>:
/// <list type="bullet">
///   <item><c>Current</c>     — DueDate is today or in the future (≤ 0 days late)</item>
///   <item><c>1-30</c>        — 1 to 30 days past due</item>
///   <item><c>31-60</c>       — 31 to 60 days past due</item>
///   <item><c>61-90</c>       — 61 to 90 days past due</item>
///   <item><c>90+</c>         — 91+ days past due</item>
/// </list>
/// The same single function (<see cref="ForDaysOverdue"/>) is the
/// only place these thresholds live, so the report cannot drift
/// from any future renderer that consumes the same row shape.
/// </summary>
public static class AgingBucket
{
    public const string Current = "Current";
    public const string Days1To30 = "1-30";
    public const string Days31To60 = "31-60";
    public const string Days61To90 = "61-90";
    public const string Days90Plus = "90+";

    /// <summary>
    /// Pure / synchronous bucket assignment from the signed
    /// "days past due" integer. A negative or zero value is
    /// <c>Current</c> (invoice is not yet overdue today).
    /// </summary>
    public static string ForDaysOverdue(int daysOverdue)
    {
        if (daysOverdue <= 0) return Current;
        if (daysOverdue <= 30) return Days1To30;
        if (daysOverdue <= 60) return Days31To60;
        if (daysOverdue <= 90) return Days61To90;
        return Days90Plus;
    }

    /// <summary>
    /// Whole-day count between <paramref name="dueDate"/> and
    /// <paramref name="nowUtc"/>. Compared on date boundaries so an
    /// invoice due today is reported as 0 (Current), not -1. Negative
    /// when the due date is still in the future.
    /// </summary>
    public static int DaysOverdue(System.DateTime dueDate, System.DateTime nowUtc)
        => (nowUtc.Date - dueDate.Date).Days;
}
