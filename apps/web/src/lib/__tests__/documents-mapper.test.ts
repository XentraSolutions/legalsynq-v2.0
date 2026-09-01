import assert from "node:assert/strict";
import test from "node:test";
import { mapDocuments } from "../cases/cases.mapper";
import type { DocumentTypeResponse } from "../lookup/lookup.types";

const otherDocumentTypeId = "10000000-0000-0000-0000-000000000005";

const documentTypes: DocumentTypeResponse[] = [
  {
    id: otherDocumentTypeId,
    category: "DocumentCategory",
    code: "Other",
    description: "Other",
    isActive: true,
    isSystem: true,
    name: "Other",
    sortOrder: 5,
  },
];

test("mapDocuments uses canonical documentTypeId before legacy typeId", () => {
  const mapped = mapDocuments(
    {
      data: [
        {
          id: "document-1",
          liensId: null,
          typeId: "14",
          documentTypeId: otherDocumentTypeId,
        },
      ],
    },
    documentTypes,
  );

  assert.equal(mapped.caseDocuments[0]?.documentType, "Other");
});

test("mapDocuments displays Other when historical type metadata is unavailable", () => {
  const mapped = mapDocuments(
    {
      data: [
        {
          id: "document-2",
          liensId: null,
        },
      ],
    },
    documentTypes,
  );

  assert.equal(mapped.caseDocuments[0]?.documentType, "Other");
});
