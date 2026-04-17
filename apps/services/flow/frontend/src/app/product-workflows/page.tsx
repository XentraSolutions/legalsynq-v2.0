"use client";

import { useEffect, useState } from "react";
import {
  listProductWorkflows,
  type ProductSlug,
  type ProductWorkflowResponse,
} from "@/lib/api/product-workflows";

const PRODUCTS: { slug: ProductSlug; label: string }[] = [
  { slug: "synqlien", label: "SynqLien" },
  { slug: "careconnect", label: "CareConnect" },
  { slug: "synqfund", label: "SynqFund" },
];

interface ProductState {
  loading: boolean;
  rows: ProductWorkflowResponse[];
  error: string | null;
}

const EMPTY: ProductState = { loading: true, rows: [], error: null };

/**
 * LS-FLOW-MERGE-P3 — minimal validation page for product-correlated workflows.
 * Per-product calls fail independently: if the user lacks the capability
 * policy on one product, that section shows the 403 message but the others
 * still load.
 */
export default function ProductWorkflowsPage() {
  const [state, setState] = useState<Record<ProductSlug, ProductState>>({
    synqlien: EMPTY,
    careconnect: EMPTY,
    synqfund: EMPTY,
  });

  useEffect(() => {
    PRODUCTS.forEach(({ slug }) => {
      listProductWorkflows(slug)
        .then((rows) =>
          setState((prev) => ({ ...prev, [slug]: { loading: false, rows, error: null } }))
        )
        .catch((err: unknown) =>
          setState((prev) => ({
            ...prev,
            [slug]: { loading: false, rows: [], error: err instanceof Error ? err.message : String(err) },
          }))
        );
    });
  }, []);

  return (
    <main className="p-6 space-y-8">
      <header>
        <h1 className="text-2xl font-semibold">Product Workflows</h1>
        <p className="text-sm text-gray-600">
          Workflow instances linked to product-side entities, grouped by product.
          Each product section is gated by its own capability policy.
        </p>
      </header>

      {PRODUCTS.map(({ slug, label }) => {
        const s = state[slug];
        return (
          <section key={slug} className="border rounded p-4">
            <h2 className="text-lg font-medium mb-2">{label}</h2>
            {s.loading && <p className="text-sm text-gray-500">Loading…</p>}
            {s.error && (
              <p className="text-sm text-red-600">Error: {s.error}</p>
            )}
            {!s.loading && !s.error && s.rows.length === 0 && (
              <p className="text-sm text-gray-500">No workflow instances yet.</p>
            )}
            {s.rows.length > 0 && (
              <table className="w-full text-sm">
                <thead className="text-left text-gray-600">
                  <tr>
                    <th className="py-1">Source</th>
                    <th>Correlation</th>
                    <th>Status</th>
                    <th>Created</th>
                  </tr>
                </thead>
                <tbody>
                  {s.rows.map((r) => (
                    <tr key={r.id} className="border-t">
                      <td className="py-1">
                        {r.sourceEntityType}/{r.sourceEntityId}
                      </td>
                      <td>{r.correlationKey ?? "—"}</td>
                      <td>{r.status}</td>
                      <td>{new Date(r.createdAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        );
      })}
    </main>
  );
}
