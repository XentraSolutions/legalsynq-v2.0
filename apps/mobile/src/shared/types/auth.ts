export interface OrganizationSummary {
  id: string;
  name: string;
  tenantId: string;
}

export interface UserSession {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  permissions: string[];
  organization: OrganizationSummary;
  tenantId: string;
}

export interface AuthState {
  user: UserSession | null;
  token: string | null;
  isAuthenticated: boolean;
}
