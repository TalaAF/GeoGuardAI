import type { AxiosInstance } from "axios";

/**
 * Fetches the PDF through the authenticated API client and hands it to the browser.
 *
 * A plain link cannot be used: the download endpoint requires the bearer token, and the
 * response must never be a direct object-storage URL. The blob is revoked immediately after
 * the click so nothing is retained in memory.
 */
export async function downloadReport(
  api: AxiosInstance,
  reportId: string,
  reference: string,
) {
  const response = await api.get<Blob>(`/reports/${reportId}/download`, {
    responseType: "blob",
    headers: { Accept: "application/pdf" },
  });
  const url = URL.createObjectURL(
    new Blob([response.data], { type: "application/pdf" }),
  );
  const link = document.createElement("a");
  link.href = url;
  link.download = `${reference}.pdf`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
