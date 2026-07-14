param(
    [string] $BaseUrl = "https://core-qa.legalsynq.net/liens",
    [string] $BearerToken = $env:LEGAL_SYNQ_QA_BEARER_TOKEN,
    [string] $CorrelationId = [guid]::NewGuid().ToString()
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BearerToken)) {
    throw "Set LEGAL_SYNQ_QA_BEARER_TOKEN to a valid rl-liens1 bearer token before running this script."
}

$BaseUrl = $BaseUrl.TrimEnd("/")
$headers = @{
    accept = "application/json"
    authorization = "Bearer $BearerToken"
    "x-correlation-id" = $CorrelationId
}

function Invoke-LegalSynqJson {
    param(
        [Parameter(Mandatory = $true)] [string] $Method,
        [Parameter(Mandatory = $true)] [string] $Path,
        [object] $Body
    )

    $uri = "$BaseUrl/$($Path.TrimStart("/"))"
    $params = @{
        Method = $Method
        Uri = $uri
        Headers = $headers
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 12)
    }

    try {
        Invoke-RestMethod @params
    }
    catch {
        $responseBody = ""
        if ($_.Exception.Response -and $_.Exception.Response.GetResponseStream()) {
            $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
        }

        throw "Request failed: $Method $uri`n$responseBody"
    }
}

$stamp = [DateTime]::UtcNow.ToString("yyyyMMddHHmmss")
$incidentDate = "06/15/2026"
$reportStartDate = "06/06/2026"
$reportEndDate = "07/06/2026"

Write-Host "Creating QA medical-provider export seed data. CorrelationId=$CorrelationId"

$provider = Invoke-LegalSynqJson -Method "POST" -Path "/api/liens/contacts" -Body @{
    ContactType = "Provider"
    FirstName = "QA"
    LastName = "Medical"
    Title = "Provider Relations"
    Organization = "QA Medical Provider Seed $stamp"
    Email = "qa-medical-provider-$stamp@demo.legalsynq.test"
    Phone = "(702) 555-2600"
    AddressLine1 = "2600 QA Provider Way"
    City = "Henderson"
    State = "NV"
    PostalCode = "89052"
    Notes = "Seeded for medical-provider report export verification."
}

$providerId = $provider.id
if ([string]::IsNullOrWhiteSpace($providerId)) {
    throw "Provider create did not return an id."
}

$caseCode = "QA-RL1-MP-$stamp"
$case = Invoke-LegalSynqJson -Method "POST" -Path "/api/liens/cases/create" -Body @{
    code = $caseCode
    firstname = "QA"
    lastname = "Patient$stamp"
    dob = "01/01/1990"
    address = "1500 Dashboard Export Ave"
    city = "Henderson"
    state = "NV"
    zipcode = "89052"
    dateOfLoss = $incidentDate
    externalCaseId = "QA-MP-EXPORT-$stamp"
    note = "accidentTypeId=MVA; accidentType=Motor Vehicle Accident"
}

$caseId = $case.data.id
if ([string]::IsNullOrWhiteSpace($caseId)) {
    throw "Case create did not return data.id."
}

$lien = Invoke-LegalSynqJson -Method "POST" -Path "/api/liens/cases/liens/medical" -Body @{
    caseId = $caseId
    status = "Active"
    purchaseDate = $incidentDate
    initialServiceDate = $incidentDate
    endServiceDate = "06/20/2026"
    note = "Seeded for medical-provider dashboard report export."
    isBulk = "No"
    isServicing = "Yes"
}

$lienId = $lien.data
if ([string]::IsNullOrWhiteSpace($lienId)) {
    throw "Lien create did not return data."
}

Invoke-LegalSynqJson -Method "POST" -Path "/api/liens/liens/reassign/medical-provider" -Body @{
    liensId = $lienId
    medicalProvider = $providerId
} | Out-Null

Invoke-LegalSynqJson -Method "POST" -Path "/api/liens/cases/liens/medicalcode" -Body @{
    liensId = $lienId
    code = "QA-SEED-001"
    medicareCost = "75.00"
    billingAmount = "150.00"
    purchaseAmount = "100.00"
    payee = "QA Health System"
    outboundCheckNumber = "QA-$stamp"
} | Out-Null

$report = Invoke-LegalSynqJson -Method "POST" -Path "/api/liens/cases/dashboard/medical-provider-report-export/v3" -Body @{
    page = 1
    limit = 500
    startDate = $reportStartDate
    endDate = $reportEndDate
    filterType = "medicalProvider"
    filterId = $providerId
}

Write-Host "Seed complete."
Write-Host "ProviderId=$providerId"
Write-Host "CaseId=$caseId"
Write-Host "LienId=$lienId"
Write-Host "ReportTotalCount=$($report.totalCount)"

if ($report.totalCount -lt 1) {
    throw "Seeded records were created, but the report endpoint still returned no rows for provider $providerId."
}

$report.items | Select-Object -First 3 | ConvertTo-Json -Depth 12
