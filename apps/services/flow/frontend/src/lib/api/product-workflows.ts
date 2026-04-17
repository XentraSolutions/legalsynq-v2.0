import { apiFetch } from "@/lib/api/client";

/**
 * LS-FLOW-MERGE-P3 — client for the product-facing workflow endpoints.
 * One client function per supported product. The route segment must match
 * the backend ProductWorkflowsController routes.
 */
export type ProductSlug = "synqlien" | "careconnect" | "synqfund";

export interface ProductWorkflowResponse {
  id: string;
  productKey: string;
  sourceEntityType: string;
  sourceEntityId: string;
  workflowDefinitionId: string;
  workflowInstanceTaskId: string | null;
  correlationKey: string | null;
  status: string;
  createdAt: string;
  updatedAt: string | null;
}

export async function listProductWorkflows(product: ProductSlug): Promise<ProductWorkflowResponse[]> {
  return apiFetch<ProductWorkflowResponse[]>(`/api/v1/product-workflows/${product}`);
}
