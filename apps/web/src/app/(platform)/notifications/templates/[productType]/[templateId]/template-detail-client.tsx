'use client';

import { useState, useTransition, useMemo } from 'react';
import type { GlobalTemplate, GlobalTemplateVersion, BrandedPreviewResult, ProductType } from '@/lib/notifications-shared';
import { PRODUCT_TYPE_LABELS } from '@/lib/notifications-shared';
import { previewTemplateVersion } from '../../actions';

function SafeHtmlFrame({ html, className }: { html: string; className?: string }) {
  const srcDoc = useMemo(() => {
    return `<!DOCTYPE html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="script-src 'none'; object-src 'none';"><style>body{margin:0;padding:16px;font-family:system-ui,sans-serif;font-size:14px;color:#333;}</style></head><body>${html}</body></html>`;
  }, [html]);
  return (
    <iframe
      srcDoc={srcDoc}
      sandbox=""
      className={className}
      title="Template content"
      style={{ border: 'none', width: '100%', minHeight: 200 }}
    />
  );
}

interface TemplateDetailClientProps {
  template: GlobalTemplate;
  versions: GlobalTemplateVersion[];
  productType: ProductType;
  tenantId: string;
}

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit',
    });
  } catch { return iso; }
}

const VERSION_STATUS_CLS: Record<string, string> = {
  published: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  draft:     'bg-amber-50 text-amber-700 border-amber-200',
  retired:   'bg-gray-100 text-gray-500 border-gray-200',
};

export function TemplateDetailClient({ template, versions, productType, tenantId }: TemplateDetailClientProps) {
  const [selectedVersion, setSelectedVersion] = useState<GlobalTemplateVersion | null>(null);
  const [showPreview, setShowPreview] = useState(false);
  const [previewData, setPreviewData] = useState<BrandedPreviewResult | null>(null);
  const [previewError, setPreviewError] = useState('');
  const [previewTab, setPreviewTab] = useState<'html' | 'text' | 'source'>('html');
  const [templateVars, setTemplateVars] = useState<Record<string, string>>({});
  const [pending, startT] = useTransition();

  const publishedVersion = versions.find(v => v.status === 'published');

  function parseVariables(version: GlobalTemplateVersion): string[] {
    if (version.variablesSchemaJson) {
      try {
        const schema = JSON.parse(version.variablesSchemaJson);
        if (schema && typeof schema === 'object') {
          return Object.keys(schema.properties ?? schema);
        }
      } catch { /* ignore */ }
    }
    const pattern = /\{\{(\w+(?:\.\w+)*)\}\}/g;
    const vars = new Set<string>();
    const text = [version.subjectTemplate, version.bodyTemplate, version.textTemplate].filter(Boolean).join(' ');
    let match;
    while ((match = pattern.exec(text)) !== null) {
      if (!match[1].startsWith('brand.')) vars.add(match[1]);
    }
    return Array.from(vars);
  }

  function parseSampleData(version: GlobalTemplateVersion): Record<string, string> {
    if (version.sampleDataJson) {
      try {
        const data = JSON.parse(version.sampleDataJson);
        if (data && typeof data === 'object') {
          const result: Record<string, string> = {};
          for (const [k, v] of Object.entries(data)) {
            result[k] = String(v);
          }
          return result;
        }
      } catch { /* ignore */ }
    }
    return {};
  }

  function handleViewVersion(v: GlobalTemplateVersion) {
    setSelectedVersion(v);
    setShowPreview(false);
    setPreviewData(null);
    setPreviewError('');
  }

  function handlePreviewVersion(v: GlobalTemplateVersion) {
    setSelectedVersion(v);
    setShowPreview(true);
    setPreviewData(null);
    setPreviewError('');
    const sample = parseSampleData(v);
    const vars = parseVariables(v);
    const merged: Record<string, string> = {};
    for (const key of vars) {
      merged[key] = sample[key] ?? '';
    }
    setTemplateVars(merged);
  }

  function handleRunPreview() {
    if (!selectedVersion) return;
    setPreviewError('');
    startT(async () => {
      const tplData: Record<string, unknown> = {};
      for (const [k, v] of Object.entries(templateVars)) {
        if (v.trim()) tplData[k] = v.trim();
      }
      const result = await previewTemplateVersion(
        template.id,
        selectedVersion.id,
        productType,
        tplData,
      );
      if (result.success) {
        setPreviewData(result.data);
        setPreviewTab('html');
      } else {
        setPreviewError(result.error);
      }
    });
  }

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <div className="flex items-start justify-between mb-4">
          <div>
            <h1 className="text-xl font-bold text-gray-900">{template.name}</h1>
            {template.description && (
              <p className="text-sm text-gray-500 mt-1">{template.description}</p>
            )}
          </div>
          <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
            {PRODUCT_TYPE_LABELS[productType]}
          </span>
        </div>

        <dl className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm">
          <div>
            <dt className="text-xs text-gray-400 font-medium">Template Key</dt>
            <dd className="text-gray-700 font-mono text-xs mt-0.5">{template.templateKey}</dd>
          </div>
          <div>
            <dt className="text-xs text-gray-400 font-medium">Channel</dt>
            <dd className="text-gray-700 capitalize mt-0.5">{template.channel}</dd>
          </div>
          <div>
            <dt className="text-xs text-gray-400 font-medium">Category</dt>
            <dd className="text-gray-700 mt-0.5">{template.category ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs text-gray-400 font-medium">Branded</dt>
            <dd className="mt-0.5">
              {template.isBrandable ? (
                <span className="text-emerald-600 flex items-center gap-1"><i className="ri-check-line" /> Yes</span>
              ) : (
                <span className="text-gray-400">No</span>
              )}
            </dd>
          </div>
        </dl>

        <div className="mt-4 rounded-md bg-gray-50 border border-gray-200 px-4 py-2.5">
          <p className="text-xs text-gray-500">
            <i className="ri-lock-line mr-1" />
            This template is managed by the platform. You can view and preview it, but changes are
            made by the platform team.
          </p>
        </div>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-100">
          <h2 className="text-sm font-semibold text-gray-700">Versions</h2>
        </div>

        {versions.length === 0 ? (
          <div className="px-5 py-12 text-center">
            <i className="ri-file-list-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">No versions available yet.</p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Version</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Status</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Subject</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Created</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Published</th>
                <th className="px-5 py-2.5 text-right text-[11px] font-semibold uppercase tracking-wide text-gray-400">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {versions.map(v => {
                const statusCls = VERSION_STATUS_CLS[v.status] ?? 'bg-gray-100 text-gray-500 border-gray-200';
                const isCurrent = v.status === 'published';
                return (
                  <tr key={v.id} className={`transition-colors ${isCurrent ? 'bg-emerald-50/30' : 'hover:bg-gray-50'}`}>
                    <td className="px-5 py-3 text-sm font-semibold text-gray-800">
                      v{v.versionNumber}
                      {isCurrent && <span className="ml-2 text-[10px] text-emerald-600 font-medium">(current)</span>}
                    </td>
                    <td className="px-5 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide border ${statusCls}`}>
                        {v.status}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-600 truncate max-w-[200px]">
                      {v.subjectTemplate || '—'}
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">{fmtDate(v.createdAt)}</td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">{v.publishedAt ? fmtDate(v.publishedAt) : '—'}</td>
                    <td className="px-5 py-3 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => handleViewVersion(v)}
                          className="text-xs text-gray-500 hover:text-gray-700 font-medium"
                        >
                          View
                        </button>
                        {template.isBrandable && (
                          <button
                            type="button"
                            onClick={() => handlePreviewVersion(v)}
                            className="text-xs text-indigo-600 hover:text-indigo-500 font-medium"
                          >
                            Preview
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {selectedVersion && !showPreview && (
        <div className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">
              Version {selectedVersion.versionNumber} — Content
            </h2>
            <button
              type="button"
              onClick={() => setSelectedVersion(null)}
              className="text-xs text-gray-400 hover:text-gray-600"
            >
              <i className="ri-close-line text-base" />
            </button>
          </div>

          {selectedVersion.subjectTemplate && (
            <div className="bg-gray-50 rounded-lg px-4 py-3">
              <span className="text-[11px] text-gray-400 font-medium">Subject</span>
              <p className="text-sm text-gray-800 font-medium mt-0.5">{selectedVersion.subjectTemplate}</p>
            </div>
          )}

          {selectedVersion.bodyTemplate && (
            <div>
              <span className="text-[11px] text-gray-400 font-medium mb-2 block">HTML Content</span>
              <div className="border border-gray-200 rounded-lg bg-white max-h-[400px] overflow-hidden">
                <SafeHtmlFrame html={selectedVersion.bodyTemplate} className="rounded-lg" />
              </div>
            </div>
          )}

          {selectedVersion.textTemplate && (
            <div>
              <span className="text-[11px] text-gray-400 font-medium mb-2 block">Plain Text</span>
              <pre className="border border-gray-200 rounded-lg p-4 text-sm text-gray-700 whitespace-pre-wrap bg-gray-50 max-h-[300px] overflow-y-auto">
                {selectedVersion.textTemplate}
              </pre>
            </div>
          )}

          {parseVariables(selectedVersion).length > 0 && (
            <div>
              <span className="text-[11px] text-gray-400 font-medium mb-2 block">Template Variables</span>
              <div className="flex flex-wrap gap-2">
                {parseVariables(selectedVersion).map(v => (
                  <span key={v} className="inline-flex items-center px-2 py-1 rounded bg-gray-100 text-xs text-gray-600 font-mono">
                    {`{{${v}}}`}
                  </span>
                ))}
              </div>
            </div>
          )}

          <div className="rounded-md bg-gray-50 border border-gray-200 px-4 py-2.5">
            <p className="text-xs text-gray-500">
              <i className="ri-information-line mr-1" />
              Template content is managed by the platform team and cannot be modified.
            </p>
          </div>
        </div>
      )}

      {selectedVersion && showPreview && (
        <div className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-700">
              Branded Preview — v{selectedVersion.versionNumber}
            </h2>
            <button
              type="button"
              onClick={() => { setShowPreview(false); setSelectedVersion(null); setPreviewData(null); }}
              className="text-xs text-gray-400 hover:text-gray-600"
            >
              <i className="ri-close-line text-base" />
            </button>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="space-y-3">
              <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wide">Sample Data</h3>
              {Object.keys(templateVars).length === 0 ? (
                <p className="text-xs text-gray-400 italic">No template variables detected.</p>
              ) : (
                <div className="space-y-2">
                  {Object.entries(templateVars).map(([key, val]) => (
                    <div key={key}>
                      <label className="block text-xs text-gray-500 font-mono mb-0.5">{`{{${key}}}`}</label>
                      <input
                        type="text"
                        value={val}
                        onChange={e => setTemplateVars(prev => ({ ...prev, [key]: e.target.value }))}
                        className="w-full rounded-md border border-gray-300 px-2.5 py-1.5 text-sm text-gray-900 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
                        placeholder={`Value for ${key}`}
                      />
                    </div>
                  ))}
                </div>
              )}

              <button
                type="button"
                onClick={handleRunPreview}
                disabled={pending}
                className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 transition-colors disabled:opacity-50"
              >
                {pending && <i className="ri-loader-4-line animate-spin" />}
                Render Preview
              </button>
            </div>

            <div className="lg:col-span-2 space-y-3">
              {previewError && (
                <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                  <i className="ri-error-warning-line mr-1.5" />
                  {previewError}
                </div>
              )}

              {previewData && (
                <>
                  <div className="flex items-center gap-4 border-b border-gray-100 pb-2">
                    <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Result</h3>
                    {previewData.branding && (
                      <div className="flex items-center gap-2 text-[11px] text-gray-500">
                        {previewData.branding.primaryColor && (
                          <span className="flex items-center gap-1">
                            <span className="w-3 h-3 rounded-full border border-gray-200" style={{ backgroundColor: previewData.branding.primaryColor }} />
                          </span>
                        )}
                        <span className="font-medium">{previewData.branding.name}</span>
                        <span className="italic">({previewData.branding.source})</span>
                      </div>
                    )}
                  </div>

                  {previewData.subject && (
                    <div className="bg-gray-50 rounded-lg px-3 py-2">
                      <span className="text-[11px] text-gray-400 font-medium">Subject</span>
                      <p className="text-sm text-gray-800 font-medium mt-0.5">{previewData.subject}</p>
                    </div>
                  )}

                  <div className="flex items-center gap-1 border-b border-gray-100">
                    {(['html', 'text', 'source'] as const).map(t => (
                      <button
                        key={t}
                        type="button"
                        onClick={() => setPreviewTab(t)}
                        className={`px-3 py-1.5 text-xs font-medium capitalize transition-colors ${
                          previewTab === t
                            ? 'text-indigo-700 border-b-2 border-indigo-600'
                            : 'text-gray-400 hover:text-gray-600'
                        }`}
                      >
                        {t === 'source' ? 'HTML Source' : t === 'html' ? 'HTML Preview' : 'Text'}
                      </button>
                    ))}
                  </div>

                  {previewTab === 'html' && previewData.body && (
                    <div className="border border-gray-200 rounded-lg bg-white max-h-[500px] overflow-hidden">
                      <SafeHtmlFrame html={previewData.body} className="rounded-lg" />
                    </div>
                  )}
                  {previewTab === 'text' && (
                    <pre className="border border-gray-200 rounded-lg p-4 text-sm text-gray-700 whitespace-pre-wrap bg-gray-50 max-h-[500px] overflow-y-auto">
                      {previewData.text || '(no text version)'}
                    </pre>
                  )}
                  {previewTab === 'source' && (
                    <pre className="border border-gray-200 rounded-lg p-4 text-[11px] text-gray-600 font-mono whitespace-pre-wrap bg-gray-50 max-h-[500px] overflow-y-auto">
                      {previewData.body || '(empty)'}
                    </pre>
                  )}

                  {previewData.branding?.source === 'system_defaults' && (
                    <div className="rounded-md bg-amber-50 border border-amber-200 px-3 py-2">
                      <p className="text-xs text-amber-700">
                        <i className="ri-information-line mr-1" />
                        Default branding applied. Set up your branding profile in{' '}
                        <a href="/notifications/branding" className="underline font-medium">Notification Branding</a>{' '}
                        to personalise your notifications.
                      </p>
                    </div>
                  )}
                </>
              )}

              {!previewData && !previewError && (
                <div className="py-12 text-center">
                  <i className="ri-eye-line text-3xl text-gray-300" />
                  <p className="mt-2 text-sm text-gray-400">
                    Click &ldquo;Render Preview&rdquo; to see how this template looks with your branding.
                  </p>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
