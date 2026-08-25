import type { ProductCode, ProductEntitlementSummary } from '@/types/control-center';

export interface ProductCatalogEntry {
  code: ProductCode;
  platformCode: string;
  name: string;
  iconSrc: string;
  description: string;
}

export const PRODUCT_CATALOG: ProductCatalogEntry[] = [
  {
    code: 'CareConnect',
    platformCode: 'SYNQ_CARECONNECT',
    name: 'Synq CareConnect',
    iconSrc: '/product-icons/synqconnect.png',
    description: 'Care coordination and provider network management',
  },
  {
    code: 'SynqFund',
    platformCode: 'SYNQ_FUND',
    name: 'Synq Funds',
    iconSrc: '/product-icons/synqfund.png',
    description: 'Presettlement funding',
  },
  {
    code: 'SynqLien',
    platformCode: 'SYNQ_LIENS',
    name: 'Synq Liens',
    iconSrc: '/product-icons/synqlien.png',
    description: 'Medical lien tracking and settlement workflows',
  },
  {
    code: 'Xenia',
    platformCode: 'SYNQ_AI',
    name: 'Xenia',
    iconSrc: '/product-icons/synqai.png',
    description: 'Tenant-aware AI assistant and agent platform',
  },
  {
    code: 'SynqBill',
    platformCode: 'SYNQ_BILL',
    name: 'Synq Bill',
    iconSrc: '/product-icons/synqbill.png',
    description: 'Billing, invoicing, and fee management',
  },
  {
    code: 'SynqRx',
    platformCode: 'SYNQ_RX',
    name: 'Synq Rx',
    iconSrc: '/product-icons/synqrx.png',
    description: 'Prescription and pharmacy benefit coordination',
  },
  {
    code: 'SynqPayout',
    platformCode: 'SYNQ_PAYOUT',
    name: 'Synq Payout',
    iconSrc: '/product-icons/synqpayout.png',
    description: 'Disbursement and payout processing',
  },
  {
    code: 'SynqSelling',
    platformCode: 'SYNQ_SELLING',
    name: 'Synq Selling',
    iconSrc: '',
    description: 'Portfolio sales, buyer engagement, and transaction workflows',
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
