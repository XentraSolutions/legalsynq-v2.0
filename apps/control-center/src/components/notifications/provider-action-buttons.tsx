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
  const [testSt,     setTestSt]        = useState<BtnState>('idle');
  const [toggleSt,   setToggleSt]      = useState<BtnState>('idle');
  const [deleteSt,   setDeleteSt]      = useState<BtnState>('idle');
  const [confirmDel, setConfirmDel]    = useState(false);
  const [errorMsg,   setErrorMsg]      = useState<string | null>(null);

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

  const btnBase = 'inline-flex items-center gap-1 px-2.5 py-1 rounded text-[11px] font-semibold border transition-colors disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap';

  function stateIcon(s: BtnState) {
    if (s === 'loading') return <i className="ri-loader-4-line animate-spin" />;
    if (s === 'ok')      return <i className="ri-check-line text-green-600" />;
    if (s === 'err')     return <i className="ri-close-line text-red-500" />;
    return null;
  }

  const isValidated = validationStatus === 'valid';

  return (
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

        {/* Test — only available after validation */}
        <span title={!isValidated ? 'Validate the config first before testing' : undefined}>
          <button
            disabled={isPending || !isValidated}
            onClick={() => runAction(() => testProviderConfig(configId), setTestSt)}
            className={`${btnBase} bg-white text-gray-600 border-gray-300 hover:border-indigo-400 hover:text-indigo-700`}
          >
            {stateIcon(testSt) ?? <i className="ri-send-plane-line" />}
            Test
          </button>
        </span>

        {/* Activate — only shown when inactive, requires validation first */}
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
  );
}
