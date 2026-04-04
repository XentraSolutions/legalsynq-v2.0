export type ProductType = 'careconnect' | 'synqlien' | 'synqfund' | 'synqrx' | 'synqpayout';

export const PRODUCT_TYPES: ProductType[] = ['careconnect', 'synqlien', 'synqfund', 'synqrx', 'synqpayout'];

export const PRODUCT_TYPE_LABELS: Record<ProductType, string> = {
  careconnect: 'CareConnect',
  synqlien:    'SynqLien',
  synqfund:    'SynqFund',
  synqrx:      'SynqRx',
  synqpayout:  'SynqPayout',
};

export interface TenantBranding {
  id:              string;
  tenantId:        string;
  productType:     ProductType;
  brandName:       string;
  logoUrl:         string | null;
  primaryColor:    string | null;
  secondaryColor:  string | null;
  accentColor:     string | null;
  textColor:       string | null;
  backgroundColor: string | null;
  buttonRadius:    string | null;
  fontFamily:      string | null;
  emailHeaderHtml: string | null;
  emailFooterHtml: string | null;
  supportEmail:    string | null;
  supportPhone:    string | null;
  websiteUrl:      string | null;
  createdAt:       string;
  updatedAt:       string;
}

export interface BrandingListResponse {
  data: TenantBranding[];
  meta: { total: number; limit: number; offset: number };
}
