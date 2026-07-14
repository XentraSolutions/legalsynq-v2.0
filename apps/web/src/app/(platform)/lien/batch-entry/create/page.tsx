"use client";

import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/lien/page-header";
import UploadDocumentComponent from "@/components/lien/upload-document";
import { batchService } from "@/lib/batch/batch.service";
import { dateConverter } from "@/lib/cases/cases.mapper";
import Field from "@/components/lien/field";
import BatchUploadComponent from "../batch-upload";

export const dynamic = "force-dynamic";

const STEPS = ["Upload File", "Map Fields", "Validate", "Import"];

export default function BatchEntryPage() {
  return (
    <BatchUploadComponent
      action="create"
      data={undefined}
    ></BatchUploadComponent>
  );
}
