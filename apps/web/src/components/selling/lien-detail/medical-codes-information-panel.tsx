import { formatCurrency } from "@/lib/lien-utils";
import {
  LienCaseDetail,
  LienDetail,
  LienFundingCompanyDetail,
  LienStatusHistoryItem,
  MedicalCodeDetail,
} from "@/types/lien-selling";

interface LienDetailPanelProps {
  lien: MedicalCodeDetail[];
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 uppercase tracking-wide">
        {label}
      </dt>
      <dd className="mt-1 text-sm text-gray-900">{value ?? "—"}</dd>
    </div>
  );
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="border-t border-gray-100 pt-5 mt-5 first:border-0 first:pt-0 first:mt-0">
      <h3 className="text-md font-semibold mb-4">{title}</h3>
      {children}
    </section>
  );
}
export function MedicalCodesInformationPanel({ lien }: LienDetailPanelProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-6 py-5">
        <Section title="Medical Code & Marketplace Pricing">
          <div className="bg-white border border-gray-200 rounded-lg">
            <div className="col-12">
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
                    {lien.map((row) => (
                      <tr key={row.id}>
                        <td className="px-4 py-3 text-sm text-gray-700">
                          <div className="font-medium">{row.code}</div>
                          <div className="text-gray-500 text-xs truncate max-w-90 block">
                            {row.code}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-700 text-right">
                          {formatCurrency(row.billingAmount)}
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-700 text-right">
                          {formatCurrency(row.askAmount)}
                        </td>
                      </tr>
                    ))}
                    {lien.length === 0 && (
                      <tr>
                        <td
                          colSpan={6}
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
          </div>
        </Section>
      </div>
    </div>
  );
}
