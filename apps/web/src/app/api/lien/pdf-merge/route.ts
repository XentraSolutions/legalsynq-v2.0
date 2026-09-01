// apps/web/src/app/api/lien/pdf-merge/route.ts
import { type NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";
import { mergePdfsFromUrls } from "@/lib/pdf-merge.service";

const GATEWAY_URL = process.env.GATEWAY_URL ?? "http://127.0.0.1:5010";

function getSessionToken(cookieStore: Awaited<ReturnType<typeof cookies>>) {
  return (
    cookieStore.get("portal_session")?.value ??
    cookieStore.get("platform_session")?.value
  );
}
export async function POST(req: NextRequest): Promise<NextResponse> {
  try {
    const cookieStore = await cookies();
    const token = getSessionToken(cookieStore);

    if (!token) {
      return NextResponse.json(
        { error: { code: "unauthorized" } },
        { status: 401 },
      );
    }

    const { urls } = (await req.json()) as { urls: unknown };

    if (!Array.isArray(urls) || urls.length === 0) {
      return NextResponse.json(
        {
          error: {
            code: "invalid_urls",
            message: "urls must be a non-empty array",
          },
        },
        { status: 400 },
      );
    }

    for (const url of urls) {
      if (typeof url !== "string" || !url.startsWith("/api/lien/")) {
        return NextResponse.json(
          {
            error: {
              code: "invalid_url",
              message: "URLs must be from /api/lien/ BFF paths",
            },
          },
          { status: 400 },
        );
      }
    }

    const absoluteUrls = urls.map((url) => {
      return new URL(url, req.nextUrl.origin).toString();
    });

    const pdfBytes = await mergePdfsFromUrls(absoluteUrls, token);
    const pdfBuffer = Buffer.from(pdfBytes);

    // Return as base64-encoded JSON
    return NextResponse.json({
      data: pdfBuffer.toString("base64"),
    });
  } catch (error) {
    console.error("PDF merge failed:", error);
    return NextResponse.json(
      { error: { code: "merge_failed", message: String(error) } },
      { status: 500 },
    );
  }
}
