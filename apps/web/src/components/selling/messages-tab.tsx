"use client";

import { type FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { Download, Eye, MessagesSquare, Paperclip, Send } from "lucide-react";
import { ContactsEmptyState } from "./contacts/contacts-empty-state";
import { fileIconFor, UploadedFileRow } from "./uploaded-file-row";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import type { SellerLienMessage } from "@/lib/selling/liens.types";

const MAX_MESSAGE_LENGTH = 400;
const MAX_ATTACHMENT_COUNT = 10;

function formatTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";

  return new Intl.DateTimeFormat("en-US", {
    month: "long",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(0)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}

export function MessagesTab({
  lienId,
  emptyTitle = "No Messages Yet",
  emptyDescription = "Start the conversation below. Messages are saved to this lien offer thread.",
}: {
  lienId?: string;
  emptyTitle?: string;
  emptyDescription?: string;
}) {
  const [messages, setMessages] = useState<SellerLienMessage[]>([]);
  const [draft, setDraft] = useState("");
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const threadRef = useRef<HTMLDivElement | null>(null);
  const shouldScrollRef = useRef(false);

  const sortedMessages = useMemo(
    () =>
      [...messages].sort(
        (a, b) =>
          new Date(a.createdAtUtc).getTime() -
          new Date(b.createdAtUtc).getTime(),
      ),
    [messages],
  );

  const canSend = Boolean(lienId) && (draft.trim().length > 0 || pendingFiles.length > 0) && !submitting;

  useEffect(() => {
    let cancelled = false;

    async function loadMessages() {
      if (!lienId) {
        setMessages([]);
        setLoading(false);
        return;
      }

      setLoading(true);
      setError(null);
      try {
        const result = await liensService.getLienMessages(lienId);
        if (!cancelled) setMessages(result.items);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : "Failed to load messages.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadMessages();
    return () => {
      cancelled = true;
    };
  }, [lienId]);

  useEffect(() => {
    if (!shouldScrollRef.current) return;
    shouldScrollRef.current = false;

    const thread = threadRef.current;
    if (!thread) return;

    if (typeof thread.scrollTo === "function") {
      thread.scrollTo({ top: thread.scrollHeight, behavior: "smooth" });
      return;
    }

    thread.scrollTop = thread.scrollHeight;
  }, [messages.length]);

  const handleSend = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSend || !lienId) return;

    setSubmitting(true);
    setError(null);
    try {
      const message = await liensService.sendLienMessage(lienId, draft, pendingFiles);
      shouldScrollRef.current = true;
      setMessages((current) => [...current, message]);
      setDraft("");
      setPendingFiles([]);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "The message could not be sent. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-6 py-5 space-y-5">
        <h3 className="text-md font-semibold">Messages</h3>

        {loading ? (
          <p className="text-sm text-gray-400 py-12 text-center">Loading messages...</p>
        ) : sortedMessages.length === 0 ? (
          <ContactsEmptyState
            icon={MessagesSquare}
            title={emptyTitle}
            description={emptyDescription}
          />
        ) : (
          <div
            ref={threadRef}
            className="flex flex-col gap-3 max-h-[480px] overflow-y-auto pr-1"
            aria-label="Message thread"
          >
            {sortedMessages.map((message) => (
              <div
                key={message.id}
                className={`flex ${message.isCurrentUser ? "justify-end" : "justify-start"}`}
              >
                <div
                  className={`max-w-[80%] rounded-2xl px-4 py-3 text-sm space-y-2 ${
                    message.isCurrentUser
                      ? "bg-[#EE7132]/10 text-gray-900"
                      : "border border-gray-200 bg-gray-50 text-gray-900"
                  }`}
                >
                  <div className="flex items-center gap-2 text-xs font-medium text-gray-500">
                    <span>{message.isCurrentUser ? "You" : message.senderName}</span>
                    <span aria-hidden="true">·</span>
                    <span>{formatTimestamp(message.createdAtUtc)}</span>
                  </div>
                  {message.message ? (
                    <p className="whitespace-pre-wrap break-words">{message.message}</p>
                  ) : null}
                  {message.attachments?.length ? (
                    <div className="space-y-2">
                      {message.attachments.map((attachment) => (
                        <UploadedFileRow
                          key={attachment.id}
                          icon={fileIconFor(attachment.fileName)}
                          title={attachment.fileName}
                          subtitle={formatSize(attachment.fileSizeBytes)}
                          actions={
                            <>
                              {attachment.viewUrl ? (
                                <a
                                  href={attachment.viewUrl}
                                  target="_blank"
                                  rel="noreferrer"
                                  aria-label="View attachment"
                                  title="View"
                                  className="flex h-7 w-7 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-[#EE7132]"
                                >
                                  <Eye className="h-4 w-4" />
                                </a>
                              ) : null}
                              {attachment.downloadUrl ? (
                                <a
                                  href={attachment.downloadUrl}
                                  target="_blank"
                                  rel="noreferrer"
                                  aria-label="Download attachment"
                                  title="Download"
                                  className="flex h-7 w-7 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-[#EE7132]"
                                >
                                  <Download className="h-4 w-4" />
                                </a>
                              ) : null}
                            </>
                          }
                        />
                      ))}
                    </div>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        )}

        {pendingFiles.length > 0 ? (
          <div className="space-y-2">
            {pendingFiles.map((file, index) => (
              <UploadedFileRow
                key={`${file.name}-${file.lastModified}-${index}`}
                icon={fileIconFor(file.name)}
                title={file.name}
                subtitle={formatSize(file.size)}
                actions={
                  <button
                    type="button"
                    className="text-xs font-medium text-gray-400 hover:text-red-600"
                    onClick={() =>
                      setPendingFiles((current) => current.filter((_, i) => i !== index))
                    }
                  >
                    Remove
                  </button>
                }
              />
            ))}
          </div>
        ) : null}

        <form
          className="flex items-center gap-2 rounded-xl border border-gray-200 px-3 py-2 focus-within:border-[#EE7132]"
          onSubmit={handleSend}
        >
          <input
            ref={fileInputRef}
            type="file"
            multiple
            className="hidden"
            onChange={(event) => {
              const files = Array.from(event.target.files ?? []);
              if (files.length) {
                setPendingFiles((current) => {
                  const availableSlots = Math.max(0, MAX_ATTACHMENT_COUNT - current.length);
                  const nextFiles = [...current, ...files.slice(0, availableSlots)];
                  if (files.length > availableSlots) {
                    setError(`Attach up to ${MAX_ATTACHMENT_COUNT} files per message.`);
                  } else if (error) {
                    setError(null);
                  }
                  return nextFiles;
                });
              }
              event.target.value = "";
            }}
          />
          <input
            type="text"
            aria-label="Message"
            placeholder="Type a message..."
            maxLength={MAX_MESSAGE_LENGTH}
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              if (error) setError(null);
            }}
            className="flex-1 min-w-0 text-sm outline-none"
          />
          <span className="hidden text-xs text-gray-400 sm:inline">
            {draft.length}/{MAX_MESSAGE_LENGTH}
          </span>
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={submitting || pendingFiles.length >= MAX_ATTACHMENT_COUNT}
            title="Attach files"
            className="w-8 h-8 flex items-center justify-center rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-50 shrink-0 disabled:text-gray-300 disabled:cursor-not-allowed"
            aria-label="Attach file"
          >
            <Paperclip className="h-4 w-4" />
          </button>
          <button
            type="submit"
            disabled={!canSend}
            className="w-9 h-9 rounded-full bg-[#EE7132] text-white flex items-center justify-center shrink-0 disabled:bg-[#F7B899] disabled:cursor-not-allowed hover:bg-[#D9672E] transition-colors"
            aria-label="Send message"
          >
            <Send className={`h-4 w-4 ${submitting ? "animate-pulse" : ""}`} />
          </button>
        </form>
        {error ? (
          <p role="alert" className="text-sm font-medium text-red-600">
            {error}
          </p>
        ) : null}
      </div>
    </div>
  );
}
