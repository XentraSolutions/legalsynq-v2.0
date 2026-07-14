"use client";

import BatchUploadComponent from "../batch-upload";

export const dynamic = "force-dynamic";

export default function BatchEntryPage() {
  return <BatchUploadComponent action="create" data={undefined} />;
}
