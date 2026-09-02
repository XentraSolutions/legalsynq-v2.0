import {
  DeepLinkError,
  buildDeepLink,
  type BuildDeepLinkInput,
} from "@/lib/deep-links";

type DeepLinkBuilder = (input: BuildDeepLinkInput) => string;

export function DashboardOpenInAppLink({
  builder = buildDeepLink,
}: {
  builder?: DeepLinkBuilder;
}) {
  try {
    const href = builder({ routeKey: "dashboard" });

    return (
      <a
        href={href}
        className="inline-flex w-fit shrink-0 items-center gap-1.5 rounded-md border border-gray-200 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2"
      >
        <i className="ri-smartphone-line" aria-hidden="true" />
        Open in App
      </a>
    );
  } catch (error) {
    if (error instanceof DeepLinkError) return null;
    throw error;
  }
}
