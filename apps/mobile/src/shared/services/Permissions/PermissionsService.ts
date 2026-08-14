export type PermissionStatus = 'granted' | 'denied' | 'undetermined';

export const PermissionsService = {
  async getDocumentPermissionStatus(): Promise<PermissionStatus> {
    return 'undetermined';
  },
};
