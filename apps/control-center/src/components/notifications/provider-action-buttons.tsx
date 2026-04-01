'use client';

import { useTransition, useState } from 'react';
import {
  validateProviderConfig,
  testProviderConfig,
  activateProviderConfig,
  deleteProviderConfig,
} from '@/app/notifications/actions';

interface Props {
  configId:         string;
  status:           'active' | 'inactive';
  validationStatus: 'not_validated' | 'valid' | 'invalid';
}

type BtnState = 'idle' | 'loading' | 'ok' | 'err';

export function ProviderActionButtons({ configId, status, validationStatus }: Props) {
  const [isPending,  startTransition]  = useTransition();
  const [validateSt, setValidateSt]    = useState<BtnState>('idle');
  const [toggleSt,   setToggleSt]      = useState<BtnState>('idle');
  const [deleteSt,   setDeleteSt]      = useState<BtnState>('idle');
  const [confirmDel, setConfirmDel]    = useState(false);
  const [errorMsg,   setErrorMsg]      = useState<string | null>(null);

  const [testOpen,    setTestOpen]    = useState(false);
  const [testEmail,   setTestEmail]   = useState('');
  const [testSubject, setTestSubject] = useState('LegalSynq — Test Email');
  const [testBody,    setTestBody]    = useState('This is a test email from the LegalSynq Notifications platform.');
  const [testSt,      setTestSt]      = useState<BtnState>('idle');
  const [testMsg,     setTestMsg]     = useState<string | null>(null);

  function runAction(
    fn:    () => Promise<{ success: boolean; error?: string }>,
    setSt: (s: BtnState) => void,
  ) {
    setErrorMsg(null);
    setSt('loading');
    startTransition(async () => {
      const res = await fn();
      if (res.success) {
        setSt('ok');
        setTimeout(() => setSt('idle'), 3000);
      } else {
        setSt('err');
        setErrorMsg(res.error ?? 'Action failed.');
        setTimeout(() => setSt('idle'), 4000);
      }
    });
  }

  function handleSendTest(e: React.FormEvent) {
    e.preventDefault();
    if (!testEmail.trim()) return;
    setTestMsg(null);
    setTestSt('loading');
    startTransition(async () => {
      const res = await testProviderConfig(configId, {
        toEmail: testEmail.trim(),
        subject: testSubject.trim() || 'LegalSynq — Test Email',
        body:    testBody.trim()    || 'This is a test email from the LegalSynq Notifications platform.',
      });
      if (res.success) {
        setTestSt('ok');
        setTestMsg(res.message ?? 'Test email sent successfully.');
      } else {
        setTestSt('err');
        setTestMsg(res.error ?? 'Test failed.');
      }
    });
  }

  function closeTestModal() {
    setTestOpen(false);
    setTestEmail('');
    setTestSubject('LegalSynq — Test Email');
    setTestBody('This is a test email from the LegalSynq Notifications platform.');
    setTestSt('idle');
    setTestMsg(null);
  }

  const btnBase = 'inline-flex items-center gap-1 px-2.5 py-1 rounded text-[11px] font-semibold border transition-colors disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap';

  function stateIcon(s: BtnState) {
    if (s === 'loading') return <i className="ri-loader-4-line animate-spin" />;
    if (s === 'ok')      return <i className="ri-check-line text-green-600" />;
    if (s === 'err')     return <i className="ri-close-line text-red-500" />;
    return null;
  }

  const isValidated = validationStatus === 'valid';
  const inputCls    = 'block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none';

  return (
    <>
      <div className="flex flex-col gap-1.5">
        <div className="flex gap-1.5 flex-wrap">

          {/* Validate */}
          <button
            disabled={isPending}
            onClick={() => runAction(() => validateProviderConfig(configId), setValidateSt)}
            className={`${btnBase} bg-white text-gray-600 border-gray-300 hover:border-blue-400 hover:text-blue-700`}
          >
            {stateIcon(validateSt) ?? <i className="ri-shield-check-line" />}
            Validate
          </button>

          {/* Test — opens modal */}
          <span title={!isValidated ? 'Validate the config first before testing' : undefined}>
            <button
              disabled={isPending || !isValidated}
              onClick={() => setTestOpen(true)}
              className={`${btnBase} bg-white text-gray-600 border-gray-300 hover:border-indigo-400 hover:text-indigo-700`}
            >
              <i className="ri-send-plane-line" />
              Test
            </button>
          </span>

          {/* Logs */}
          <a
            href={`/notifications/providers/${configId}/logs`}
            className={`${btnBase} bg-white text-gray-600 border-gray-300 hover:border-violet-400 hover:text-violet-700`}
          >
            <i className="ri-file-list-3-line" />
            Logs
          </a>

          {/* Activate — only shown when inactive */}
          {status === 'inactive' && (
            <span title={!isValidated ? 'Validate the config first before activating' : undefined}>
              <button
                disabled={isPending || !isValidated}
                onClick={() => runAction(() => activateProviderConfig(configId), setToggleSt)}
                className={`${btnBase} bg-green-50 text-green-700 border-green-300 hover:border-green-500`}
              >
                {stateIcon(toggleSt) ?? <i className="ri-toggle-line" />}
                Activate
              </button>
            </span>
          )}

          {/* Delete */}
          {!confirmDel ? (
            <button
              disabled={isPending}
              onClick={() => setConfirmDel(true)}
              className={`${btnBase} bg-white text-red-500 border-red-200 hover:border-red-400 hover:bg-red-50`}
              title="Delete this provider config"
            >
              <i className="ri-delete-bin-line" />
            </button>
          ) : (
            <>
              <button
                disabled={isPending}
                onClick={() => runAction(() => deleteProviderConfig(configId), setDeleteSt)}
                className={`${btnBase} bg-red-600 text-white border-red-600 hover:bg-red-700`}
              >
                {stateIcon(deleteSt) ?? <i className="ri-check-line" />}
                Confirm
              </button>
              <button
                disabled={isPending}
                onClick={() => setConfirmDel(false)}
                className={`${btnBase} bg-white text-gray-500 border-gray-300 hover:bg-gray-50`}
              >
                Cancel
              </button>
            </>
          )}
        </div>

        {errorMsg && (
          <p className="text-[11px] text-red-600 bg-red-50 border border-red-200 rounded px-2 py-1">
            {errorMsg}
          </p>
        )}
      </div>

      {/* ── Test Email Modal ──────────────────────────────────────────────── */}
      {testOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-md">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <div>
                <h2 className="text-sm font-semibold text-gray-900">Send Test Email</h2>
                <p className="text-[11px] text-gray-500 mt-0.5">
                  A real email will be sent via this provider config.
                </p>
              </div>
              <button onClick={closeTestModal} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-lg" />
              </button>
            </div>

            <form onSubmit={handleSendTest} className="px-5 py-4 space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Recipient Email <span className="text-red-500">*</span>
                </label>
                <input
                  type="email"
                  value={testEmail}
                  onChange={e => setTestEmail(e.target.value)}
                  placeholder="you@example.com"
                  required
                  autoFocus
                  className={inputCls}
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Subject</label>
                <input
                  type="text"
                  value={testSubject}
                  onChange={e => setTestSubject(e.target.value)}
                  placeholder="LegalSynq — Test Email"
                  className={inputCls}
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Body</label>
                <textarea
                  value={testBody}
                  onChange={e => setTestBody(e.target.value)}
                  rows={4}
                  placeholder="Test email body…"
                  className={`${inputCls} resize-none`}
                />
              </div>

              {testMsg && (
                <p className={`text-xs rounded px-3 py-2 border ${
                  testSt === 'ok'
                    ? 'text-green-700 bg-green-50 border-green-200'
                    : 'text-red-600 bg-red-50 border-red-200'
                }`}>
                  {testSt === 'ok' ? <><i className="ri-check-line mr-1" />{testMsg}</> : testMsg}
                </p>
              )}

              <div className="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  onClick={closeTestModal}
                  className="px-3 py-1.5 rounded-md text-sm text-gray-600 hover:bg-gray-100 transition-colors"
                >
                  Close
                </button>
                <button
                  type="submit"
                  disabled={isPending || testSt === 'loading'}
                  className="inline-flex items-center gap-1.5 px-4 py-1.5 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
                >
                  {testSt === 'loading'
                    ? <><i className="ri-loader-4-line animate-spin" /> Sending…</>
                    : <><i className="ri-send-plane-line" /> Send Test</>
                  }
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
