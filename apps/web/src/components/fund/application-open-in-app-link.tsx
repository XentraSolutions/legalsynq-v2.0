import {
  OpenInAppLink,
  type DeepLinkBuilder,
} from "@/components/open-in-app-link";

export function ApplicationOpenInAppLink({
  applicationId,
  builder,
}: {
  applicationId: string | null | undefined;
  builder?: DeepLinkBuilder;
}) {
  if (!applicationId?.trim()) return null;

  return (
    <OpenInAppLink
      intent={{
        routeKey: "applicationDetails",
        pathParams: { applicationId },
      }}
      builder={builder}
      className="inline-flex items-center gap-1.5 rounded-md border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2"
    />
  );
}
