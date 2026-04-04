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

export interface GlobalTemplate {
  id:              string;
  tenantId:        string | null;
  templateKey:     string;
  channel:         string;
  name:            string;
  description:     string | null;
  status:          string;
  isSystemTemplate: boolean;
  productType:     ProductType | null;
  templateScope:   string;
  editorType:      string;
  category:        string | null;
  isBrandable:     boolean;
  createdAt:       string;
  updatedAt:       string;
}

export interface GlobalTemplateVersion {
  id:                  string;
  templateId:          string;
  versionNumber:       number;
  subjectTemplate:     string | null;
  bodyTemplate:        string;
  textTemplate:        string | null;
  variablesSchemaJson: string | null;
  sampleDataJson:      string | null;
  editorJson:          string | null;
  designTokensJson:    string | null;
  layoutType:          string | null;
  status:              string;
  publishedAt:         string | null;
  createdAt:           string;
  updatedAt:           string;
}

export interface GlobalTemplateListResponse {
  data: GlobalTemplate[];
  meta: { total: number; limit: number; offset: number };
}

export interface TenantTemplate {
  id:              string;
  tenantId:        string | null;
  templateKey:     string;
  channel:         string;
  name:            string;
  description:     string | null;
  status:          string;
  isSystemTemplate: boolean;
  productType:     ProductType | null;
  templateScope:   string;
  editorType:      string;
  category:        string | null;
  isBrandable:     boolean;
  createdAt:       string;
  updatedAt:       string;
}

export interface TenantTemplateListResponse {
  data: TenantTemplate[];
  meta: { total: number; limit: number; offset: number };
}

export interface TenantTemplateVersion {
  id:                  string;
  templateId:          string;
  versionNumber:       number;
  subjectTemplate:     string | null;
  bodyTemplate:        string;
  textTemplate:        string | null;
  variablesSchemaJson: string | null;
  sampleDataJson:      string | null;
  editorJson:          string | null;
  designTokensJson:    string | null;
  layoutType:          string | null;
  status:              string;
  publishedAt:         string | null;
  createdAt:           string;
  updatedAt:           string;
}

export type OverrideStatus = 'none' | 'draft' | 'published';

export interface TemplatePreviewResult {
  templateId: string;
  versionId:  string;
  subject?:   string;
  body:       string;
  text?:      string;
}

export interface BrandedPreviewResult {
  templateId: string;
  versionId:  string;
  subject:    string;
  body:       string;
  text:       string;
  branding: {
    source:       string;
    name:         string;
    primaryColor: string;
  };
}
