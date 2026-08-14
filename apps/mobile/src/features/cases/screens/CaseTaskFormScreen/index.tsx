import { useEffect, useState } from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  Text,
  View,
} from 'react-native';
import DateTimePicker from '@react-native-community/datetimepicker';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { CASE_TASK_STATUSES } from '@/features/cases/components/CaseTasksTab';
import {
  useCaseTask,
  useCaseTaskUsers,
  useCreateCaseTask,
  useUpdateCaseTask,
} from '@/features/cases/hooks/useCaseTasks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { SelectOptionModal, type SelectOptionItem } from '@/shared/components/SelectOptionModal';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

type TaskFormRoute = NativeStackScreenProps<MainStackParamList, 'CaseTaskForm'>['route'];
type PickerField = 'assignee' | 'priority' | 'status';

const PRIORITY_OPTIONS = ['High', 'Medium', 'Low'].map((value) => ({ label: value, value }));
const STATUS_OPTIONS = CASE_TASK_STATUSES.map((value) => ({ label: value, value }));

function parseIsoDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return year && month && day ? new Date(year, month - 1, day) : new Date();
}

function isoDate(value: Date): string {
  return [
    value.getFullYear(),
    String(value.getMonth() + 1).padStart(2, '0'),
    String(value.getDate()).padStart(2, '0'),
  ].join('-');
}

function displayDate(value: string): string {
  if (!value) return 'mm / dd / yyyy';
  const parts = value.includes('/') ? value.split('/') : value.split('-').reverse();
  if (value.includes('/')) return parts.join(' / ');
  const [day, month, year] = parts;
  return month && day && year ? `${month} / ${day} / ${year}` : value;
}

function toIsoDate(value: string): string {
  if (!value || value.includes('-')) return value;
  const [month, day, year] = value.split('/');
  return month && day && year ? `${year}-${month.padStart(2, '0')}-${day.padStart(2, '0')}` : value;
}

export function CaseTaskFormScreen() {
  const navigation = useNavigation();
  const route = useRoute<TaskFormRoute>();
  const { caseId, taskId } = route.params;
  const editing = Boolean(taskId);
  const taskQuery = useCaseTask(caseId, taskId);
  const usersQuery = useCaseTaskUsers();
  const createTask = useCreateCaseTask(caseId);
  const updateTask = useUpdateCaseTask(caseId, taskId ?? '');
  const toast = useToast();
  const [title, setTitle] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [priority, setPriority] = useState('');
  const [status, setStatus] = useState('');
  const [assignedTo, setAssignedTo] = useState('');
  const [assignedUserId, setAssignedUserId] = useState('');
  const [description, setDescription] = useState('');
  const [pickerField, setPickerField] = useState<PickerField | null>(null);
  const [dateVisible, setDateVisible] = useState(false);

  useEffect(() => {
    const task = taskQuery.data;
    if (!task) return;
    setTitle(task.title);
    setDueDate(toIsoDate(task.dueDate));
    setPriority(task.priority);
    setStatus(task.status);
    setAssignedTo(task.assignedTo);
    setDescription(task.description);
  }, [taskQuery.data]);

  useEffect(() => {
    if (!assignedTo || assignedUserId) return;
    const matchedUser = usersQuery.data?.find(
      (user) => `${user.firstName} ${user.lastName}`.trim() === assignedTo
    );
    if (matchedUser) setAssignedUserId(matchedUser.id);
  }, [assignedTo, assignedUserId, usersQuery.data]);

  const assigneeOptions = (usersQuery.data ?? []).map((user) => ({
    label: `${user.firstName} ${user.lastName}`.trim() || user.email,
    value: user.id,
  }));
  const pickerConfig: Record<
    PickerField,
    { options: SelectOptionItem[]; selectedLabel?: string; selectedValue: string; title: string }
  > = {
    assignee: {
      options: assigneeOptions,
      selectedLabel: assignedTo,
      selectedValue: assignedUserId,
      title: 'Assign To',
    },
    priority: {
      options: PRIORITY_OPTIONS,
      selectedValue: priority,
      title: 'Priority',
    },
    status: {
      options: STATUS_OPTIONS,
      selectedValue: status,
      title: 'Status',
    },
  };
  const selectedPicker = pickerField ? pickerConfig[pickerField] : null;
  const saving = createTask.isPending || updateTask.isPending;

  function selectOption(option: SelectOptionItem) {
    if (pickerField === 'priority') setPriority(option.value);
    if (pickerField === 'status') setStatus(option.value);
    if (pickerField === 'assignee') {
      setAssignedUserId(option.value);
      setAssignedTo(option.label);
    }
    setPickerField(null);
  }

  async function save() {
    if (!title.trim() || !priority || !status || !assignedTo || !description.trim()) {
      toast.showError('Task title, priority, status, assignee, and description are required.');
      return;
    }

    const input = {
      assignedTo,
      description: description.trim(),
      dueDate: dueDate || undefined,
      priority,
      status,
      title: title.trim(),
    };

    try {
      if (editing) {
        await updateTask.mutateAsync(input);
        toast.showSuccess('Task updated successfully');
      } else {
        await createTask.mutateAsync(input);
        toast.showSuccess('Task created successfully');
      }
      navigation.goBack();
    } catch (error) {
      toast.showError(
        error instanceof Error
          ? error.message
          : editing
            ? 'Unable to update task'
            : 'Unable to create task'
      );
    }
  }

  if (editing && taskQuery.isLoading) {
    return (
      <View className="flex-1 items-center justify-center bg-[#fafafa] dark:bg-[#050506]">
        <Spinner />
      </View>
    );
  }

  if (editing && (taskQuery.isError || !taskQuery.data)) {
    return (
      <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
        <Header showBack title="" onBack={() => navigation.goBack()} />
        <EmptyState
          actionLabel="Try Again"
          description="The selected task could not be loaded."
          title="Unable to load task"
          onAction={() => void taskQuery.refetch()}
        />
      </View>
    );
  }

  return (
    <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <Header showBack title="" onBack={() => navigation.goBack()} />
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        <ScrollView
          className="flex-1"
          contentContainerClassName="px-6 pb-10"
          keyboardShouldPersistTaps="handled"
        >
          <Text className="mt-2 font-jakarta-bold text-[24px] leading-8 text-[#202228] dark:text-white">
            {editing ? 'View / Edit Task' : 'Create New Task'}
          </Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#777a84] dark:text-[#a1a1aa]')}>
            {editing
              ? 'View or update the task information as needed.'
              : 'Provide the necessary information to create and assign a new task.'}
          </Text>

          <View className="mt-6 gap-5">
            <Input
              label="Task Title *"
              placeholder="Enter task title"
              value={title}
              onChangeText={setTitle}
            />
            <View>
              <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#202228] dark:text-white')}>
                Due Date
              </Text>
              <Pressable
                accessibilityLabel="Select due date"
                accessibilityRole="button"
                className="h-[52px] flex-row items-center rounded-[14px] border border-border bg-white px-4 dark:border-[#303138] dark:bg-[#191a1f]"
                onPress={() => setDateVisible(true)}
              >
                <Text
                  className={cx(
                    FIGMA_TEXT.input,
                    'flex-1',
                    dueDate
                      ? 'text-[#202228] dark:text-white'
                      : 'text-[#777a84] dark:text-[#a1a1aa]'
                  )}
                >
                  {displayDate(dueDate)}
                </Text>
                <Ionicons color="#777a84" name="calendar-outline" size={18} />
              </Pressable>
            </View>
            {(
              [
                {
                  field: 'priority',
                  label: 'Priority *',
                  placeholder: 'Select priority',
                  value: priority,
                },
                { field: 'status', label: 'Status *', placeholder: 'Select status', value: status },
                {
                  field: 'assignee',
                  label: 'Assign To *',
                  placeholder: 'Select assignee',
                  value: assignedTo,
                },
              ] as const
            ).map((field) => (
              <View key={field.field}>
                <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#202228] dark:text-white')}>
                  {field.label}
                </Text>
                <Pressable
                  accessibilityLabel={`Select ${field.field}`}
                  accessibilityRole="button"
                  className="h-[52px] flex-row items-center rounded-[14px] border border-border bg-white px-4 dark:border-[#303138] dark:bg-[#191a1f]"
                  onPress={() => setPickerField(field.field)}
                >
                  <Text
                    className={cx(
                      FIGMA_TEXT.input,
                      'flex-1',
                      field.value
                        ? 'text-[#202228] dark:text-white'
                        : 'text-[#777a84] dark:text-[#a1a1aa]'
                    )}
                    numberOfLines={1}
                  >
                    {field.value || field.placeholder}
                  </Text>
                  <Ionicons color="#777a84" name="chevron-down" size={18} />
                </Pressable>
              </View>
            ))}
            <Input
              label="Description *"
              multiline
              placeholder="Leave some description here..."
              value={description}
              onChangeText={setDescription}
            />
          </View>

          <Button
            className="mt-8"
            label={editing ? 'Save' : 'Create Task'}
            loading={saving}
            onPress={() => void save()}
          />
          <Button
            className="mt-3"
            label="Cancel"
            variant="secondary"
            onPress={() => navigation.goBack()}
          />
        </ScrollView>
      </KeyboardAvoidingView>

      <SelectOptionModal
        options={selectedPicker?.options ?? []}
        selectedLabel={selectedPicker?.selectedLabel}
        selectedValue={selectedPicker?.selectedValue}
        title={selectedPicker?.title ?? 'Select option'}
        visible={pickerField !== null}
        onClose={() => setPickerField(null)}
        onSelect={selectOption}
      />

      <Modal
        animationType="fade"
        transparent
        visible={dateVisible}
        onRequestClose={() => setDateVisible(false)}
      >
        <View className="flex-1 items-center justify-center bg-black/40 px-6">
          <Pressable className="absolute inset-0" onPress={() => setDateVisible(false)} />
          <View className="w-full rounded-[20px] bg-white p-6 dark:bg-[#191a1f]">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
              Due Date
            </Text>
            <DateTimePicker
              display={Platform.OS === 'ios' ? 'spinner' : 'calendar'}
              mode="date"
              value={parseIsoDate(dueDate)}
              onChange={(_, selectedDate) => {
                if (selectedDate) setDueDate(isoDate(selectedDate));
                if (Platform.OS !== 'ios') setDateVisible(false);
              }}
            />
            {Platform.OS === 'ios' ? (
              <Button
                className="mt-3"
                label="Done"
                size="sm"
                onPress={() => setDateVisible(false)}
              />
            ) : null}
          </View>
        </View>
      </Modal>
    </View>
  );
}
