import { ActionMenu } from "@/components/lien/action-menu";
import { useEffect } from "react";

type DataMappingComponentProps = {
  template: {
    columns: string[];
    tableData: Record<string, unknown>[];
    id: string;
    batchUploadId: string;
    caseId: string;
  };
  importStatus?: "FAILED" | "PROCESSING";
  onRemoveDetails: (id: string) => void;
};
export default function DataMappingComponent({
  template,
  onRemoveDetails,
}: DataMappingComponentProps) {
  useEffect(() => {}, [template]);
  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-600">
        Imported {template.tableData.length}
      </p>
      <div className="border border-gray-200 rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead>
              <tr className="bg-gray-50">
                {template.columns.length > 0 ? (
                  template.columns.map((column) => (
                    <th
                      key={column}
                      className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase whitespace-nowrap"
                    >
                      {column}
                    </th>
                  ))
                ) : (
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    No columns available
                  </th>
                )}
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {template.columns.length > 0 ? (
                template.tableData.map((row, rowIndex) => (
                  <tr
                    key={`${rowIndex}-${JSON.stringify(row)}`}
                    className="hover:bg-gray-50"
                  >
                    {template.columns.map((column) => {
                      const value = row?.[column];
                      const previewText =
                        value === null || value === undefined
                          ? "—"
                          : String(value);

                      return (
                        <td
                          key={`${column}-${rowIndex}`}
                          className="px-4 py-3 text-sm text-gray-700 whitespace-nowrap"
                        >
                          {previewText}
                        </td>
                      );
                    })}
                    <td>
                      <ActionMenu
                        items={[
                          {
                            label: "Remove",
                            icon: "",
                            onClick: () => onRemoveDetails(row.id as string),
                          },
                        ]}
                      />
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td className="px-4 py-6 text-sm text-gray-500 text-center">
                    No data context columns available yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
