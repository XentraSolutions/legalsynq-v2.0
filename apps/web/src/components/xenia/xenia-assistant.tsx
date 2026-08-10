"use client";

import { useEffect, useMemo, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { usePathname, useSearchParams } from 'next/navigation';
import { xeniaClient } from '@/lib/xenia/client';
import { formatShortTimestamp } from '@/lib/format-date';
import { useTimezone } from '@/lib/use-timezone';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  parseMarkdownBlocks,
  parseMarkdownInlines,
  type MarkdownBlock,
  type MarkdownInlineToken,
} from '@/lib/xenia/markdown';
import {
  buildStarterPrompts,
  buildXeniaContext,
  parseXeniaMessageMetadata,
  serializeXeniaContext,
} from '@/lib/xenia/context';
import {
  drainSseBuffer,
  flushSseBuffer,
  type XeniaStreamEvent,
} from '@/lib/xenia/stream';
import type {
  XeniaAgent,
  XeniaBootstrap,
  XeniaConversation,
  XeniaConversationSummary,
  XeniaLookupResult,
  XeniaMessage,
} from '@/lib/xenia/types';

type AssistantMode = 'page' | 'drawer';

interface XeniaAssistantProps {
  mode?: AssistantMode;
  initialContext?: Record<string, unknown>;
}

export function XeniaAssistant({ mode = 'page', initialContext }: XeniaAssistantProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const timezone = useTimezone();
  const [bootstrap, setBootstrap] = useState<XeniaBootstrap | null>(null);
  const [conversations, setConversations] = useState<XeniaConversationSummary[]>([]);
  const [activeConversation, setActiveConversation] = useState<XeniaConversation | null>(null);
  const [selectedAgentKey, setSelectedAgentKey] = useState('generic');
  const [input, setInput] = useState('');
  const [draftAssistant, setDraftAssistant] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const endRef = useRef<HTMLDivElement>(null);

  const structuredContext = useMemo(() => buildXeniaContext(
    pathname,
    searchParams,
    mode,
    initialContext,
  ), [initialContext, mode, pathname, searchParams]);

  const contextJson = useMemo(() => serializeXeniaContext(structuredContext), [structuredContext]);

  const agents = useMemo(() => {
    const items = bootstrap?.agents ?? [];
    return [...items].sort((a, b) => {
      if (a.agentKey === 'generic') return -1;
      if (b.agentKey === 'generic') return 1;
      return a.name.localeCompare(b.name);
    });
  }, [bootstrap?.agents]);

  const agentSelectionLocked = isStreaming || (mode === 'page' && !!activeConversation);
  const starterPrompts = useMemo(
    () => buildStarterPrompts(selectedAgentKey, structuredContext),
    [selectedAgentKey, structuredContext],
  );

  useEffect(() => {
    let alive = true;
    async function load() {
      setIsLoading(true);
      setError(null);
      try {
        const [boot, list] = await Promise.all([
          xeniaClient.bootstrap(),
          xeniaClient.conversations(),
        ]);
        if (!alive) return;
        setBootstrap(boot);
        setConversations(list);
        setSelectedAgentKey(boot.preferences.defaultAgentKey || boot.agents[0]?.agentKey || 'generic');
        if (list[0]) {
          const conversation = await xeniaClient.getConversation(list[0].id);
          if (alive) {
            setActiveConversation(conversation);
            setSelectedAgentKey(conversation.agentKey);
          }
        }
      } catch (err) {
        if (alive) setError(err instanceof Error ? err.message : 'Unable to load Xenia.');
      } finally {
        if (alive) setIsLoading(false);
      }
    }
    load();
    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: 'end' });
  }, [activeConversation?.messages, draftAssistant]);

  const selectedAgent = agents.find(agent => agent.agentKey === selectedAgentKey) ?? agents[0];

  async function startConversation(agentKey = selectedAgentKey): Promise<XeniaConversation> {
    const conversation = await xeniaClient.createConversation(agentKey, mode, contextJson);
    setActiveConversation(conversation);
    setConversations(prev => [
      {
        id: conversation.id,
        agentKey: conversation.agentKey,
        agentVersion: conversation.agentVersion,
        title: conversation.title,
        source: conversation.source,
        status: conversation.status,
        lastMessageAtUtc: conversation.lastMessageAtUtc,
        createdAtUtc: conversation.createdAtUtc,
        updatedAtUtc: conversation.updatedAtUtc,
      },
      ...prev.filter(item => item.id !== conversation.id),
    ]);
    return conversation;
  }

  async function selectConversation(id: string) {
    if (isStreaming) return;
    setError(null);
    const conversation = await xeniaClient.getConversation(id);
    setActiveConversation(conversation);
    setSelectedAgentKey(conversation.agentKey);
  }

  async function archiveConversation(id: string) {
    if (isStreaming) return;
    await xeniaClient.archiveConversation(id);
    setConversations(prev => prev.filter(item => item.id !== id));
    if (activeConversation?.id === id) setActiveConversation(null);
  }

  async function submit(overrideContent?: string) {
    const content = (overrideContent ?? input).trim();
    if (!content || isStreaming) return;

    if (!overrideContent) {
      setInput('');
    } else {
      setInput('');
    }
    setDraftAssistant('');
    setError(null);
    setIsStreaming(true);

    try {
      const conversation = activeConversation ?? await startConversation(selectedAgentKey);
      await streamMessage(conversation.id, content);
      const refreshed = await xeniaClient.getConversation(conversation.id);
      setActiveConversation(refreshed);
      setConversations(prev => [
        {
          id: refreshed.id,
          agentKey: refreshed.agentKey,
          agentVersion: refreshed.agentVersion,
          title: refreshed.title,
          source: refreshed.source,
          status: refreshed.status,
          lastMessageAtUtc: refreshed.lastMessageAtUtc,
          createdAtUtc: refreshed.createdAtUtc,
          updatedAtUtc: refreshed.updatedAtUtc,
        },
        ...prev.filter(item => item.id !== refreshed.id),
      ]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Xenia could not send the message.');
    } finally {
      setIsStreaming(false);
      setDraftAssistant('');
    }
  }

  async function streamMessage(conversationId: string, content: string) {
    const res = await fetch(`/api/xenia/assistant/conversations/${conversationId}/messages:stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body: JSON.stringify({ content, contextJson }),
    });

    if (!res.ok || !res.body) {
      throw new Error(`Xenia stream failed (${res.status})`);
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const drained = drainSseBuffer(buffer);
      buffer = drained.rest;
      drained.events.forEach(applyStreamEvent);
      if (drained.events.some(event => event.type === 'delta')) {
        await yieldForBrowserPaint();
      }
    }

    buffer += decoder.decode();
    const drained = drainSseBuffer(buffer);
    drained.events.forEach(applyStreamEvent);
    flushSseBuffer(drained.rest).forEach(applyStreamEvent);
  }

  function applyStreamEvent(evt: XeniaStreamEvent) {
    if (evt.type === 'delta' && evt.delta) {
      flushSync(() => {
        setDraftAssistant(prev => prev + evt.delta);
      });
      return;
    }

    if (evt.type === 'user_message' && evt.message) {
      setActiveConversation(prev => prev
        ? { ...prev, messages: [...prev.messages, evt.message as XeniaMessage] }
        : prev);
      return;
    }

    if (evt.type === 'message' && evt.message) {
      setActiveConversation(prev => prev
        ? { ...prev, messages: [...prev.messages, evt.message as XeniaMessage] }
        : prev);
      setDraftAssistant('');
      return;
    }

    if (evt.type === 'error') {
      setError(evt.error ?? 'Xenia provider failed.');
    }
  }

  return (
    <div className={mode === 'drawer' ? 'flex h-full min-h-0 flex-col bg-white' : 'flex min-h-[calc(100vh-7rem)] overflow-hidden rounded-lg border border-gray-200 bg-white'}>
      <aside className={mode === 'drawer' ? 'border-b border-gray-200 p-3' : 'hidden w-72 shrink-0 border-r border-gray-200 bg-gray-50 p-3 md:block'}>
        <div className="flex items-center justify-between gap-2">
          <div>
            <p className="text-sm font-semibold text-gray-900">Xenia</p>
            <p className="text-xs text-gray-500">{selectedAgent?.name ?? 'Assistant'}</p>
          </div>
          <button
            type="button"
            onClick={() => setActiveConversation(null)}
            disabled={isStreaming}
            className="inline-flex h-8 w-8 items-center justify-center rounded-md border border-gray-200 text-gray-500 hover:bg-white disabled:opacity-50"
            title="New conversation"
          >
            <i className="ri-add-line text-base" />
          </button>
        </div>

        <label className="mt-3 block text-xs font-medium text-gray-500">
          Agent
          <Select
            value={selectedAgentKey}
            onValueChange={(value) => {
              setSelectedAgentKey(value);
              if (mode === 'drawer') setActiveConversation(null);
            }}
            disabled={agentSelectionLocked}
          >
            <SelectTrigger className="mt-1.5 h-11 text-sm font-medium text-gray-900 shadow-sm">
              <SelectValue placeholder="Select an agent" />
            </SelectTrigger>
            <SelectContent className="z-[90]">
              {agents.map(agent => (
                <SelectItem key={agent.agentKey} value={agent.agentKey}>
                  {agent.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </label>

        {mode === 'page' && (
          <div className="mt-4 space-y-1">
            {conversations.map(conversation => (
              <div
                key={conversation.id}
                className={[
                  'group flex w-full items-center justify-between gap-2 rounded-md px-2 py-2 text-left text-sm',
                  activeConversation?.id === conversation.id ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-600 hover:bg-white',
                ].join(' ')}
              >
                <button
                  type="button"
                  onClick={() => selectConversation(conversation.id)}
                  className="min-w-0 flex-1 text-left"
                >
                  <span className="block truncate">{conversation.title}</span>
                  <time
                    dateTime={conversation.createdAtUtc}
                    className="mt-0.5 block text-xs text-gray-400"
                  >
                    {formatShortTimestamp(conversation.createdAtUtc, timezone)}
                  </time>
                </button>
                <button
                  type="button"
                  onClick={(event) => {
                    event.stopPropagation();
                    archiveConversation(conversation.id);
                  }}
                  className="hidden h-6 w-6 shrink-0 items-center justify-center rounded text-gray-400 hover:bg-gray-100 hover:text-gray-700 group-hover:inline-flex"
                  title="Archive"
                >
                  <i className="ri-archive-line text-sm" />
                </button>
              </div>
            ))}
          </div>
        )}
      </aside>

      <main className="flex min-h-0 flex-1 flex-col">
        <div className="border-b border-gray-100 px-4 py-3">
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <h1 className="truncate text-base font-semibold text-gray-900">
                {activeConversation?.title ?? 'New conversation'}
              </h1>
              <p className="truncate text-xs text-gray-500">
                {selectedAgent?.description ?? 'Tenant-aware AI assistant'}
              </p>
            </div>
            {bootstrap?.usage.monthlyRequestLimit != null && (
              <span className="shrink-0 rounded-md bg-gray-100 px-2 py-1 text-xs text-gray-600">
                {bootstrap.usage.requestsThisMonth}/{bootstrap.usage.monthlyRequestLimit}
              </span>
            )}
          </div>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">
          {isLoading ? (
            <div className="space-y-3">
              <div className="h-16 animate-pulse rounded-lg bg-gray-100" />
              <div className="h-24 animate-pulse rounded-lg bg-gray-100" />
            </div>
          ) : (
            <div className="mx-auto max-w-3xl space-y-4">
              {(activeConversation?.messages.length ?? 0) === 0 && !draftAssistant && (
                <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 px-4 py-8 text-center">
                  <p className="text-sm font-medium text-gray-700">Ask Xenia about your current work.</p>
                  <p className="mt-1 text-xs text-gray-500">Xenia only uses tenant context and product data you are authorized to access.</p>
                  <div className="mt-4 flex flex-wrap justify-center gap-2">
                    {starterPrompts.map(prompt => (
                      <button
                        key={prompt}
                        type="button"
                        onClick={() => submit(prompt)}
                        disabled={isLoading || isStreaming}
                        className="inline-flex rounded-full border border-orange-200 bg-white px-3 py-1.5 text-xs font-medium text-orange-700 hover:border-orange-300 hover:bg-orange-50 disabled:opacity-50"
                      >
                        {prompt}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {activeConversation?.messages.map(message => (
                <MessageBubble
                  key={message.id}
                  message={message}
                  onPromptClick={prompt => {
                    setInput(prompt);
                  }}
                />
              ))}

              {draftAssistant && (
                <div className="flex justify-start">
                  <div className="max-w-[85%] break-words rounded-2xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-800 shadow-sm">
                    <AssistantMessageContent content={draftAssistant} isStreaming />
                  </div>
                </div>
              )}

              {isStreaming && !draftAssistant && (
                <div className="flex justify-start px-2 py-1">
                  <ThinkingIndicator />
                </div>
              )}

              {error && (
                <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                  {error}
                </div>
              )}
              <div ref={endRef} />
            </div>
          )}
        </div>

        <div className="border-t border-gray-100 p-3">
          <div className="mx-auto flex max-w-3xl items-end gap-2">
            <textarea
              value={input}
              onChange={event => setInput(event.target.value)}
              onKeyDown={event => {
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault();
                  submit();
                }
              }}
              rows={2}
              placeholder="Message Xenia"
              disabled={isLoading || isStreaming}
              className="min-h-11 flex-1 resize-none rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-orange-500 disabled:bg-gray-50"
            />
            <button
              type="button"
              onClick={() => {
                void submit();
              }}
              disabled={!input.trim() || isLoading || isStreaming}
              className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-[#0f1928] text-white hover:bg-[#16243a] disabled:cursor-not-allowed disabled:opacity-50"
              title="Send"
            >
              {isStreaming ? <i className="ri-loader-4-line animate-spin text-base" /> : <i className="ri-send-plane-2-line text-base" />}
            </button>
          </div>
        </div>
      </main>
    </div>
  );
}

async function yieldForBrowserPaint() {
  await new Promise<void>(resolve => {
    if (typeof window === 'undefined' || typeof window.requestAnimationFrame !== 'function') {
      setTimeout(resolve, 0);
      return;
    }

    window.requestAnimationFrame(() => resolve());
  });
}

function MessageBubble({
  message,
  onPromptClick,
}: {
  message: XeniaMessage;
  onPromptClick: (prompt: string) => void;
}) {
  const isUser = message.role === 'user';
  const metadata = useMemo(() => parseXeniaMessageMetadata(message.metadataJson), [message.metadataJson]);
  return (
    <div className={isUser ? 'flex justify-end' : 'flex justify-start'}>
      <div className={[
        'max-w-[85%] break-words rounded-2xl px-4 py-3 text-sm leading-6',
        isUser ? 'bg-[#0f1928] text-white' : 'border border-gray-200 bg-white text-gray-800 shadow-sm',
      ].join(' ')}>
        {isUser ? (
          <p className="whitespace-pre-wrap">{message.content}</p>
        ) : (
          <AssistantMessageContent content={message.content} />
        )}
        {!isUser && metadata.lookupResults.length > 0 && (
          <LookupResultCards results={metadata.lookupResults} />
        )}
        {message.citations.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-2 border-t border-gray-100 pt-3">
            {message.citations.map(citation => (
              citation.url ? (
                <a
                  key={citation.id}
                  href={citation.url}
                  className="inline-flex items-center rounded-full border border-orange-200 bg-orange-50 px-2.5 py-1 text-xs font-medium text-orange-700 hover:border-orange-300 hover:bg-orange-100"
                >
                  {citation.label}
                </a>
              ) : (
                <span
                  key={citation.id}
                  className="inline-flex items-center rounded-full border border-gray-200 bg-gray-50 px-2.5 py-1 text-xs font-medium text-gray-600"
                >
                  {citation.label}
                </span>
              )
            ))}
          </div>
        )}
        {!isUser && metadata.followUpPrompts.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-2 border-t border-gray-100 pt-3">
            {metadata.followUpPrompts.map(prompt => (
              <button
                key={prompt}
                type="button"
                onClick={() => onPromptClick(prompt)}
                className="inline-flex rounded-full border border-gray-200 bg-gray-50 px-2.5 py-1 text-xs font-medium text-gray-700 hover:border-gray-300 hover:bg-gray-100"
              >
                {prompt}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function LookupResultCards({ results }: { results: XeniaLookupResult[] }) {
  return (
    <div className="mt-3 space-y-2">
      {results.map(result => (
        result.url ? (
          <a
            key={`${result.kind}:${result.id}`}
            href={result.url}
            className="block rounded-xl border border-gray-200 bg-gray-50/70 px-3 py-3 transition-colors hover:border-orange-300 hover:bg-orange-50/60"
          >
            <LookupResultCardBody result={result} />
          </a>
        ) : (
          <div
            key={`${result.kind}:${result.id}`}
            className="rounded-xl border border-gray-200 bg-gray-50/70 px-3 py-3"
          >
            <LookupResultCardBody result={result} />
          </div>
        )
      ))}
    </div>
  );
}

function ThinkingIndicator() {
  return (
    <div
      aria-label="Xenia is thinking"
      className="inline-flex items-center gap-2 text-sm text-gray-500"
      role="status"
    >
      <img
        src="/product-icons/synqai.png"
        alt=""
        aria-hidden="true"
        className="h-5 w-5 animate-spin object-contain"
      />
      <span className="animate-pulse font-medium">Thinking...</span>
    </div>
  );
}

function LookupResultCardBody({ result }: { result: XeniaLookupResult }) {
  return (
    <div className="space-y-2">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-gray-900">{result.title}</p>
          {result.subtitle && (
            <p className="truncate text-xs text-gray-500">{result.subtitle}</p>
          )}
        </div>
        {result.status && (
          <span className="shrink-0 rounded-full bg-white px-2 py-0.5 text-[11px] font-medium text-gray-700">
            {result.status}
          </span>
        )}
      </div>
      {result.description && (
        <p className="text-xs leading-5 text-gray-600">{result.description}</p>
      )}
      {result.badges.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {result.badges.map(badge => (
            <span
              key={badge}
              className="inline-flex rounded-full border border-orange-200 bg-white px-2 py-0.5 text-[11px] font-medium text-orange-700"
            >
              {badge}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

function AssistantMessageContent({
  content,
  isStreaming = false,
}: {
  content: string;
  isStreaming?: boolean;
}) {
  const blocks = useMemo(() => parseMarkdownBlocks(content), [content]);

  if (blocks.length === 0) {
    return <p className="whitespace-pre-wrap">{content}</p>;
  }

  return (
    <div className={['space-y-3', isStreaming ? 'opacity-90' : ''].join(' ').trim()}>
      {blocks.map((block, index) => renderMarkdownBlock(block, `${index}`))}
    </div>
  );
}

function renderMarkdownBlock(block: MarkdownBlock, key: string): JSX.Element {
  switch (block.type) {
    case 'heading': {
      const className = {
        1: 'text-lg font-semibold text-gray-950',
        2: 'text-base font-semibold text-gray-950',
        3: 'text-sm font-semibold uppercase tracking-wide text-gray-700',
        4: 'text-sm font-semibold text-gray-900',
        5: 'text-sm font-medium text-gray-900',
        6: 'text-xs font-semibold uppercase tracking-wide text-gray-500',
      }[block.level];
      return (
        <div key={key} className={className}>
          {renderInlineTokens(parseMarkdownInlines(block.text), `${key}-heading`)}
        </div>
      );
    }
    case 'paragraph':
      return (
        <p key={key} className="text-sm leading-6 text-gray-800">
          {renderInlineTokens(parseMarkdownInlines(block.text), `${key}-paragraph`)}
        </p>
      );
    case 'list':
      return block.ordered ? (
        <ol key={key} className="list-decimal space-y-2 pl-5 marker:font-semibold marker:text-gray-500">
          {block.items.map((item, index) => (
            <li key={`${key}-item-${index}`} className="pl-1">
              <div className="space-y-2">
                {item.map((nestedBlock, nestedIndex) =>
                  renderMarkdownBlock(nestedBlock, `${key}-item-${index}-${nestedIndex}`))}
              </div>
            </li>
          ))}
        </ol>
      ) : (
        <ul key={key} className="list-disc space-y-2 pl-5 marker:text-gray-500">
          {block.items.map((item, index) => (
            <li key={`${key}-item-${index}`} className="pl-1">
              <div className="space-y-2">
                {item.map((nestedBlock, nestedIndex) =>
                  renderMarkdownBlock(nestedBlock, `${key}-item-${index}-${nestedIndex}`))}
              </div>
            </li>
          ))}
        </ul>
      );
    case 'blockquote':
      return (
        <blockquote
          key={key}
          className="border-l-2 border-orange-200 bg-orange-50/50 pl-4 italic text-gray-700"
        >
          <div className="space-y-2 py-0.5">
            {block.blocks.map((nestedBlock, nestedIndex) =>
              renderMarkdownBlock(nestedBlock, `${key}-quote-${nestedIndex}`))}
          </div>
        </blockquote>
      );
    case 'code':
      return (
        <div key={key} className="overflow-hidden rounded-xl border border-slate-200 bg-slate-950">
          {block.language && (
            <div className="border-b border-slate-800 px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-slate-300">
              {block.language}
            </div>
          )}
          <pre className="overflow-x-auto px-3 py-3 text-[13px] leading-6 text-slate-100">
            <code>{block.code}</code>
          </pre>
        </div>
      );
    case 'table':
      return (
        <div key={key} className="overflow-x-auto rounded-xl border border-gray-200">
          <table className="min-w-full border-collapse text-left text-sm">
            <thead className="bg-gray-50">
              <tr>
                {block.headers.map((header, index) => (
                  <th
                    key={`${key}-header-${index}`}
                    className="border-b border-gray-200 px-3 py-2 font-semibold text-gray-700"
                  >
                    {renderInlineTokens(parseMarkdownInlines(header), `${key}-header-${index}`)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {block.rows.map((row, rowIndex) => (
                <tr key={`${key}-row-${rowIndex}`} className="border-t border-gray-100">
                  {row.map((cell, cellIndex) => (
                    <td
                      key={`${key}-row-${rowIndex}-cell-${cellIndex}`}
                      className="px-3 py-2 align-top text-gray-700"
                    >
                      {renderInlineTokens(
                        parseMarkdownInlines(cell),
                        `${key}-row-${rowIndex}-cell-${cellIndex}`,
                      )}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
    case 'rule':
      return <hr key={key} className="border-gray-200" />;
    default:
      return <div key={key} />;
  }
}

function renderInlineTokens(tokens: MarkdownInlineToken[], keyPrefix: string): React.ReactNode[] {
  return tokens.flatMap((token, index) => {
    const key = `${keyPrefix}-${index}`;

    switch (token.type) {
      case 'text': {
        const segments = token.value.split('\n');
        return segments.flatMap((segment, segmentIndex) => (
          segmentIndex === 0
            ? [<span key={`${key}-segment-${segmentIndex}`}>{segment}</span>]
            : [
              <br key={`${key}-break-${segmentIndex}`} />,
              <span key={`${key}-segment-${segmentIndex}`}>{segment}</span>,
            ]
        ));
      }
      case 'strong':
        return (
          <strong key={key} className="font-semibold text-gray-950">
            {renderInlineTokens(token.children, `${key}-strong`)}
          </strong>
        );
      case 'emphasis':
        return (
          <em key={key} className="italic">
            {renderInlineTokens(token.children, `${key}-emphasis`)}
          </em>
        );
      case 'code':
        return (
          <code
            key={key}
            className="rounded-md bg-gray-100 px-1.5 py-0.5 font-mono text-[0.85em] text-gray-900"
          >
            {token.value}
          </code>
        );
      case 'link':
        return (
          <a
            key={key}
            href={token.href}
            target={isExternalHref(token.href) ? '_blank' : undefined}
            rel={isExternalHref(token.href) ? 'noreferrer' : undefined}
            className="font-medium text-orange-700 underline decoration-orange-300 underline-offset-2 hover:text-orange-800"
          >
            {renderInlineTokens(token.children, `${key}-link`)}
          </a>
        );
      default:
        return [];
    }
  });
}

function isExternalHref(href: string): boolean {
  return /^https?:\/\//i.test(href);
}
