import type { ProductCode, ProductEntitlementSummary } from '@/types/control-center';

export interface ProductCatalogEntry {
  code: ProductCode;
  name: string;
  iconSrc: string;
  description: string;
}

export const PRODUCT_CATALOG: ProductCatalogEntry[] = [
  {
    code: 'CareConnect',
    name: 'Synq CareConnect',
    iconSrc: '/product-icons/synqconnect.png',
    description: 'Care coordination and provider network management',
  },
  {
    code: 'SynqFund',
    name: 'Synq Funds',
    iconSrc: '/product-icons/synqfund.png',
    description: 'Presettlement funding',
  },
  {
    code: 'SynqLien',
    name: 'Synq Liens',
    iconSrc: '/product-icons/synqlien.png',
    description: 'Medical lien tracking and settlement workflows',
  },
  {
    code: 'Xenia',
    name: 'Xenia',
    iconSrc: '/product-icons/synqai.png',
    description: 'Enterprise AI orchestration, agents, skills, knowledge, governance, and usage management.',
  },
  {
    code: 'SynqBill',
    name: 'Synq Bill',
    iconSrc: '/product-icons/synqbill.png',
    description: 'Billing, invoicing, and fee management',
  },
  {
    code: 'SynqRx',
    name: 'Synq Rx',
    iconSrc: '/product-icons/synqrx.png',
    description: 'Prescription and pharmacy benefit coordination',
  },
  {
    code: 'SynqPayout',
    name: 'Synq Payout',
    iconSrc: '/product-icons/synqpayout.png',
    description: 'Disbursement and payout processing',
  },
];

export function mergeTenantEntitlements(
  entitlements: ProductEntitlementSummary[],
): ProductEntitlementSummary[] {
  const byCode = new Map(entitlements.map(entitlement => [entitlement.productCode, entitlement]));

  const merged = PRODUCT_CATALOG.map(product => {
    const existing = byCode.get(product.code);
    if (existing) return existing;

    return {
      productCode: product.code,
      productName: product.name,
      enabled: false,
      status: 'Disabled',
    } satisfies ProductEntitlementSummary;
  });

  for (const entitlement of entitlements) {
    if (!merged.some(item => item.productCode === entitlement.productCode)) {
      merged.push(entitlement);
    }
  }

  return merged;
}
