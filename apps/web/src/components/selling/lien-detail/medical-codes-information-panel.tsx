import { formatCurrency } from "@/lib/lien-utils";
import { MedicalPricingRowDetail } from "@/types/lien-selling";
import { parsePricingRow } from "@/lib/selling/selling-detail.mapper";
import { PanelShell } from "./panel-shell";

interface LienDetailPanelProps {
  lien: MedicalPricingRowDetail[];
  onEdit?: () => void;
}

export function MedicalCodesInformationPanel({
  lien,
  onEdit,
}: LienDetailPanelProps) {
  return (
    <PanelShell title="Medical Code & Marketplace Pricing" onEdit={onEdit}>
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 tracking-wider">
                  Code / Description
                </th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 tracking-wider">
                  Billing Amount
                </th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 tracking-wider">
                  Target Ask Amount
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {lien.map((row) => {
                const data = parsePricingRow(row);
                return (
                  <tr key={row.id}>
                    <td className="px-4 py-3 text-sm text-gray-700">
                      <div className="font-medium">{data.medicalCode}</div>
                      {data.description && (
                        <div className="text-gray-500 text-xs max-w-90 block">
                          {data.description}
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(data.billingAmount)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(data.targetSaleAmount)}
                    </td>
                  </tr>
                );
              })}
              {lien.length === 0 && (
                <tr>
                  <td
                    colSpan={3}
                    className="px-4 py-6 text-center text-sm text-gray-500"
                  >
                    No record
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </PanelShell>
  );
}
