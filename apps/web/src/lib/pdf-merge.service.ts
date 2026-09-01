// apps/web/src/lib/pdf-merge.service.ts
import { PDFDocument, PDFPage } from "pdf-lib";

export async function mergePdfsFromUrls(
  urls: string[],
  token?: string,
): Promise<Uint8Array> {
  const mergedPdf = await PDFDocument.create();
  for (const url of urls) {
    const headers: Record<string, string> = {};
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(url, { headers, cache: "no-store" });
    if (!response.ok) {
      throw new Error(
        `Failed to fetch PDF from ${url}: ${response.statusText}`,
      );
    }

    const pdfBytes = await response.arrayBuffer();
    const pdf = await PDFDocument.load(pdfBytes);

    const copiedPages = await mergedPdf.copyPages(pdf, pdf.getPageIndices());
    copiedPages.forEach((page: PDFPage) => mergedPdf.addPage(page));
  }

  return await mergedPdf.save();
}
