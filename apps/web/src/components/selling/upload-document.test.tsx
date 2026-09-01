import { act, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, test, vi } from 'vitest';
import SellingUploadDocument, {
  SELLING_DOCUMENT_MAX_FILE_SIZE_BYTES,
} from './upload-document';

const { useDropzoneMock } = vi.hoisted(() => ({
  useDropzoneMock: vi.fn(),
}));

vi.mock('react-dropzone', () => ({
  useDropzone: useDropzoneMock,
}));

describe('SellingUploadDocument', () => {
  beforeEach(() => {
    useDropzoneMock.mockReset();
    useDropzoneMock.mockReturnValue({
      getRootProps: () => ({}),
      getInputProps: () => ({}),
    });
  });

  test('advertises and enforces the Documents service 25 MB upload limit', async () => {
    const onUploaded = vi.fn();
    render(
      <SellingUploadDocument onUploaded={onUploaded} isMultiple={false} />,
    );
    const dropzoneOptions = useDropzoneMock.mock.calls[0][0];

    expect(screen.getByText(/max 25 MB/i)).toBeInTheDocument();
    expect(dropzoneOptions.maxSize).toBe(SELLING_DOCUMENT_MAX_FILE_SIZE_BYTES);

    act(() => {
      dropzoneOptions.onDropRejected([
        {
          file: new File(['%PDF'], 'oversized.pdf', { type: 'application/pdf' }),
          errors: [{ code: 'file-too-large', message: 'File is larger than 25 MB' }],
        },
      ]);
    });

    expect(
      screen.getByText(/This file is too large\. Maximum size is 25 MB\./i),
    ).toBeInTheDocument();
    expect(onUploaded).not.toHaveBeenCalled();
  });
});
