using Intake.Domain.Extraction;

namespace Intake.Application.Extraction;

public sealed record ExtractionFactDescriptor(
    string Code,
    string DataType,
    string Description);

public static class ExtractionFactCatalog
{
    public static IReadOnlyList<ExtractionFactDescriptor> All { get; } =
    [
        new("PATIENT_NAME", ExtractionFactDataTypes.Name, "Patient or claimant name as written."),
        new("PATIENT_IDENTIFIER", ExtractionFactDataTypes.Identifier, "Patient or claimant identifier as written."),
        new("DATE_OF_BIRTH", ExtractionFactDataTypes.Date, "Date of birth as written."),
        new("PROVIDER_NAME", ExtractionFactDataTypes.Name, "Provider, facility, or creditor name."),
        new("PROVIDER_IDENTIFIER", ExtractionFactDataTypes.Identifier, "Provider, facility, or creditor identifier."),
        new("DATE_OF_SERVICE_START", ExtractionFactDataTypes.Date, "Beginning of service date as written."),
        new("DATE_OF_SERVICE_END", ExtractionFactDataTypes.Date, "End of service date as written."),
        new("INVOICE_NUMBER", ExtractionFactDataTypes.Identifier, "Invoice or account invoice identifier."),
        new("ACCOUNT_NUMBER", ExtractionFactDataTypes.Identifier, "Patient or provider account identifier."),
        new("LIEN_AMOUNT", ExtractionFactDataTypes.Money, "Lien or claimed amount as written."),
        new("BILLED_AMOUNT", ExtractionFactDataTypes.Money, "Billed amount as written."),
        new("PAID_AMOUNT", ExtractionFactDataTypes.Money, "Paid amount as written."),
        new("BALANCE_AMOUNT", ExtractionFactDataTypes.Money, "Balance amount as written."),
        new("SETTLEMENT_AMOUNT", ExtractionFactDataTypes.Money, "Settlement amount as written, if present."),
        new("INSURER_NAME", ExtractionFactDataTypes.Name, "Insurer name."),
        new("CLAIM_NUMBER", ExtractionFactDataTypes.Identifier, "Insurance claim identifier."),
        new("POLICY_NUMBER", ExtractionFactDataTypes.Identifier, "Insurance policy identifier."),
        new("ATTORNEY_NAME", ExtractionFactDataTypes.Name, "Attorney name."),
        new("LAW_FIRM_NAME", ExtractionFactDataTypes.Name, "Law firm name."),
        new("LETTER_DATE", ExtractionFactDataTypes.Date, "Date of a letter or correspondence."),
        new("DOCUMENT_DATE", ExtractionFactDataTypes.Date, "Date printed on the document."),
        new("DOCUMENT_TITLE", ExtractionFactDataTypes.Text, "Document title as written."),
        new("FACILITY_ADDRESS", ExtractionFactDataTypes.Address, "Facility or provider address."),
        new("EFFECTIVE_DATE", ExtractionFactDataTypes.Date, "Policy or agreement effective date."),
        new("EXPIRATION_DATE", ExtractionFactDataTypes.Date, "Policy or agreement expiration date."),
    ];

    public static IReadOnlyDictionary<string, ExtractionFactDescriptor> ByCode { get; } =
        All.ToDictionary(item => item.Code, StringComparer.Ordinal);

    public static bool IsKnown(string code) => ByCode.ContainsKey(code);
}