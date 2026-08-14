import axios from 'axios';

import { ConfigService } from '@/shared/services/Config';

import { attachInterceptors } from './interceptors';

export const apiClient = axios.create({
  baseURL: ConfigService.getApiBaseUrl(),
  timeout: 30000,
  headers: {
    Accept: 'application/json',
    'Content-Type': 'application/json',
  },
});

attachInterceptors(apiClient);
