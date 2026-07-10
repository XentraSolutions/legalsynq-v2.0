import { ActionMenu } from "@/components/lien/action-menu";

type DataValidationComponentProps = {
  validations: {
    isSuccess?: boolean;
    message?: string;
    totalRows?: number;
    successCount?: number;
    failedCount?: number;
    data?: Array<{
      id: string;
      batchUploadId: string;
      row: number;
      status: string;
      reason: string;
      data: Record<string, unknown>;
    }>;
  } | null;
  status?: "VIEWING" | "PROCESSING";
};
export default function DataValidationComponent({
  validations,
  status = "PROCESSING",
}: DataValidationComponentProps) {
  if (status === "VIEWING") {
    const rows = validations?.data ?? [];

    const columns = rows.length > 0 ? Object.keys(rows[0].data ?? {}) : [];

    return (
      <div className="border border-gray-200 rounded-lg overflow-hidden bg-white">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">
                  Row
                </th>
                <th className="px-4 py-3 text-center text-xs font-medium uppercase text-gray-500">
                  Status
                </th>

                {columns.map((column) => (
                  <th
                    key={column}
                    className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500"
                  >
                    {column}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody className="divide-y divide-gray-100 bg-white">
              {rows.map((item) => (
                <tr key={item.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-sm">{item.row}</td>
                  <td className="px-4 py-3 text-center">
                    {item.status === "FAILED" ? (
                      <div className="group relative inline-flex">
                        <i className="ri-alert-line text-red-500 text-lg cursor-pointer" />

                        <div className="pointer-events-none absolute left-1/2 top-full z-20 mt-2 hidden -translate-x-1/2 whitespace-nowrap rounded bg-gray-900 px-3 py-2 text-xs text-white shadow-lg group-hover:block">
                          {item.reason}
                        </div>
                      </div>
                    ) : (
                      <i className="ri-checkbox-circle-line text-green-500 text-lg" />
                    )}
                  </td>
                  {columns.map((column) => (
                    <td
                      key={column}
                      className="px-4 py-3 text-sm text-gray-700"
                    >
                      {item.data[column] == null
                        ? "—"
                        : String(item.data[column])}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div
        className={`flex items-center gap-3 p-4 rounded-lg border ${
          (validations?.failedCount ?? 0) > 0
            ? "bg-amber-50 border-amber-200"
            : "bg-green-50 border-green-200"
        }`}
      >
        <i
          className={`text-xl ${
            (validations?.failedCount ?? 0) > 0
              ? "ri-error-warning-line text-amber-600"
              : "ri-checkbox-circle-line text-green-600"
          }`}
        />
        <div>
          <p
            className={`text-sm font-medium ${
              (validations?.failedCount ?? 0) > 0
                ? "text-amber-700"
                : "text-green-700"
            }`}
          >
            {validations?.failedCount
              ? "Validation Issues Found"
              : "Validation Complete"}
          </p>
          <p
            className={`text-xs ${
              (validations?.failedCount ?? 0) > 0
                ? "text-amber-600"
                : "text-green-600"
            }`}
          >
            {validations?.message ??
              "Processing validation results will appear here."}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
          <p className="text-2xl font-bold text-gray-900">
            {validations?.successCount ?? 0}
          </p>
          <p className="text-xs text-gray-500">Valid Records</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
          <p className="text-2xl font-bold text-amber-600">
            {validations?.failedCount ?? 0}
          </p>
          <p className="text-xs text-gray-500">Failed Records</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
          <p className="text-2xl font-bold text-gray-900">
            {validations?.totalRows ?? 0}
          </p>
          <p className="text-xs text-gray-500">Total Rows</p>
        </div>
      </div>

      {validations?.data?.some((item) => item.status === "FAILED") && (
        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-4 py-3 bg-gray-50 text-xs font-medium text-gray-500 uppercase">
            Failed rows
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Row
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Status
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Reason
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Data
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {validations.data
                  .filter((item) => item.status === "FAILED")
                  .map((item) => (
                    <tr key={item.id} className="hover:bg-gray-50 align-top">
                      <td className="px-4 py-3 text-sm text-gray-700">
                        {item.row}
                      </td>
                      <td className="px-4 py-3 text-sm text-amber-600 font-medium">
                        {item.status}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700 max-w-[260px]">
                        {item.reason}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">
                        <div className="space-y-1">
                          {Object.entries(item.data ?? {}).map(
                            ([key, value]) => (
                              <div key={key} className="flex gap-2 text-xs">
                                <span className="font-medium text-gray-500">
                                  {key}:
                                </span>
                                <span>
                                  {value === null || value === undefined
                                    ? "—"
                                    : String(value)}
                                </span>
                              </div>
                            ),
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
