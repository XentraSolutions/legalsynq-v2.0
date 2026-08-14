import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { CaseTasksTab, taskStatusGroup } from './index';

const mockDelete = jest.fn().mockResolvedValue(undefined);
const mockShowSuccess = jest.fn();
const mockTasks = [
  {
    assignedTo: 'Sarah Mitchell',
    caseId: 'case-1',
    createdAt: '08/01/2026',
    description: 'File the mechanics lien before the deadline.',
    dueDate: '10/22/2026',
    priority: 'High',
    priorityId: 'High',
    status: 'Upcoming',
    statusId: 'Upcoming',
    taskId: 'task-1',
    title: 'File Mechanics Lien',
  },
  {
    assignedTo: 'David Chen',
    caseId: 'case-1',
    createdAt: '08/01/2026',
    description: 'Archive the finalized case files.',
    dueDate: '12/01/2026',
    priority: 'Low',
    priorityId: 'Low',
    status: 'Completed',
    statusId: 'Completed',
    taskId: 'task-2',
    title: 'Archive Resolved Case Files',
  },
];

jest.mock('@/features/cases/hooks/useCaseTasks', () => ({
  useCaseTasks: () => ({
    data: mockTasks,
    isError: false,
    isLoading: false,
    refetch: jest.fn(),
  }),
  useDeleteCaseTask: () => ({ isPending: false, mutateAsync: mockDelete }),
}));

jest.mock('@/shared/hooks', () => ({
  useToast: () => ({
    showError: jest.fn(),
    showSuccess: mockShowSuccess,
  }),
}));

describe('CaseTasksTab', () => {
  beforeEach(() => jest.clearAllMocks());

  it('normalizes backend task statuses into the Figma groups', () => {
    expect(taskStatusGroup('NEW')).toBe('Upcoming');
    expect(taskStatusGroup('IN_PROGRESS')).toBe('In Progress');
    expect(taskStatusGroup('WAITING_BLOCKED')).toBe('In Review');
    expect(taskStatusGroup('COMPLETED')).toBe('Completed');
  });

  it('renders, filters, collapses, creates, and edits case tasks', () => {
    const onCreate = jest.fn();
    const onEdit = jest.fn();
    const screen = render(<CaseTasksTab caseId="case-1" onCreate={onCreate} onEdit={onEdit} />);

    expect(screen.getByTestId('case-tasks-page')).toBeTruthy();
    expect(screen.getByText('Upcoming')).toBeTruthy();
    expect(screen.getByText('Completed')).toBeTruthy();
    expect(screen.getByText('File Mechanics Lien')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Collapse Upcoming tasks'));
    expect(screen.queryByText('File Mechanics Lien')).toBeNull();
    fireEvent.press(screen.getByLabelText('Expand Upcoming tasks'));

    fireEvent.press(screen.getByLabelText('Filter tasks by status'));
    fireEvent.press(screen.getByLabelText('Filter by Completed'));
    expect(screen.queryByText('File Mechanics Lien')).toBeNull();
    expect(screen.getByText('Archive Resolved Case Files')).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Create task'));
    expect(onCreate).toHaveBeenCalledTimes(1);

    fireEvent.press(screen.getByLabelText('Manage task Archive Resolved Case Files'));
    fireEvent.press(screen.getByText('View / Edit Task'));
    expect(onEdit).toHaveBeenCalledWith('task-2');
  });

  it('confirms and deletes the selected task', async () => {
    const screen = render(<CaseTasksTab caseId="case-1" onCreate={jest.fn()} onEdit={jest.fn()} />);

    fireEvent.press(screen.getByLabelText('Manage task File Mechanics Lien'));
    fireEvent.press(screen.getByText('Delete Task'));
    expect(screen.getByText('Delete Task?')).toBeTruthy();

    fireEvent.press(screen.getByText('Yes, Delete'));

    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith('task-1'));
    expect(mockShowSuccess).toHaveBeenCalledWith('Task deleted successfully');
  });
});
