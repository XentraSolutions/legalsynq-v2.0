"use client";

import { type FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { postFundingOfferedLienMessage } from "@/lib/synqlien-funding-portal/client-actions";
import type { OfferedLienMessage } from "@/lib/synqlien-funding-portal/types";

interface OfferedLienMessagesProps {
  id: string;
  initialMessages: OfferedLienMessage[];
}

const MAX_MESSAGE_LENGTH = 400;

export function OfferedLienMessages({
  id,
  initialMessages,
}: OfferedLienMessagesProps) {
  const router = useRouter();
  const threadRef = useRef<HTMLDivElement | null>(null);
  const shouldScrollRef = useRef(false);
  const [messages, setMessages] = useState(initialMessages);
  const [draft, setDraft] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const trimmedDraft = draft.trim();
  const canSend = trimmedDraft.length > 0 && !submitting;

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
    const result = await postFundingOfferedLienMessage(id, draft);
    setSubmitting(false);

    if (result.ok && result.message) {
      shouldScrollRef.current = true;
      setMessages(current => [...current, result.message!]);
      setDraft("");
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

      <form
        className="mt-6 flex items-center gap-4 rounded-[12px] border border-[#e5e5e5] bg-white py-3 pl-4 pr-3 shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors focus-within:border-[#ee7132]"
        onSubmit={handleSubmit}
      >
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
        <p className={`mt-2 whitespace-pre-wrap break-words rounded-[12px] px-4 py-3 text-[14px] font-normal leading-[1.6] ${
          mine ? "bg-[#fdf1eb] text-[#0a0a0a]" : "bg-[#f5f5f5] text-[#0a0a0a]"
        }`}>
          {message.message}
        </p>
      </div>
    </article>
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
