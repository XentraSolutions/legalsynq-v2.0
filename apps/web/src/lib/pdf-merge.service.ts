import { PDFDocument, PDFPage } from "pdf-lib";

export async function mergePdfsFromUrls(urls: string[]): Promise<Uint8Array> {
  console.log(urls);
  const mergedPdf = await PDFDocument.create();

  for (const url of urls) {
    const response = await fetch(url);
    const pdfBytes = await response.arrayBuffer();
    const pdf = await PDFDocument.load(pdfBytes);

    const copiedPages = await mergedPdf.copyPages(pdf, pdf.getPageIndices());
    copiedPages.forEach((page: PDFPage) => mergedPdf.addPage(page));
  }

  return await mergedPdf.save();
}
