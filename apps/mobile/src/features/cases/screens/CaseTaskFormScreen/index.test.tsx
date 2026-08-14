import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { CaseTaskFormScreen } from './index';

const mockGoBack = jest.fn();
const mockCreate = jest.fn().mockResolvedValue(undefined);
const mockUpdate = jest.fn().mockResolvedValue(undefined);
const mockShowError = jest.fn();
const mockShowSuccess = jest.fn();
let mockRouteParams: { caseId: string; taskId?: string } = { caseId: 'case-1' };
const mockExistingTask = {
  assignedTo: 'Sarah Mitchell',
  caseId: 'case-1',
  createdAt: '08/01/2026',
  description: 'File the mechanics lien.',
  dueDate: '10/22/2026',
  priority: 'High',
  priorityId: 'High',
  status: 'Upcoming',
  statusId: 'Upcoming',
  taskId: 'task-1',
  title: 'File Mechanics Lien',
};

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({ goBack: mockGoBack }),
  useRoute: () => ({ params: mockRouteParams }),
}));

jest.mock('@/features/cases/hooks/useCaseTasks', () => ({
  useCaseTask: (_caseId: string, taskId?: string) => ({
    data: taskId ? mockExistingTask : undefined,
    isError: false,
    isLoading: false,
    refetch: jest.fn(),
  }),
  useCaseTaskUsers: () => ({
    data: [
      {
        email: 'sarah@example.com',
        firstName: 'Sarah',
        id: 'user-1',
        isActive: true,
        lastName: 'Mitchell',
      },
    ],
  }),
  useCreateCaseTask: () => ({ isPending: false, mutateAsync: mockCreate }),
  useUpdateCaseTask: () => ({ isPending: false, mutateAsync: mockUpdate }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({ showError: mockShowError, showSuccess: mockShowSuccess }),
}));

describe('CaseTaskFormScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockRouteParams = { caseId: 'case-1' };
  });

  it('validates and creates a case task', async () => {
    const screen = render(<CaseTaskFormScreen />);

    expect(screen.getByText('Create New Task')).toBeTruthy();
    fireEvent.changeText(screen.getByPlaceholderText('Enter task title'), 'Prepare Demand');
    fireEvent.press(screen.getByLabelText('Select priority'));
    fireEvent.press(screen.getByTestId('select-option-High'));
    fireEvent.press(screen.getByLabelText('Select status'));
    fireEvent.press(screen.getByTestId('select-option-Upcoming'));
    fireEvent.press(screen.getByLabelText('Select assignee'));
    fireEvent.press(screen.getByTestId('select-option-user-1'));
    fireEvent.changeText(
      screen.getByPlaceholderText('Leave some description here...'),
      'Prepare and send the case demand.'
    );
    fireEvent.press(screen.getByText('Create Task'));

    await waitFor(() =>
      expect(mockCreate).toHaveBeenCalledWith({
        assignedTo: 'Sarah Mitchell',
        description: 'Prepare and send the case demand.',
        dueDate: undefined,
        priority: 'High',
        status: 'Upcoming',
        title: 'Prepare Demand',
      })
    );
    expect(mockShowSuccess).toHaveBeenCalledWith('Task created successfully');
    expect(mockGoBack).toHaveBeenCalledTimes(1);
  });

  it('loads and updates an existing task', async () => {
    mockRouteParams = { caseId: 'case-1', taskId: 'task-1' };
    const screen = render(<CaseTaskFormScreen />);

    expect(screen.getByText('View / Edit Task')).toBeTruthy();
    expect(screen.getByDisplayValue('File Mechanics Lien')).toBeTruthy();
    fireEvent.changeText(screen.getByDisplayValue('File Mechanics Lien'), 'Updated Task');
    fireEvent.press(screen.getByText('Save'));

    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith({
        assignedTo: 'Sarah Mitchell',
        description: 'File the mechanics lien.',
        dueDate: '2026-10-22',
        priority: 'High',
        status: 'Upcoming',
        title: 'Updated Task',
      })
    );
    expect(mockShowSuccess).toHaveBeenCalledWith('Task updated successfully');
  });
});
