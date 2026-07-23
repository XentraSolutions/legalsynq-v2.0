import { type NextRequest } from "next/server";
import { proxyPublicBuyerPortal } from "@/lib/liens/public-buyer-portal-proxy";

export const dynamic = "force-dynamic";

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ token: string }> },
) {
  const { token } = await params;
  return proxyPublicBuyerPortal(req, token);
}
