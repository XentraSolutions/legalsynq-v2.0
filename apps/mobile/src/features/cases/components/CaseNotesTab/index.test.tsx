import { fireEvent, render, waitFor } from '@testing-library/react-native';

import { CaseNotesTab } from './index';

const mockAddNote = jest.fn(() => Promise.resolve());
const mockDeleteNote = jest.fn(() => Promise.resolve());
const mockShowError = jest.fn();
const mockShowSuccess = jest.fn();

const notes = [
  {
    id: 'tracking-note-1',
    caseId: 'case-1',
    authorId: 'user-2',
    authorName: 'Serena Ferrara',
    category: 'follow-up',
    content: 'Court scheduling confirmation is still pending.',
    createdAt: '2026-07-22T10:15:00Z',
    isEdited: false,
    isPinned: false,
  },
  {
    id: 'feed-note-1',
    caseId: 'case-1',
    authorId: 'user-1',
    authorName: 'John Doe',
    category: 'general',
    content: 'Lien filing approved by county clerk.',
    createdAt: '2026-07-24T14:29:00Z',
    isEdited: false,
    isPinned: false,
  },
];

jest.mock('@/features/cases/hooks', () => ({
  useAddCaseNote: () => ({ isPending: false, mutateAsync: mockAddNote }),
  useCaseNotes: () => ({
    data: notes,
    isError: false,
    isLoading: false,
    refetch: jest.fn(),
  }),
  useDeleteCaseNote: () => ({ isPending: false, mutateAsync: mockDeleteNote }),
}));

jest.mock('@/shared/hooks', () => ({
  useAuth: () => ({ user: { id: 'user-1' } }),
  useToast: () => ({
    showError: mockShowError,
    showInfo: jest.fn(),
    showSuccess: mockShowSuccess,
  }),
}));

describe('CaseNotesTab', () => {
  beforeEach(() => jest.clearAllMocks());

  it('switches between case tracking and feed notes', () => {
    const screen = render(<CaseNotesTab caseId="case-1" />);

    expect(screen.getByText('Court scheduling confirmation is still pending.')).toBeTruthy();
    expect(screen.queryByText('Lien filing approved by county clerk.')).toBeNull();
    expect(screen.queryByLabelText('Add feed note')).toBeNull();

    fireEvent.press(screen.getByText('Feeds'));

    expect(screen.getByText('Lien filing approved by county clerk.')).toBeTruthy();
    expect(screen.queryByText('Court scheduling confirmation is still pending.')).toBeNull();
    expect(screen.getByLabelText('Add feed note')).toBeTruthy();
  });

  it('adds a feed note from the inline composer', async () => {
    const screen = render(<CaseNotesTab caseId="case-1" />);
    fireEvent.press(screen.getByText('Feeds'));
    fireEvent.changeText(screen.getByLabelText('Add feed note'), 'Settlement call scheduled.');
    fireEvent.press(screen.getByLabelText('Send note'));

    await waitFor(() => {
      expect(mockAddNote).toHaveBeenCalledWith({
        category: 'general',
        content: 'Settlement call scheduled.',
      });
    });
    expect(mockShowSuccess).toHaveBeenCalledWith('Note added successfully');
  });

  it('allows the author to manage and delete a feed note', async () => {
    const screen = render(<CaseNotesTab caseId="case-1" />);
    fireEvent.press(screen.getByText('Feeds'));
    fireEvent.press(screen.getByLabelText('Manage note by John Doe'));

    expect(screen.getByText('Manage Note')).toBeTruthy();
    fireEvent.press(screen.getByText('Delete Note'));

    await waitFor(() => expect(mockDeleteNote).toHaveBeenCalledWith('feed-note-1'));
    expect(mockShowSuccess).toHaveBeenCalledWith('Note deleted successfully');
  });
});
