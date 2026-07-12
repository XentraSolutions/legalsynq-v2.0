'use client';

import { useEffect, useRef, useState, useTransition } from 'react';
import { ApiError, apiClient } from '@/lib/api-client';
import { XeniaProductShell } from '../xenia-product-shell';

type XeniaMessageRole = 'User' | 'Assistant' | 'System';

interface XeniaConversationMessage {
  messageId: string;
  role: XeniaMessageRole;
  content: string;
  actionLabel: string | null;
  productCode: string | null;
  createdAtUtc: string;
}

interface XeniaConversation {
  conversationId: string;
  tenantId: string;
  createdByUserId: string;
  title: string;
  activationSource: string;
  productCode: string | null;
  sourceReference: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  messages: XeniaConversationMessage[];
}

interface XeniaConversationTurn {
  conversation: XeniaConversation;
  userMessage: XeniaConversationMessage;
  assistantMessage: XeniaConversationMessage;
  outputChunks: string[];
  usage: {
    requestCount: number;
    promptTokens: number;
    completionTokens: number;
    estimatedCostUsd: number;
  };
}

interface XeniaTenantConfigurationSummary {
  enabled: boolean;
  deploymentModel: 'Managed' | 'BringYourOwnAI';
  defaultModel: string;
  failoverEnabled: boolean;
}

function formatTimestamp(value: string) {
  try {
    return new Date(value).toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return value;
  }
}

function deriveConversationTitle(content: string) {
  const normalized = content.replace(/\s+/g, ' ').trim();
  if (!normalized) return 'New Xenia conversation';
  return normalized.length > 56 ? `${normalized.slice(0, 53)}...` : normalized;
}

function summarizeConversation(conversation: XeniaConversation) {
  const latestMessage = conversation.messages[conversation.messages.length - 1];
  if (!latestMessage) return 'No messages yet';

  const normalized = latestMessage.content.replace(/\s+/g, ' ').trim();
  return normalized.length > 88 ? `${normalized.slice(0, 85)}...` : normalized;
}

function emptyStatePrompts() {
  return [
    'Summarize this lien file and list missing documents.',
    'Draft a tenant-safe follow-up message for this case.',
    'Explain the next operational risks in this workflow.',
  ];
}

export function XeniaWorkspaceClient({
  sessionEmail,
  tenantCode,
}: {
  sessionEmail: string;
  tenantCode: string;
}) {
  const [conversations, setConversations] = useState<XeniaConversation[]>([]);
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const [configuration, setConfiguration] = useState<XeniaTenantConfigurationSummary | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [composerError, setComposerError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  const activeConversation = conversations.find((item) => item.conversationId === activeConversationId) ?? null;

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [conversationResponse, configurationResponse] = await Promise.all([
          apiClient.get<XeniaConversation[]>('/xenia/conversations'),
          apiClient.get<XeniaTenantConfigurationSummary>('/xenia/tenant/configuration').catch(() => null),
        ]);
        if (cancelled) return;

        const nextConversations = conversationResponse.data;
        setConversations(nextConversations);
        setActiveConversationId(current => current ?? nextConversations[0]?.conversationId ?? null);
        setConfiguration(configurationResponse?.data ?? null);
        setLoadError(null);
      } catch (error) {
        if (cancelled) return;
        setLoadError(error instanceof ApiError ? error.message : 'Unable to load Xenia conversations right now.');
      } finally {
        if (!cancelled) setIsBootstrapping(false);
      }
    }

    void load();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [activeConversationId, conversations]);

  const handleNewConversation = () => {
    setActiveConversationId(null);
    setComposerError(null);
    setDraft('');
  };

  const handlePromptShortcut = (prompt: string) => {
    setDraft(prompt);
    setComposerError(null);
  };

  const handleSubmit = () => {
    const content = draft.trim();
    if (!content || isPending) return;

    setComposerError(null);

    startTransition(async () => {
      try {
        let conversationId = activeConversationId;

        if (!conversationId) {
          const created = await apiClient.post<XeniaConversation>('/xenia/conversations', {
            title: deriveConversationTitle(content),
            activationSource: 'UserClick',
            productCode: 'XENIA',
            sourceReference: null,
            initialMessage: null,
          });

          conversationId = created.data.conversationId;

          setConversations(current => [created.data, ...current]);
          setActiveConversationId(conversationId);
        }

        const turn = await apiClient.post<XeniaConversationTurn>(`/xenia/conversations/${conversationId}/messages`, {
          content,
          actionLabel: 'Send with Xenia',
          productCode: 'XENIA',
        });

        setConversations(current => {
          const filtered = current.filter(item => item.conversationId !== turn.data.conversation.conversationId);
          return [turn.data.conversation, ...filtered];
        });
        setActiveConversationId(turn.data.conversation.conversationId);
        setDraft('');
      } catch (error) {
        setComposerError(error instanceof ApiError ? error.message : 'Xenia could not send that message.');
      }
    });
  };

  const messageCount = activeConversation?.messages.length ?? 0;
  const isXeniaEnabled = configuration?.enabled ?? true;
  const modeLabel = configuration?.deploymentModel ?? 'Managed';
  const modelLabel = configuration?.defaultModel ?? 'Not configured';

  return (
    <XeniaProductShell
      eyebrow="Xenia Workspace"
      title="Tenant AI conversations"
      description="Use Xenia inside the tenant portal to work through summaries, drafts, and operational reasoning without directly mutating business data."
    >
      <div className="grid gap-6 xl:grid-cols-[320px_minmax(0,1fr)] xl:items-stretch">
        <aside className="overflow-hidden rounded-[28px] border border-slate-200/80 bg-white/90 shadow-lg shadow-slate-200/40 backdrop-blur xl:flex xl:h-[calc(100vh-3.5rem)] xl:min-h-[64rem] xl:flex-col">
          <div className="border-b border-slate-200/80 px-5 py-5">
            <div className="flex items-center justify-between gap-3">
              <div>
                <div className="inline-flex items-center gap-2 rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-700">
                  <i className="ri-robot-line" />
                  Xenia
                </div>
                <h1 className="mt-3 text-xl font-semibold tracking-tight text-slate-950">Workspace</h1>
                <p className="mt-1 text-sm text-slate-600">Tenant-aware AI conversations for LegalSynq workflows.</p>
              </div>
              <button
                type="button"
                onClick={handleNewConversation}
                className="inline-flex h-11 w-11 items-center justify-center rounded-2xl border border-slate-200 bg-white text-slate-700 transition hover:border-amber-300 hover:text-amber-700"
                aria-label="Start new conversation"
              >
                <i className="ri-add-line text-lg" />
              </button>
            </div>

            <div className="mt-5 grid gap-3">
              <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Session</p>
                <p className="mt-2 text-sm font-medium text-slate-900">{sessionEmail}</p>
              </div>
              <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Tenant</p>
                <p className="mt-2 text-sm font-medium text-slate-900">{tenantCode}</p>
              </div>
            </div>
          </div>

          <div className="px-3 py-3 xl:min-h-0 xl:flex-1 xl:overflow-y-auto">
            <div className="mb-3 flex items-center justify-between px-2">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Conversations</p>
              <p className="text-xs text-slate-400">{conversations.length}</p>
            </div>

            {isBootstrapping ? (
              <div className="space-y-2 px-2 pb-3">
                {[0, 1, 2].map((item) => (
                  <div key={item} className="animate-pulse rounded-2xl border border-slate-100 bg-slate-50 px-4 py-4">
                    <div className="h-4 w-32 rounded bg-slate-200" />
                    <div className="mt-3 h-3 w-full rounded bg-slate-200" />
                    <div className="mt-2 h-3 w-24 rounded bg-slate-200" />
                  </div>
                ))}
              </div>
            ) : loadError ? (
              <div className="mx-2 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {loadError}
              </div>
            ) : conversations.length === 0 ? (
              <div className="mx-2 rounded-2xl border border-dashed border-slate-200 bg-slate-50 px-4 py-8 text-center text-sm text-slate-500">
                No conversations yet. Start one from the composer.
              </div>
            ) : (
              <div className="space-y-2">
                {conversations.map((conversation) => {
                  const selected = conversation.conversationId === activeConversationId;

                  return (
                    <button
                      key={conversation.conversationId}
                      type="button"
                      onClick={() => {
                        setActiveConversationId(conversation.conversationId);
                        setComposerError(null);
                      }}
                      className={`block w-full rounded-2xl border px-4 py-4 text-left transition ${
                        selected
                          ? 'border-amber-300 bg-amber-50/70 shadow-sm'
                          : 'border-transparent bg-white hover:border-slate-200 hover:bg-slate-50'
                      }`}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <p className="line-clamp-2 text-sm font-semibold text-slate-900">{conversation.title}</p>
                        <span className="whitespace-nowrap text-[11px] text-slate-400">
                          {formatTimestamp(conversation.updatedAtUtc)}
                        </span>
                      </div>
                      <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-600">
                        {summarizeConversation(conversation)}
                      </p>
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        </aside>

        <section className="flex h-[calc(100vh-3.5rem)] min-h-[64rem] flex-col overflow-hidden rounded-[30px] border border-slate-200/80 bg-white/90 shadow-lg shadow-slate-200/40 backdrop-blur">
          <div className="border-b border-slate-200/80 px-6 py-6">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div className="max-w-3xl">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-700">Explicit and reviewable</p>
                <h2 className="mt-2 text-3xl font-semibold tracking-tight text-slate-950">
                  {activeConversation ? activeConversation.title : 'Start a new Xenia conversation'}
                </h2>
                <p className="mt-3 text-sm leading-7 text-slate-600">
                  Use Xenia to summarize cases, draft operational responses, or inspect workflow gaps without changing business data automatically.
                </p>
              </div>

              <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3">
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Messages</p>
                  <p className="mt-2 text-lg font-semibold text-slate-950">{messageCount}</p>
                </div>
                <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3">
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Mode</p>
                  <p className="mt-2 text-lg font-semibold text-slate-950">{modeLabel}</p>
                </div>
                <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3">
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Default Model</p>
                  <p className="mt-2 text-lg font-semibold text-slate-950">{modelLabel}</p>
                </div>
              </div>
            </div>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-6 py-6">
            {!isXeniaEnabled ? (
              <div className="mx-auto max-w-3xl rounded-[28px] border border-amber-200 bg-amber-50/80 p-8 text-center shadow-sm">
                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-white text-amber-700 shadow-sm">
                  <i className="ri-error-warning-line text-xl" />
                </div>
                <h3 className="mt-4 text-xl font-semibold text-slate-950">Xenia is disabled for this tenant</h3>
                <p className="mt-3 text-sm leading-7 text-slate-600">
                  The conversation workspace is available, but the current tenant configuration is disabled. Ask a tenant or platform admin to enable Xenia before sending prompts here.
                </p>
              </div>
            ) : activeConversation && activeConversation.messages.length > 0 ? (
              <div className="mx-auto max-w-4xl space-y-4">
                {activeConversation.messages.map((message) => {
                  const isAssistant = message.role === 'Assistant';

                  return (
                    <article
                      key={message.messageId}
                      className={`flex ${isAssistant ? 'justify-start' : 'justify-end'}`}
                    >
                      <div
                        className={`max-w-3xl rounded-[24px] px-5 py-4 shadow-sm ${
                          isAssistant
                            ? 'border border-slate-200 bg-white text-slate-800'
                            : 'bg-slate-950 text-white'
                        }`}
                      >
                        <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em]">
                          <span className={isAssistant ? 'text-amber-700' : 'text-slate-300'}>
                            {isAssistant ? 'Xenia' : 'You'}
                          </span>
                          <span className={isAssistant ? 'text-slate-400' : 'text-slate-500'}>
                            {formatTimestamp(message.createdAtUtc)}
                          </span>
                        </div>
                        <p className="whitespace-pre-wrap text-sm leading-7">{message.content}</p>
                      </div>
                    </article>
                  );
                })}
                <div ref={messagesEndRef} />
              </div>
            ) : (
              <div className="mx-auto grid max-w-4xl gap-5 lg:grid-cols-[1.25fr_0.95fr]">
                <section className="rounded-[28px] border border-amber-200/80 bg-[linear-gradient(135deg,_rgba(255,247,237,0.95)_0%,_rgba(255,255,255,0.98)_52%,_rgba(238,242,255,0.95)_100%)] p-7 shadow-sm">
                  <div className="inline-flex items-center gap-2 rounded-full border border-amber-200 bg-white/90 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-700">
                    <i className="ri-sparkling-line" />
                    Suggested uses
                  </div>
                  <h3 className="mt-4 text-2xl font-semibold tracking-tight text-slate-950">Start with a concrete operational task.</h3>
                  <p className="mt-3 text-sm leading-7 text-slate-600">
                    This first UI is wired to Xenia’s conversation service. It can already store threads, return assistant replies,
                    and keep the interaction inside the tenant context.
                  </p>
                  <div className="mt-6 space-y-3">
                    {emptyStatePrompts().map((prompt) => (
                      <button
                        key={prompt}
                        type="button"
                        onClick={() => handlePromptShortcut(prompt)}
                        className="flex w-full items-start gap-3 rounded-2xl border border-white/70 bg-white/80 px-4 py-4 text-left shadow-sm transition hover:border-amber-300 hover:bg-white"
                      >
                        <div className="mt-0.5 flex h-9 w-9 items-center justify-center rounded-2xl bg-amber-100 text-amber-700">
                          <i className="ri-arrow-right-up-line" />
                        </div>
                        <div>
                          <p className="text-sm font-semibold text-slate-900">{prompt}</p>
                          <p className="mt-1 text-sm text-slate-500">Click to load this into the composer.</p>
                        </div>
                      </button>
                    ))}
                  </div>
                </section>

                <section className="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
                  <h3 className="text-base font-semibold text-slate-950">Current scope</h3>
                  <div className="mt-4 space-y-3">
                    {[
                      'Conversation creation and thread history',
                      'Tenant-scoped assistant replies through the Xenia API',
                      'A clean path to add streaming, approvals, and provider controls next',
                    ].map((item) => (
                      <div key={item} className="flex items-start gap-3 rounded-2xl bg-slate-50 px-4 py-3">
                        <div className="mt-0.5 flex h-7 w-7 items-center justify-center rounded-full bg-slate-900 text-xs text-white">
                          <i className="ri-check-line" />
                        </div>
                        <p className="text-sm leading-6 text-slate-600">{item}</p>
                      </div>
                    ))}
                  </div>
                </section>
              </div>
            )}
          </div>

          <div className="border-t border-slate-200/80 bg-white/95 px-6 py-5">
            <div className="mx-auto max-w-4xl">
              {composerError ? (
                <div className="mb-4 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                  {composerError}
                </div>
              ) : null}

              <div className="rounded-[26px] border border-slate-200 bg-slate-50/85 p-4 shadow-inner shadow-slate-100">
                <label htmlFor="xenia-draft" className="sr-only">Message Xenia</label>
                <textarea
                  id="xenia-draft"
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' && (event.metaKey || event.ctrlKey)) {
                      event.preventDefault();
                      handleSubmit();
                    }
                  }}
                  rows={4}
                  placeholder="Ask Xenia to summarize a file, draft a response, or inspect the next workflow risk..."
                  className="w-full resize-none border-0 bg-transparent px-1 py-1 text-sm leading-7 text-slate-800 placeholder:text-slate-400 focus:outline-none"
                />

                <div className="mt-4 flex flex-col gap-3 border-t border-slate-200 pt-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex flex-wrap gap-2">
                    {['Summarize', 'Draft response', 'Find risks'].map((label) => (
                      <button
                        key={label}
                        type="button"
                        onClick={() => handlePromptShortcut(`${label} this workflow item with a concise operational recommendation.`)}
                        className="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 transition hover:border-amber-300 hover:text-amber-700"
                      >
                        {label}
                      </button>
                    ))}
                  </div>

                  <div className="flex items-center gap-3">
                    <p className="text-xs text-slate-400">Press `Ctrl`/`Cmd` + `Enter` to send</p>
                    <button
                      type="button"
                      disabled={!draft.trim() || isPending || !isXeniaEnabled}
                      onClick={handleSubmit}
                      className="inline-flex items-center gap-2 rounded-full bg-slate-950 px-5 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-300"
                    >
                      {isPending ? <i className="ri-loader-4-line animate-spin" /> : <i className="ri-send-plane-2-line" />}
                      Send to Xenia
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>
      </div>
    </XeniaProductShell>
  );
}
