import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import type { RememberedTenant, RememberedTenantInput } from '@/shared/types/tenant';

function normalizeTenantCode(tenantCode: string): string {
  return tenantCode.trim().toLowerCase();
}

function createLocalTenantId(tenantCode: string): string {
  return `tenant-code:${encodeURIComponent(normalizeTenantCode(tenantCode))}`;
}

function nowIso(): string {
  return new Date().toISOString();
}

function sortTenants(tenants: RememberedTenant[]): RememberedTenant[] {
  return [...tenants].sort((left, right) => right.lastUsedAt.localeCompare(left.lastUsedAt));
}

function toRememberedTenant(
  input: RememberedTenantInput,
  existing?: RememberedTenant
): RememberedTenant {
  const tenantCode = input.tenantCode.trim();
  const fallbackName = tenantCode;

  return {
    id: existing?.id ?? input.id ?? createLocalTenantId(tenantCode),
    tenantId: input.tenantId ?? existing?.tenantId ?? null,
    tenantCode,
    tenantName: input.tenantName?.trim() || existing?.tenantName || fallbackName,
    apiEndpoint: input.apiEndpoint ?? existing?.apiEndpoint ?? null,
    isConfirmed: input.isConfirmed ?? existing?.isConfirmed ?? false,
    lastUsedAt: input.lastUsedAt ?? nowIso(),
  };
}

async function readTenants(): Promise<RememberedTenant[]> {
  const serialized = await SecureStorageService.getItem(STORAGE_KEYS.REMEMBERED_TENANTS);
  if (!serialized) {
    return [];
  }

  try {
    const parsed = JSON.parse(serialized) as RememberedTenant[];
    if (!Array.isArray(parsed)) {
      throw new Error('Remembered tenants payload is not an array');
    }

    return parsed.filter((tenant) => Boolean(tenant?.id && tenant?.tenantCode));
  } catch {
    await Promise.all([
      SecureStorageService.deleteItem(STORAGE_KEYS.REMEMBERED_TENANTS),
      SecureStorageService.deleteItem(STORAGE_KEYS.ACTIVE_TENANT_ID),
    ]);
    return [];
  }
}

async function writeTenants(tenants: RememberedTenant[]): Promise<void> {
  await SecureStorageService.setItem(
    STORAGE_KEYS.REMEMBERED_TENANTS,
    JSON.stringify(sortTenants(tenants))
  );
}

async function writeActiveTenantId(id: string): Promise<void> {
  await SecureStorageService.setItem(STORAGE_KEYS.ACTIVE_TENANT_ID, id);
}

export const TenantSelectionService = {
  normalizeTenantCode,

  async getRememberedTenants(): Promise<RememberedTenant[]> {
    return sortTenants(await readTenants());
  },

  async getActiveTenant(): Promise<RememberedTenant | null> {
    const [tenants, activeTenantId] = await Promise.all([
      readTenants(),
      SecureStorageService.getItem(STORAGE_KEYS.ACTIVE_TENANT_ID),
    ]);

    if (!tenants.length) {
      return null;
    }

    const activeTenant = tenants.find((tenant) => tenant.id === activeTenantId);
    return activeTenant ?? sortTenants(tenants)[0] ?? null;
  },

  async setActiveTenant(id: string): Promise<RememberedTenant | null> {
    const tenants = await readTenants();
    const tenant = tenants.find((item) => item.id === id);
    if (!tenant) {
      return null;
    }

    const updatedTenant = { ...tenant, lastUsedAt: nowIso() };
    const updatedTenants = tenants.map((item) => (item.id === id ? updatedTenant : item));
    await Promise.all([writeTenants(updatedTenants), writeActiveTenantId(updatedTenant.id)]);
    return updatedTenant;
  },

  async addLocalTenantCode(tenantCode: string): Promise<RememberedTenant> {
    const normalized = normalizeTenantCode(tenantCode);
    const tenants = await readTenants();
    const existing = tenants.find(
      (tenant) => normalizeTenantCode(tenant.tenantCode) === normalized
    );
    const tenant = toRememberedTenant(
      {
        tenantCode,
        isConfirmed: existing?.isConfirmed ?? false,
      },
      existing
    );
    const updatedTenants = existing
      ? tenants.map((item) => (item.id === existing.id ? tenant : item))
      : [...tenants, tenant];

    await Promise.all([writeTenants(updatedTenants), writeActiveTenantId(tenant.id)]);
    return tenant;
  },

  async upsertRememberedTenant(input: RememberedTenantInput): Promise<RememberedTenant> {
    const tenants = await readTenants();
    const normalized = normalizeTenantCode(input.tenantCode);
    const existing = tenants.find(
      (tenant) =>
        normalizeTenantCode(tenant.tenantCode) === normalized ||
        Boolean(input.tenantId && tenant.tenantId === input.tenantId)
    );
    const tenant = toRememberedTenant(
      {
        ...input,
        isConfirmed: input.isConfirmed ?? true,
      },
      existing
    );
    const updatedTenants = existing
      ? tenants.map((item) => (item.id === existing.id ? tenant : item))
      : [...tenants, tenant];

    await Promise.all([writeTenants(updatedTenants), writeActiveTenantId(tenant.id)]);
    return tenant;
  },

  async removeRememberedTenant(id: string): Promise<void> {
    const tenants = await readTenants();
    const updatedTenants = tenants.filter((tenant) => tenant.id !== id);
    const activeTenantId = await SecureStorageService.getItem(STORAGE_KEYS.ACTIVE_TENANT_ID);
    const nextActiveTenant = sortTenants(updatedTenants)[0];

    await writeTenants(updatedTenants);

    if (activeTenantId === id) {
      if (nextActiveTenant) {
        await writeActiveTenantId(nextActiveTenant.id);
      } else {
        await SecureStorageService.deleteItem(STORAGE_KEYS.ACTIVE_TENANT_ID);
      }
    }
  },

  async clearRememberedTenants(): Promise<void> {
    await Promise.all([
      SecureStorageService.deleteItem(STORAGE_KEYS.REMEMBERED_TENANTS),
      SecureStorageService.deleteItem(STORAGE_KEYS.ACTIVE_TENANT_ID),
    ]);
  },
};
