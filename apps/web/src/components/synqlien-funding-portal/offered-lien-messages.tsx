"use client";

import { type FormEvent, type ReactNode, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { postFundingOfferedLienMessage } from "@/lib/synqlien-funding-portal/client-actions";
import type { OfferedLienMessage } from "@/lib/synqlien-funding-portal/types";

interface OfferedLienMessagesProps {
  id: string;
  initialMessages: OfferedLienMessage[];
}

const MAX_MESSAGE_LENGTH = 400;
const MAX_ATTACHMENT_COUNT = 10;

export function OfferedLienMessages({
  id,
  initialMessages,
}: OfferedLienMessagesProps) {
  const router = useRouter();
  const threadRef = useRef<HTMLDivElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const shouldScrollRef = useRef(false);
  const [messages, setMessages] = useState(initialMessages);
  const [draft, setDraft] = useState("");
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const trimmedDraft = draft.trim();
  const canSend = (trimmedDraft.length > 0 || pendingFiles.length > 0) && !submitting;

  useEffect(() => {
    setMessages(initialMessages);
  }, [initialMessages]);

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

  const sortedMessages = useMemo(
    () =>
      [...messages].sort(
        (a, b) =>
          new Date(a.createdAtUtc).getTime() -
          new Date(b.createdAtUtc).getTime(),
      ),
    [messages],
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSend) return;

    setSubmitting(true);
    setError(null);
    const result = await postFundingOfferedLienMessage(id, draft, pendingFiles);
    setSubmitting(false);

    if (result.ok && result.message) {
      shouldScrollRef.current = true;
      setMessages(current => [...current, result.message!]);
      setDraft("");
      setPendingFiles([]);
      router.refresh();
      return;
    }

    setError(result.error?.message ?? "The message could not be sent. Please try again.");
  }

  return (
    <section className="flex min-h-[520px] flex-col rounded-[16px] border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
      <div
        ref={threadRef}
        className="flex min-h-[360px] flex-1 flex-col gap-4 overflow-y-auto pr-1"
        aria-label="Message thread"
      >
        {sortedMessages.length > 0 ? (
          sortedMessages.map(message => (
            <MessageBubble key={message.id} message={message} />
          ))
        ) : (
          <CenteredMessageEmptyState />
        )}
      </div>

      {pendingFiles.length > 0 ? (
        <div className="mt-4 flex flex-col gap-2">
          {pendingFiles.map((file, index) => (
            <AttachmentRow
              key={`${file.name}-${file.lastModified}-${index}`}
              fileName={file.name}
              subtitle={formatFileSize(file.size)}
              action={
                <button
                  type="button"
                  className="border-0 bg-transparent text-[12px] font-semibold leading-[1.6] text-[#737373] hover:text-red-600"
                  onClick={() => setPendingFiles(current => current.filter((_, fileIndex) => fileIndex !== index))}
                >
                  Remove
                </button>
              }
            />
          ))}
        </div>
      ) : null}

      <form
        className="mt-6 flex items-center gap-4 rounded-[12px] border border-[#e5e5e5] bg-white py-3 pl-4 pr-3 shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors focus-within:border-[#ee7132]"
        onSubmit={handleSubmit}
      >
        <input
          ref={fileInputRef}
          type="file"
          aria-label="Message attachments"
          multiple
          className="hidden"
          onChange={event => {
            const files = Array.from(event.target.files ?? []);
            if (files.length) {
              setPendingFiles(current => {
                const availableSlots = Math.max(0, MAX_ATTACHMENT_COUNT - current.length);
                const acceptedFiles = files.slice(0, availableSlots);
                if (files.length > availableSlots) {
                  setError(`Attach up to ${MAX_ATTACHMENT_COUNT} files per message.`);
                } else if (error) {
                  setError(null);
                }
                return [...current, ...acceptedFiles];
              });
            }
            event.target.value = "";
          }}
        />
        <input
          aria-label="Message"
          placeholder="Type a message..."
          maxLength={MAX_MESSAGE_LENGTH}
          value={draft}
          onChange={event => {
            setDraft(event.target.value);
            if (error) setError(null);
          }}
          className="min-w-0 flex-1 border-0 bg-transparent text-[14px] font-normal leading-[1.6] text-[#0a0a0a] outline-none placeholder:text-[#737373]"
        />
        <span className="hidden whitespace-nowrap text-[12px] font-normal leading-[1.6] text-[#737373] sm:inline">
          {draft.length}/{MAX_MESSAGE_LENGTH}
        </span>
        <button
          type="button"
          aria-label="Attach file"
          disabled={submitting || pendingFiles.length >= MAX_ATTACHMENT_COUNT}
          onClick={() => fileInputRef.current?.click()}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-transparent text-[#737373] transition-colors hover:bg-[#f5f5f5] hover:text-[#0a0a0a] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] disabled:cursor-not-allowed disabled:opacity-50"
        >
          <i className="ri-attachment-2 text-[16px]" aria-hidden="true" />
        </button>
        <button
          type="submit"
          aria-label="Send message"
          disabled={!canSend}
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[#ee7132] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-[#d85f25] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] active:bg-[#c95720] disabled:cursor-not-allowed disabled:opacity-60"
        >
          <i
            className={
              submitting
                ? "ri-loader-4-line animate-spin text-[16px]"
                : "ri-send-plane-2-line text-[16px]"
            }
          />
        </button>
      </form>
      {error ? (
        <p role="alert" className="mt-3 text-[14px] font-medium leading-[1.6] text-red-600">
          {error}
        </p>
      ) : null}
    </section>
  );
}

function MessageBubble({ message }: { message: OfferedLienMessage }) {
  const mine = Boolean(message.isCurrentUser);

  return (
    <article className={`flex gap-3 ${mine ? "flex-row-reverse self-end text-right" : "self-start text-left"}`}>
      <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-[14px] font-semibold leading-[1.6] ${
        mine ? "bg-[#fdf1eb] text-[#a95024]" : "bg-[#f5f5f5] text-[#525252]"
      }`}>
        {message.senderInitials || buildInitials(message.senderName)}
      </span>
      <div className={`max-w-[680px] ${mine ? "items-end" : "items-start"} flex flex-col`}>
        <div className={`flex flex-wrap items-center gap-2 ${mine ? "justify-end" : "justify-start"}`}>
          <p className="text-[14px] font-semibold leading-[1.6] text-[#0a0a0a]">
            {mine ? "You" : message.senderName}
          </p>
          <span className="text-[12px] font-normal leading-[1.6] text-[#737373]">
            {formatDateTimeParts(message.createdAtUtc)}
          </span>
        </div>
        {message.message ? (
          <p className={`mt-2 whitespace-pre-wrap break-words rounded-[12px] px-4 py-3 text-[14px] font-normal leading-[1.6] ${
          mine ? "bg-[#fdf1eb] text-[#0a0a0a]" : "bg-[#f5f5f5] text-[#0a0a0a]"
        }`}>
            {message.message}
          </p>
        ) : null}
        {message.attachments?.length ? (
          <div className="mt-2 flex flex-col gap-2">
            {message.attachments.map(attachment => (
              <AttachmentRow
                key={attachment.id}
                fileName={attachment.fileName}
                subtitle={formatFileSize(attachment.fileSizeBytes)}
                action={
                  <>
                    {attachment.viewUrl ? (
                      <a
                        href={attachment.viewUrl}
                        target="_blank"
                        rel="noreferrer"
                        aria-label="View attachment"
                        title="View"
                        className="flex h-7 w-7 items-center justify-center rounded-full text-[#737373] hover:bg-[#f5f5f5] hover:text-[#ee7132]"
                      >
                        <i className="ri-eye-line text-[16px]" aria-hidden="true" />
                      </a>
                    ) : null}
                    {attachment.downloadUrl ? (
                      <a
                        href={attachment.downloadUrl}
                        target="_blank"
                        rel="noreferrer"
                        aria-label="Download attachment"
                        title="Download"
                        className="flex h-7 w-7 items-center justify-center rounded-full text-[#737373] hover:bg-[#f5f5f5] hover:text-[#ee7132]"
                      >
                        <i className="ri-download-line text-[16px]" aria-hidden="true" />
                      </a>
                    ) : null}
                  </>
                }
              />
            ))}
          </div>
        ) : null}
      </div>
    </article>
  );
}

function AttachmentRow({
  fileName,
  subtitle,
  action,
}: {
  fileName: string;
  subtitle: string;
  action?: ReactNode;
}) {
  const title = <span className="truncate font-semibold">{fileName}</span>;

  return (
    <div className="flex min-w-0 items-center justify-between gap-3 rounded-[10px] border border-[#e5e5e5] bg-white px-3 py-2 text-left text-[12px] leading-[1.4] text-[#0a0a0a]">
      <div className="flex min-w-0 items-center gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#f5f5f5] text-[#737373]">
          <i className={`${fileIconFor(fileName)} text-[18px]`} aria-hidden="true" />
        </span>
        <div className="flex min-w-0 flex-col">
          {title}
          <span className="text-[#737373]">{subtitle}</span>
        </div>
      </div>
      {action ? <div className="flex shrink-0 items-center gap-1">{action}</div> : null}
    </div>
  );
}

function CenteredMessageEmptyState() {
  return (
    <div className="flex min-h-[260px] w-full flex-1 flex-col items-center justify-center py-10 text-center">
      <span className="flex h-10 w-10 items-center justify-center rounded-[10px] bg-[#f5f5f5] text-[#0a0a0a]">
        <i className="ri-message-3-line text-[22px]" />
      </span>
      <h2 className="mt-6 text-[20px] font-semibold leading-7 tracking-normal text-[#0a0a0a]">
        No Message Yet
      </h2>
      <p className="mt-2 max-w-[550px] text-[16px] font-normal leading-[1.6] text-[#404040]">
        There are no messages for this lien yet. Any messages related to this lien will appear here.
      </p>
    </div>
  );
}

function formatDateTimeParts(value?: string | null): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  const datePart = new Intl.DateTimeFormat("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
  }).format(date);
  const timePart = new Intl.DateTimeFormat("en-US", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(date);
  return `${datePart}  •  ${timePart}`;
}

function buildInitials(value: string): string {
  const parts = value.split(/\s+/).filter(Boolean);
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  return (value.slice(0, 2) || "SL").toUpperCase();
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(0)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}

function fileIconFor(fileName: string): string {
  const extension = fileName.split(".").pop()?.toLowerCase();
  if (extension === "pdf") return "ri-file-pdf-2-line";
  if (["jpg", "jpeg", "png"].includes(extension ?? "")) return "ri-image-line";
  if (["xlsx", "xls", "csv"].includes(extension ?? "")) return "ri-file-excel-2-line";
  if (extension === "docx") return "ri-file-word-2-line";
  return "ri-file-line";
}
