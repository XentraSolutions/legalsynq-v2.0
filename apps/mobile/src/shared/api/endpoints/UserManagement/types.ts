export interface ManagedUser {
  id: string;
  tenantId: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  status?: string;
  roles: string[];
  productRoles?: string[] | null;
  groupCount?: number;
  productCount?: number;
  avatarDocumentId?: string | null;
}
