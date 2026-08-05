import { useMemo, useState, type ReactNode } from 'react';
import { Modal, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import { useCaseTasks, useDeleteCaseTask } from '@/features/cases/hooks/useCaseTasks';
import type { CaseTask } from '@/shared/api/endpoints/Tasks';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';

export const CASE_TASK_STATUSES = ['Upcoming', 'In Progress', 'In Review', 'Completed'] as const;
export type CaseTaskStatus = (typeof CASE_TASK_STATUSES)[number];
type TaskFilter = 'All' | CaseTaskStatus;

function normalized(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[\s_-]+/g, '');
}

export function taskStatusGroup(status: string): CaseTaskStatus {
  switch (normalized(status)) {
    case 'inprogress':
      return 'In Progress';
    case 'inreview':
    case 'waitingblocked':
      return 'In Review';
    case 'completed':
      return 'Completed';
    default:
      return 'Upcoming';
  }
}

function priorityStyle(priority: string): { background: string; text: string } {
  switch (normalized(priority)) {
    case 'high':
      return { background: 'bg-[#ff383c]/15', text: 'text-[#a43532]' };
    case 'medium':
      return { background: 'bg-[#f5a524]/15', text: 'text-[#855f2c]' };
    default:
      return { background: 'bg-[#17c964]/15', text: 'text-[#2b7744]' };
  }
}

function TaskItem({ task, onManage }: { task: CaseTask; onManage: () => void }) {
  const priority = priorityStyle(task.priority);

  return (
    <View className="border-b border-[#e4e4e7] py-4 last:border-b-0 dark:border-[#303138]">
      <View className="flex-row items-center gap-2">
        <Text className="flex-1 font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
          {task.title}
        </Text>
        <View className={cx('rounded-xl px-2 py-1', priority.background)}>
          <Text className={cx('font-jakarta-medium text-[12px] leading-4', priority.text)}>
            {task.priority || 'Low'}
          </Text>
        </View>
        <Pressable
          accessibilityLabel={`Manage task ${task.title}`}
          accessibilityRole="button"
          hitSlop={10}
          onPress={onManage}
        >
          <Ionicons color="#777a84" name="ellipsis-vertical" size={17} />
        </Pressable>
      </View>
      <Text
        className="mt-2 font-jakarta text-[12px] leading-4 text-[#777a84] dark:text-[#a1a1aa]"
        numberOfLines={2}
      >
        {task.description || 'No description provided.'}
      </Text>
      <View className="mt-3 flex-row items-center gap-1.5">
        <Ionicons color="#777a84" name="calendar-outline" size={15} />
        <Text className="font-jakarta text-[12px] leading-4 text-[#777a84] dark:text-[#a1a1aa]">
          {task.dueDate || 'No due date'}
        </Text>
      </View>
    </View>
  );
}

function TaskSection({ children, title }: { children: ReactNode; title: CaseTaskStatus }) {
  const [expanded, setExpanded] = useState(true);

  return (
    <View className="rounded-[20px] bg-white px-6 pb-2 pt-5 dark:bg-[#191a1f]" style={SHADOWS.sm}>
      <Pressable
        accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} ${title} tasks`}
        accessibilityRole="button"
        accessibilityState={{ expanded }}
        className="flex-row items-center gap-2 pb-2"
        onPress={() => setExpanded((value) => !value)}
      >
        <Ionicons color="#71717a" name={expanded ? 'chevron-down' : 'chevron-forward'} size={17} />
        <Text className="flex-1 font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
          {title}
        </Text>
      </Pressable>
      {expanded ? children : null}
    </View>
  );
}

function Sheet({
  children,
  visible,
  onClose,
}: {
  children: ReactNode;
  visible: boolean;
  onClose: () => void;
}) {
  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 items-center justify-center bg-black/35 px-4">
        <Pressable
          accessibilityLabel="Close dialog"
          className="absolute inset-0"
          onPress={onClose}
        />
        <View className="w-full rounded-[22px] bg-white p-6 dark:bg-[#191a1f]" style={SHADOWS.md}>
          <Pressable
            accessibilityLabel="Close dialog"
            accessibilityRole="button"
            className="absolute right-3 top-3 z-10 h-6 w-6 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#303138]"
            onPress={onClose}
          >
            <Ionicons color="#777a84" name="close" size={17} />
          </Pressable>
          {children}
        </View>
      </View>
    </Modal>
  );
}

function FilterTaskModal({
  selected,
  visible,
  onClose,
  onSelect,
}: {
  selected: TaskFilter;
  visible: boolean;
  onClose: () => void;
  onSelect: (status: TaskFilter) => void;
}) {
  const options: TaskFilter[] = ['All', ...CASE_TASK_STATUSES];
  return (
    <Sheet visible={visible} onClose={onClose}>
      <Text className="font-jakarta-medium text-[18px] leading-6 text-[#202228] dark:text-white">
        Filter Task by Status
      </Text>
      <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#777a84] dark:text-[#a1a1aa]')}>
        Select status to filter the task list.
      </Text>
      <View className="mt-5">
        {options.map((option) => (
          <Pressable
            accessibilityLabel={`Filter by ${option}`}
            accessibilityRole="button"
            className="flex-row items-center border-b border-[#e4e4e7] py-3.5 last:border-b-0 dark:border-[#303138]"
            key={option}
            onPress={() => onSelect(option)}
          >
            <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#202228] dark:text-white')}>
              {option}
            </Text>
            {selected === option ? (
              <View className="h-5 w-5 items-center justify-center rounded-md bg-[#f97332]">
                <Ionicons color="#ffffff" name="checkmark" size={14} />
              </View>
            ) : null}
          </Pressable>
        ))}
      </View>
      <Button className="mt-5" label="Cancel" variant="secondary" onPress={onClose} />
    </Sheet>
  );
}

export function CaseTasksTab({
  caseId,
  onCreate,
  onEdit,
}: {
  caseId: string;
  onCreate: () => void;
  onEdit: (taskId: string) => void;
}) {
  const tasksQuery = useCaseTasks(caseId);
  const deleteTask = useDeleteCaseTask(caseId);
  const toast = useToast();
  const [filter, setFilter] = useState<TaskFilter>('All');
  const [filterVisible, setFilterVisible] = useState(false);
  const [selectedTask, setSelectedTask] = useState<CaseTask | null>(null);
  const [deleteVisible, setDeleteVisible] = useState(false);
  const grouped = useMemo(() => {
    const result = new Map<CaseTaskStatus, CaseTask[]>(
      CASE_TASK_STATUSES.map((status) => [status, []])
    );
    (tasksQuery.data ?? []).forEach((task) => result.get(taskStatusGroup(task.status))?.push(task));
    return result;
  }, [tasksQuery.data]);
  const visibleStatuses = filter === 'All' ? CASE_TASK_STATUSES : [filter];

  async function confirmDelete() {
    if (!selectedTask) return;
    try {
      await deleteTask.mutateAsync(selectedTask.taskId);
      setDeleteVisible(false);
      setSelectedTask(null);
      toast.showSuccess('Task deleted successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to delete task');
    }
  }

  return (
    <CaseDetailTabPage testID="case-tasks-page">
      <View className="flex-row items-center gap-3">
        <Pressable
          accessibilityLabel="Filter tasks by status"
          accessibilityRole="button"
          className="h-10 flex-1 flex-row items-center rounded-xl bg-white px-3 dark:bg-[#191a1f]"
          style={SHADOWS.sm}
          onPress={() => setFilterVisible(true)}
        >
          <Ionicons color="#777a84" name="options-outline" size={16} />
          <Text className={cx(FIGMA_TEXT.body, 'ml-2 flex-1 text-[#202228] dark:text-white')}>
            {filter} (Status)
          </Text>
        </Pressable>
        <Pressable
          accessibilityLabel="Create task"
          accessibilityRole="button"
          className="h-10 w-10 items-center justify-center rounded-full bg-[#f97332]"
          onPress={onCreate}
        >
          <Ionicons color="#ffffff" name="add" size={21} />
        </Pressable>
      </View>

      {tasksQuery.isLoading ? (
        <View className="flex-1 items-center justify-center py-16">
          <Spinner />
        </View>
      ) : tasksQuery.isError ? (
        <EmptyState
          actionLabel="Try Again"
          description="The case tasks could not be loaded."
          title="Unable to load tasks"
          onAction={() => void tasksQuery.refetch()}
        />
      ) : (tasksQuery.data ?? []).length === 0 ? (
        <EmptyState
          actionLabel="Create Task"
          description="Create the first task for this case to track upcoming work."
          title="No Tasks Yet"
          onAction={onCreate}
        />
      ) : (
        <View className="mt-5 gap-3">
          {visibleStatuses.map((status) => {
            const tasks = grouped.get(status) ?? [];
            return tasks.length > 0 ? (
              <TaskSection key={status} title={status}>
                {tasks.map((task) => (
                  <TaskItem key={task.taskId} task={task} onManage={() => setSelectedTask(task)} />
                ))}
              </TaskSection>
            ) : filter !== 'All' ? (
              <EmptyState
                key={status}
                description={`Tasks marked ${status.toLowerCase()} will appear here.`}
                title={`No ${status} Tasks`}
              />
            ) : null;
          })}
        </View>
      )}

      <FilterTaskModal
        selected={filter}
        visible={filterVisible}
        onClose={() => setFilterVisible(false)}
        onSelect={(status) => {
          setFilter(status);
          setFilterVisible(false);
        }}
      />

      <Sheet
        visible={selectedTask !== null && !deleteVisible}
        onClose={() => setSelectedTask(null)}
      >
        <Text className="font-jakarta-medium text-[18px] leading-6 text-[#202228] dark:text-white">
          Manage Task
        </Text>
        <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#777a84] dark:text-[#a1a1aa]')}>
          Select an action to manage {selectedTask?.title} task.
        </Text>
        <View className="mt-5">
          <Pressable
            accessibilityRole="button"
            className="flex-row items-center border-b border-[#e4e4e7] py-4 dark:border-[#303138]"
            onPress={() => {
              if (!selectedTask) return;
              const taskId = selectedTask.taskId;
              setSelectedTask(null);
              onEdit(taskId);
            }}
          >
            <Ionicons color="#202228" name="create-outline" size={18} />
            <Text className={cx(FIGMA_TEXT.body, 'ml-3 flex-1 text-[#202228] dark:text-white')}>
              View / Edit Task
            </Text>
            <Ionicons color="#777a84" name="chevron-forward" size={18} />
          </Pressable>
          <Pressable
            accessibilityRole="button"
            className="flex-row items-center py-4"
            onPress={() => setDeleteVisible(true)}
          >
            <Ionicons color="#ff383c" name="trash-outline" size={18} />
            <Text className={cx(FIGMA_TEXT.body, 'ml-3 flex-1 text-[#ff383c]')}>Delete Task</Text>
            <Ionicons color="#777a84" name="chevron-forward" size={18} />
          </Pressable>
        </View>
        <Button
          className="mt-4"
          label="Cancel"
          variant="secondary"
          onPress={() => setSelectedTask(null)}
        />
      </Sheet>

      <Sheet
        visible={selectedTask !== null && deleteVisible}
        onClose={() => {
          setDeleteVisible(false);
          setSelectedTask(null);
        }}
      >
        <View className="h-10 w-10 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#303138]">
          <Ionicons color="#ff383c" name="trash-outline" size={19} />
        </View>
        <Text className="mt-4 font-jakarta-medium text-[18px] leading-6 text-[#202228] dark:text-white">
          Delete Task?
        </Text>
        <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#777a84] dark:text-[#a1a1aa]')}>
          Are you sure you want to delete this task, {selectedTask?.title}
        </Text>
        <Button
          className="mt-5 border-[#ff383c] bg-[#ff383c]"
          label="Yes, Delete"
          loading={deleteTask.isPending}
          onPress={() => void confirmDelete()}
        />
        <Button
          className="mt-3"
          label="Cancel"
          variant="secondary"
          onPress={() => {
            setDeleteVisible(false);
            setSelectedTask(null);
          }}
        />
      </Sheet>
    </CaseDetailTabPage>
  );
}
