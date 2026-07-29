export interface RememberedTenant {
  id: string;
  tenantId?: string | null;
  tenantCode: string;
  tenantName: string;
  apiEndpoint?: string | null;
  isConfirmed: boolean;
  lastUsedAt: string;
}

export interface RememberedTenantInput {
  id?: string;
  tenantId?: string | null;
  tenantCode: string;
  tenantName?: string | null;
  apiEndpoint?: string | null;
  isConfirmed?: boolean;
  lastUsedAt?: string;
}
