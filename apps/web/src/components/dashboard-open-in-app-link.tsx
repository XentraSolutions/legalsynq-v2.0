import {
  OpenInAppLink,
  type DeepLinkBuilder,
} from "@/components/open-in-app-link";

export function DashboardOpenInAppLink({
  builder,
}: {
  builder?: DeepLinkBuilder;
}) {
  return (
    <OpenInAppLink
      intent={{ routeKey: "dashboard" }}
      builder={builder}
      className="inline-flex w-fit shrink-0 items-center gap-1.5 rounded-md border border-gray-200 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2"
    />
  );
}
