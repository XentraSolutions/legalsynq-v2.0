using System.Text.Json;
using Intake.Domain.Matching;

namespace Intake.Application.Matching;

public sealed record MatchingFieldRule(
    string FactCode,
    string CandidateFieldName,
    string ComparisonMethod,
    decimal Weight,
    decimal ConflictPenalty,
    bool HardConflict);

public sealed record MatchingEntityRule(
    IReadOnlyList<MatchingFieldRule> Fields,
    decimal StrongThreshold,
    decimal PossibleThreshold,
    int StrongMinimumMatchedFields,
    bool StrongRequiresHardIdentifier,
    decimal HardConflictMaximumScore);

public sealed record MatchingDuplicateRule(
    string Code,
    string DuplicateType,
    IReadOnlyList<string> RequiredFactCodes,
    IReadOnlyList<string> RequiredEntityTypes,
    decimal Score,
    string Status);

public sealed record MatchingProfileDocument(
    IReadOnlyList<string> EntityTypes,
    IReadOnlyDictionary<string, MatchingEntityRule> EntityRules,
    MatchingDuplicateRule? PrimaryDuplicateRule);

public static class MatchingProfileParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static MatchingProfileDocument Parse(MatchingProfileDefinition profile)
    {
        var document = JsonSerializer.Deserialize<MatchingProfileDocument>(
            profile.DefinitionJson,
            Options);
        if (document is null)
            throw new InvalidOperationException($"Matching profile '{profile.Code}' is empty.");

        return document;
    }
}

public static class MatchingProfileDefaults
{
    public const string Code = "LIEN_INTAKE_MATCHING_V1";
    public const int Version = 1;
    public const string ScoringVersion = "B10-SCORE-1";

    public const string DefinitionJson = """
        {
          "entityTypes":["PATIENT","PROVIDER","FACILITY","ATTORNEY","LAW_FIRM","CASE"],
          "entityRules":{
            "PATIENT":{
              "fields":[
                {"factCode":"PATIENT_NAME","candidateFieldName":"PATIENT_NAME","comparisonMethod":"PERSON_NAME","weight":0.25,"conflictPenalty":0.20,"hardConflict":false},
                {"factCode":"DATE_OF_BIRTH","candidateFieldName":"DATE_OF_BIRTH","comparisonMethod":"EXACT","weight":0.30,"conflictPenalty":0.45,"hardConflict":true},
                {"factCode":"PATIENT_IDENTIFIER","candidateFieldName":"PATIENT_IDENTIFIER","comparisonMethod":"EXACT","weight":0.30,"conflictPenalty":0.45,"hardConflict":true},
                {"factCode":"ACCOUNT_NUMBER","candidateFieldName":"ACCOUNT_NUMBER","comparisonMethod":"EXACT","weight":0.15,"conflictPenalty":0.25,"hardConflict":false}
              ],
              "strongThreshold":0.80,
              "possibleThreshold":0.50,
              "strongMinimumMatchedFields":2,
              "strongRequiresHardIdentifier":true,
              "hardConflictMaximumScore":0.49
            },
            "PROVIDER":{
              "fields":[
                {"factCode":"PROVIDER_NAME","candidateFieldName":"PROVIDER_NAME","comparisonMethod":"ORGANIZATION","weight":0.50,"conflictPenalty":0.25,"hardConflict":false},
                {"factCode":"PROVIDER_PHONE","candidateFieldName":"PROVIDER_PHONE","comparisonMethod":"EXACT","weight":0.25,"conflictPenalty":0.25,"hardConflict":false},
                {"factCode":"FACILITY_ADDRESS","candidateFieldName":"PROVIDER_ADDRESS","comparisonMethod":"ADDRESS","weight":0.25,"conflictPenalty":0.20,"hardConflict":false}
              ],
              "strongThreshold":0.80,
              "possibleThreshold":0.50,
              "strongMinimumMatchedFields":2,
              "strongRequiresHardIdentifier":false,
              "hardConflictMaximumScore":0.59
            },
            "FACILITY":{
              "fields":[
                {"factCode":"PROVIDER_NAME","candidateFieldName":"FACILITY_NAME","comparisonMethod":"ORGANIZATION","weight":0.50,"conflictPenalty":0.25,"hardConflict":false},
                {"factCode":"PROVIDER_PHONE","candidateFieldName":"FACILITY_PHONE","comparisonMethod":"EXACT","weight":0.25,"conflictPenalty":0.25,"hardConflict":false},
                {"factCode":"FACILITY_ADDRESS","candidateFieldName":"FACILITY_ADDRESS","comparisonMethod":"ADDRESS","weight":0.25,"conflictPenalty":0.20,"hardConflict":false}
              ],
              "strongThreshold":0.80,
              "possibleThreshold":0.50,
              "strongMinimumMatchedFields":2,
              "strongRequiresHardIdentifier":false,
              "hardConflictMaximumScore":0.59
            },
            "ATTORNEY":{
              "fields":[
                {"factCode":"ATTORNEY_EMAIL","candidateFieldName":"ATTORNEY_EMAIL","comparisonMethod":"EXACT","weight":0.45,"conflictPenalty":0.45,"hardConflict":true},
                {"factCode":"ATTORNEY_NAME","candidateFieldName":"ATTORNEY_NAME","comparisonMethod":"PERSON_NAME","weight":0.30,"conflictPenalty":0.20,"hardConflict":false},
                {"factCode":"LAW_FIRM_NAME","candidateFieldName":"LAW_FIRM_NAME","comparisonMethod":"ORGANIZATION","weight":0.15,"conflictPenalty":0.15,"hardConflict":false},
                {"factCode":"ATTORNEY_PHONE","candidateFieldName":"ATTORNEY_PHONE","comparisonMethod":"EXACT","weight":0.10,"conflictPenalty":0.10,"hardConflict":false}
              ],
              "strongThreshold":0.80,
              "possibleThreshold":0.50,
              "strongMinimumMatchedFields":2,
              "strongRequiresHardIdentifier":true,
              "hardConflictMaximumScore":0.49
            },
            "LAW_FIRM":{
              "fields":[
                {"factCode":"LAW_FIRM_NAME","candidateFieldName":"LAW_FIRM_NAME","comparisonMethod":"ORGANIZATION","weight":0.75,"conflictPenalty":0.35,"hardConflict":false},
                {"factCode":"ATTORNEY_EMAIL","candidateFieldName":"ATTORNEY_EMAIL","comparisonMethod":"EXACT","weight":0.125,"conflictPenalty":0.10,"hardConflict":false},
                {"factCode":"ATTORNEY_PHONE","candidateFieldName":"ATTORNEY_PHONE","comparisonMethod":"EXACT","weight":0.125,"conflictPenalty":0.10,"hardConflict":false}
              ],
              "strongThreshold":0.80,
              "possibleThreshold":0.50,
              "strongMinimumMatchedFields":1,
              "strongRequiresHardIdentifier":false,
              "hardConflictMaximumScore":0.59
            },
            "CASE":{
              "fields":[
                {"factCode":"CASE_NUMBER","candidateFieldName":"CASE_NUMBER","comparisonMethod":"EXACT","weight":0.45,"conflictPenalty":0.45,"hardConflict":true},
                {"factCode":"PATIENT_NAME","candidateFieldName":"PATIENT_NAME","comparisonMethod":"PERSON_NAME","weight":0.20,"conflictPenalty":0.15,"hardConflict":false},
                {"factCode":"CLAIM_NUMBER","candidateFieldName":"CLAIM_NUMBER","comparisonMethod":"EXACT","weight":0.15,"conflictPenalty":0.25,"hardConflict":false},
                {"factCode":"DATE_OF_ACCIDENT","candidateFieldName":"DATE_OF_ACCIDENT","comparisonMethod":"EXACT","weight":0.10,"conflictPenalty":0.15,"hardConflict":false},
                {"factCode":"ATTORNEY_NAME","candidateFieldName":"ATTORNEY_NAME","comparisonMethod":"PERSON_NAME","weight":0.10,"conflictPenalty":0.10,"hardConflict":false}
              ],
              "strongThreshold":0.80,
              "possibleThreshold":0.50,
              "strongMinimumMatchedFields":2,
              "strongRequiresHardIdentifier":true,
              "hardConflictMaximumScore":0.49
            }
          },
          "primaryDuplicateRule":{
            "code":"PATIENT_PROVIDER_ACCOUNT_SERVICE_DATE",
            "duplicateType":"BUSINESS_KEY_DUPLICATE",
            "requiredFactCodes":["PATIENT_NAME","PROVIDER_NAME","ACCOUNT_NUMBER","DATE_OF_SERVICE_START"],
            "requiredEntityTypes":["PATIENT","PROVIDER"],
            "score":0.90,
            "status":"POSSIBLE"
          }
        }
        """;
}