import { appendCareConnectMessageFiles, type SelectedCareConnectMessageFile } from '@/lib/careconnect-message-attachments';
import type { ReferralComment } from '@/types/careconnect';

export interface PostPublicThreadCommentResult {
  success: boolean;
  error?: string;
  comment?: ReferralComment;
}

export async function postPublicThreadComment(
  token: string,
  senderType: 'referrer' | 'provider',
  message: string,
  files: SelectedCareConnectMessageFile[] = [],
): Promise<PostPublicThreadCommentResult> {
  const trimmed = message.trim();
  if (!trimmed && files.length === 0) {
    return { success: false, error: 'Enter a message or attach at least one file.' };
  }

  if (trimmed.length > 4000) {
    return { success: false, error: 'Message must be 4000 characters or fewer.' };
  }

  const url = `/api/public/careconnect/api/public/referrals/thread/comments?token=${encodeURIComponent(token)}`;
  const init: RequestInit = files.length > 0
    ? {
        method: 'POST',
        body: buildForm(senderType, trimmed, files),
      }
    : {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ senderType, message: trimmed }),
      };

  try {
    const resp = await fetch(url, init);
    if (!resp.ok) {
      const body = await resp.json().catch(() => ({}));
      return {
        success: false,
        error: (body as { error?: string; detail?: string; message?: string }).error
          ?? (body as { detail?: string }).detail
          ?? (body as { message?: string }).message
          ?? 'Failed to send message. Please try again.',
      };
    }

    const comment = await resp.json() as ReferralComment;
    return { success: true, comment };
  } catch {
    return { success: false, error: 'Network error. Please check your connection and try again.' };
  }
}

function buildForm(
  senderType: 'referrer' | 'provider',
  message: string,
  files: SelectedCareConnectMessageFile[],
): FormData {
  const form = new FormData();
  form.append('senderType', senderType);
  form.append('message', message);
  appendCareConnectMessageFiles(form, files);
  return form;
}
