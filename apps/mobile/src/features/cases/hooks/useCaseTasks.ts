import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { TasksApi } from '@/shared/api/endpoints/Tasks';
import type { CreateCaseTaskRequest, UpdateCaseTaskRequest } from '@/shared/api/endpoints/Tasks';
import { UserManagementApi } from '@/shared/api/endpoints/UserManagement';

export const caseTaskKeys = {
  all: ['case-tasks'] as const,
  list: (caseId: string) => [...caseTaskKeys.all, 'list', caseId] as const,
  detail: (caseId: string, taskId: string) =>
    [...caseTaskKeys.all, 'detail', caseId, taskId] as const,
  users: () => [...caseTaskKeys.all, 'users'] as const,
};

export function useCaseTasks(caseId: string) {
  return useQuery({
    queryKey: caseTaskKeys.list(caseId),
    queryFn: () => TasksApi.listCaseTasks(caseId),
    enabled: Boolean(caseId),
  });
}

export function useCaseTask(caseId: string, taskId?: string) {
  return useQuery({
    queryKey: caseTaskKeys.detail(caseId, taskId ?? ''),
    queryFn: () => TasksApi.getCaseTask(caseId, taskId!),
    enabled: Boolean(caseId && taskId),
  });
}

export function useCaseTaskUsers() {
  return useQuery({
    queryKey: caseTaskKeys.users(),
    queryFn: async () => (await UserManagementApi.list()).filter((user) => user.isActive),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateCaseTask(caseId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: Omit<CreateCaseTaskRequest, 'caseId'>) =>
      TasksApi.createCaseTask({ ...input, caseId }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: caseTaskKeys.list(caseId) });
    },
  });
}

export function useUpdateCaseTask(caseId: string, taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: Omit<UpdateCaseTaskRequest, 'taskId'>) =>
      TasksApi.updateCaseTask({ ...input, taskId }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: caseTaskKeys.list(caseId) }),
        queryClient.invalidateQueries({ queryKey: caseTaskKeys.detail(caseId, taskId) }),
      ]);
    },
  });
}

export function useDeleteCaseTask(caseId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (taskId: string) => TasksApi.deleteCaseTask(taskId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: caseTaskKeys.list(caseId) });
    },
  });
}
