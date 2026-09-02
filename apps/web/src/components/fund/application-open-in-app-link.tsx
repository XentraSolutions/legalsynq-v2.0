import {
  DeepLinkError,
  buildDeepLink,
  type BuildDeepLinkInput,
} from "@/lib/deep-links";

type DeepLinkBuilder = (input: BuildDeepLinkInput) => string;

export function ApplicationOpenInAppLink({
  applicationId,
  builder = buildDeepLink,
}: {
  applicationId: string | null | undefined;
  builder?: DeepLinkBuilder;
}) {
  if (!applicationId?.trim()) return null;

  try {
    const href = builder({
      routeKey: "applicationDetails",
      pathParams: { applicationId },
    });

    return (
      <a
        href={href}
        className="inline-flex items-center gap-1.5 rounded-md border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2"
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
