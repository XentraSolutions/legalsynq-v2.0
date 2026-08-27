"use client";

import { useRef, useState } from "react";
import { MessagesSquare, Paperclip, Send } from "lucide-react";
import { fileIconFor, UploadedFileRow } from "./uploaded-file-row";
import { ContactsEmptyState } from "./contacts/contacts-empty-state";

export interface MessageAttachment {
  id: string;
  name: string;
  sizeLabel: string;
}

export interface MessageItem {
  id: string;
  senderName: string;
  isMine: boolean;
  body: string;
  timestampUtc: string;
  attachments?: MessageAttachment[];
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(0)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}

function formatTimestamp(value: string): string {
  const date = new Date(value);
  return new Intl.DateTimeFormat("en-US", {
    month: "long",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

// UI-only scaffold for messaging — there's no backend yet for either the
// case-level or lien-level thread, so sent messages only live in local
// state for this render. Wire this up to a real endpoint once one exists;
// the shape of MessageItem/MessageAttachment is meant to map directly onto
// whatever that response ends up looking like.
export function MessagesTab({
  emptyTitle = "No Messages Yet",
  emptyDescription = "Start the conversation below. Messaging isn't wired up yet — sent messages are shown here for preview only and won't be saved.",
}: {
  emptyTitle?: string;
  emptyDescription?: string;
}) {
  const [messages, setMessages] = useState<MessageItem[]>([]);
  const [draft, setDraft] = useState("");
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const canSend = draft.trim().length > 0 || pendingFiles.length > 0;

  const handleSend = () => {
    if (!canSend) return;
    setMessages((current) => [
      ...current,
      {
        id: crypto.randomUUID(),
        senderName: "You",
        isMine: true,
        body: draft.trim(),
        timestampUtc: new Date().toISOString(),
        attachments: pendingFiles.map((file) => ({
          id: crypto.randomUUID(),
          name: file.name,
          sizeLabel: formatSize(file.size),
        })),
      },
    ]);
    setDraft("");
    setPendingFiles([]);
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-6 py-5 space-y-5">
        <h3 className="text-md font-semibold">Messages</h3>

        {messages.length === 0 ? (
          <ContactsEmptyState
            icon={MessagesSquare}
            title={emptyTitle}
            description={emptyDescription}
          />
        ) : (
          <div className="flex flex-col gap-3 max-h-[480px] overflow-y-auto pr-1">
            {messages.map((message) => (
              <div
                key={message.id}
                className={`flex ${message.isMine ? "justify-end" : "justify-start"}`}
              >
                <div
                  className={`max-w-[80%] rounded-2xl px-4 py-3 text-sm space-y-2 ${
                    message.isMine
                      ? "bg-[#EE7132]/10 text-gray-900"
                      : "border border-gray-200 bg-gray-50 text-gray-900"
                  }`}
                >
                  <div className="flex items-center gap-2 text-xs font-medium text-gray-500">
                    <span>{message.senderName}</span>
                    <span aria-hidden="true">·</span>
                    <span>{formatTimestamp(message.timestampUtc)}</span>
                  </div>
                  {message.body && (
                    <p className="whitespace-pre-wrap break-words">{message.body}</p>
                  )}
                  {message.attachments?.map((file) => (
                    <UploadedFileRow
                      key={file.id}
                      icon={fileIconFor(file.name)}
                      title={file.name}
                      subtitle={file.sizeLabel}
                    />
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}

        {pendingFiles.length > 0 && (
          <div className="space-y-2">
            {pendingFiles.map((file, index) => (
              <UploadedFileRow
                key={`${file.name}-${index}`}
                icon={fileIconFor(file.name)}
                title={file.name}
                subtitle={formatSize(file.size)}
                actions={
                  <button
                    type="button"
                    className="text-xs text-gray-400 hover:text-red-600"
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
        )}

        <div className="flex items-center gap-2 rounded-xl border border-gray-200 px-3 py-2 focus-within:border-[#EE7132]">
          <input
            ref={fileInputRef}
            type="file"
            multiple
            className="hidden"
            onChange={(e) => {
              const files = Array.from(e.target.files ?? []);
              if (files.length) setPendingFiles((current) => [...current, ...files]);
              e.target.value = "";
            }}
          />
          <input
            type="text"
            placeholder="Type a message..."
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                handleSend();
              }
            }}
            className="flex-1 min-w-0 text-sm outline-none"
          />
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            className="w-8 h-8 flex items-center justify-center rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-50 shrink-0"
            aria-label="Attach file"
          >
            <Paperclip className="h-4 w-4" />
          </button>
          <button
            type="button"
            onClick={handleSend}
            disabled={!canSend}
            className="w-9 h-9 rounded-full bg-[#EE7132] text-white flex items-center justify-center shrink-0 disabled:bg-[#F7B899] disabled:cursor-not-allowed hover:bg-[#D9672E] transition-colors"
            aria-label="Send message"
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
