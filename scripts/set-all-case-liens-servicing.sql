UPDATE liens_Liens
SET IsServicing = 'Yes'
WHERE CaseId IS NOT NULL
  AND COALESCE(IsServicing, '') <> 'Yes';
