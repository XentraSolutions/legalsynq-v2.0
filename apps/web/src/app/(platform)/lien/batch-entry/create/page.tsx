"use client";
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
