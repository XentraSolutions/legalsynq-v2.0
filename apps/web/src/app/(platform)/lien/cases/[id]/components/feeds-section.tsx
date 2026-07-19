"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { emailToDisplayName } from "@/lib/liens/note-utils";
import {
  lienCaseNotesLegacyService,
  type CaseFeedNote,
} from "@/lib/liens/lien-case-notes-legacy.service";
import { ConfirmDialog } from "@/components/lien/modal";
import type { PanelMode } from "@/components/lien/layout-split";

type FeedTab = "notes" | "email";
type FeedFilter = "newest" | "oldest" | "deleted";

const FILTER_OPTIONS: { key: FeedFilter; label: string }[] = [
  { key: "deleted", label: "Show Deleted Comment" },
  { key: "newest", label: "Newest" },
  { key: "oldest", label: "Oldest" },
];

function FilterDropdown({
  filter,
  onChange,
}: {
  filter: FeedFilter;
  onChange: (filter: FeedFilter) => void;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const filteredOptions = useMemo(
    () => FILTER_OPTIONS.filter((o) => o.label.toLowerCase().includes(search.toLowerCase())),
    [search],
  );

  const selectedLabel = FILTER_OPTIONS.find((o) => o.key === filter)?.label ?? "Select Filter";

  return (
    <div ref={containerRef} className="relative w-full">
      <button
        type="button"
        aria-label="Select Filter"
        onClick={() => setOpen((prev) => !prev)}
        className="flex w-full items-center justify-between rounded-lg border border-gray-200 bg-white px-4 py-2.5 text-left text-sm text-gray-700 hover:border-gray-300 transition-colors"
      >
        {selectedLabel}
        <i className={`ri-arrow-${open ? "up" : "down"}-s-line text-gray-500`} />
      </button>

      {open && (
        <div className="absolute z-20 mt-2 w-full rounded-lg border border-gray-200 bg-white shadow-lg">
          <div className="p-2 border-b border-gray-100">
            <input
              type="text"
              autoFocus
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Select Filter"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-primary"
            />
          </div>
          <div className="max-h-56 overflow-y-auto py-1">
            {filteredOptions.map((option) => (
              <button
                key={option.key}
                type="button"
                onClick={() => {
                  onChange(option.key);
                  setOpen(false);
                }}
                className={`flex w-full items-center justify-between px-4 py-2.5 text-left text-sm hover:bg-gray-50 transition-colors ${
                  filter === option.key ? "bg-gray-50 text-gray-800 font-medium" : "text-gray-600"
                }`}
              >
                {option.label}
                {filter === option.key && <i className="ri-check-line text-primary" />}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function NotesFeed({ caseId }: { caseId: string }) {
  const addToast = useLienStore((s) => s.addToast);

  const [notes, setNotes] = useState<CaseFeedNote[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<FeedFilter>("newest");

  const [noteText, setNoteText] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<CaseFeedNote | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadNotes = useCallback(async () => {
    setLoading(true);
    try {
      const data = await lienCaseNotesLegacyService.getNotes(
        caseId,
        filter === "deleted",
        filter === "oldest" ? "oldest" : "newest",
      );
      setNotes(data);
    } catch (err) {
      const message = err instanceof ApiError ? err.message : "Failed to load notes";
      addToast({ type: "error", title: "Load Failed", description: message });
    } finally {
      setLoading(false);
    }
  }, [caseId, filter, addToast]);

  useEffect(() => {
    loadNotes();
  }, [loadNotes]);

  const handleSubmit = useCallback(async () => {
    const trimmed = noteText.trim();
    if (!trimmed || submitting) return;
    setSubmitting(true);
    try {
      await lienCaseNotesLegacyService.addNote(caseId, trimmed);
      setNoteText("");
      await loadNotes();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : "Failed to add note";
      addToast({ type: "error", title: "Add Note Failed", description: message });
    } finally {
      setSubmitting(false);
    }
  }, [caseId, noteText, submitting, loadNotes, addToast]);

  const handleConfirmDelete = useCallback(async () => {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await lienCaseNotesLegacyService.deleteNote(pendingDelete.id);
      setPendingDelete(null);
      await loadNotes();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : "Failed to delete note";
      addToast({ type: "error", title: "Delete Failed", description: message });
    } finally {
      setDeleting(false);
    }
  }, [pendingDelete, loadNotes, addToast]);

  return (
    <div className="space-y-4">
      <FilterDropdown filter={filter} onChange={setFilter} />

      <div className="border-t border-gray-100 pt-3">
        {loading ? (
          <div className="flex justify-center py-6">
            <i className="ri-loader-4-line animate-spin text-gray-400 text-lg" />
          </div>
        ) : notes.length === 0 ? (
          <p className="py-6 text-center text-sm text-gray-400">No result found</p>
        ) : (
          <div className="space-y-2 max-h-72 overflow-y-auto">
            {notes.map((note) => (
              <div key={note.id} className="group rounded-lg bg-gray-50 px-3 py-2.5">
                <div className="flex items-start gap-3">
                  <div className="w-9 h-9 rounded-full bg-gray-200 flex items-center justify-center shrink-0">
                    <i className="ri-user-line text-gray-400 text-base" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold text-gray-800 truncate">
                          {emailToDisplayName(note.createdBy)}
                        </p>
                        <p className="text-xs text-gray-400">{note.created}</p>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        {note.isDeleted === "Y" && (
                          <span className="px-2.5 py-1 rounded-full bg-red-100 text-red-600 text-xs font-semibold">
                            Deleted
                          </span>
                        )}
                        {note.canDelete && note.isDeleted !== "Y" && (
                          <button
                            type="button"
                            aria-label="Delete note"
                            onClick={() => setPendingDelete(note)}
                            className="opacity-0 group-hover:opacity-100 text-gray-400 hover:text-red-500 transition-opacity"
                          >
                            <i className="ri-delete-bin-line text-sm" />
                          </button>
                        )}
                      </div>
                    </div>
                    <p className="mt-2 text-sm text-gray-700 whitespace-pre-wrap break-words">
                      {note.note}
                    </p>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="rounded-lg border border-gray-200 overflow-hidden">
        <textarea
          value={noteText}
          onChange={(e) => setNoteText(e.target.value)}
          placeholder="Add a note"
          rows={3}
          className="w-full resize-none px-3 py-2.5 text-sm text-gray-700 placeholder:text-gray-400 outline-none"
        />
        <div className="flex justify-end bg-gray-50 px-3 py-2">
          <button
            type="button"
            onClick={handleSubmit}
            disabled={!noteText.trim() || submitting}
            className="flex items-center gap-2 rounded-lg bg-green-500 px-4 py-1.5 text-sm font-medium text-white hover:bg-green-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            {submitting ? (
              <i className="ri-loader-4-line animate-spin text-sm" />
            ) : (
              <i className="ri-send-plane-fill text-sm" />
            )}
            Submit
          </button>
        </div>
      </div>

      <ConfirmDialog
        open={!!pendingDelete}
        onClose={() => setPendingDelete(null)}
        onConfirm={handleConfirmDelete}
        title="Delete Note"
        description="This will remove the note from the feed. This action cannot be undone."
        confirmLabel="Delete"
        confirmVariant="danger"
        loading={deleting}
      />
    </div>
  );
}

function EmailFeed() {
  const addToast = useLienStore((s) => s.addToast);

  return (
    <div className="flex justify-center py-2">
      <button
        type="button"
        onClick={() =>
          addToast({
            type: "info",
            title: "Coming Soon",
            description: "Composing email from here isn't available yet.",
          })
        }
        className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2"
      >
        <i className="ri-mail-send-line text-sm" />
        Compose New Email
      </button>
    </div>
  );
}

export function FeedsSection({
  caseId,
  panelMode,
  onPanelModeChange,
}: {
  caseId: string;
  panelMode: PanelMode;
  onPanelModeChange: (mode: PanelMode) => void;
}) {
  const [tab, setTab] = useState<FeedTab>("notes");

  const collapsed = panelMode === "left-expanded";
  const toggleCollapsed = () => onPanelModeChange(collapsed ? "split" : "left-expanded");

  if (collapsed) {
    return (
      <div className="h-full min-h-[520px] bg-white border border-gray-200 rounded-lg flex flex-col items-center py-4">
        <button
          type="button"
          onClick={toggleCollapsed}
          title="Expand Feeds"
          className="text-gray-400 hover:text-gray-600 transition-colors"
        >
          <i className="ri-arrow-left-double-line text-lg" />
        </button>
        <i className="ri-list-check-2 text-lg text-gray-500 mt-4" />
        <span
          className="mt-auto mb-6 text-sm font-semibold text-gray-800"
          style={{ writingMode: "vertical-rl" }}
        >
          Feeds
        </span>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-visible">
      <div
        className="flex items-center justify-between px-5 py-3 cursor-pointer select-none hover:bg-gray-50/50 transition-colors"
        onClick={toggleCollapsed}
      >
        <div className="flex items-center gap-2">
          <i className="ri-arrow-right-s-line text-gray-400 text-base" />
          <i className="ri-list-check-2 text-sm text-gray-500" />
          <h3 className="text-sm font-semibold text-gray-800">Feeds</h3>
        </div>
      </div>

      <div className="px-5 py-4 border-t border-gray-100 space-y-4">
        <div className="flex rounded-lg bg-gray-100 p-0.5">
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              setTab("notes");
            }}
            className={`flex-1 rounded-md py-2 text-sm font-medium transition-colors ${
              tab === "notes" ? "bg-primary text-white" : "text-gray-600 hover:text-gray-800"
            }`}
          >
            Notes
          </button>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              setTab("email");
            }}
            className={`flex-1 rounded-md py-2 text-sm font-medium transition-colors ${
              tab === "email" ? "bg-primary text-white" : "text-gray-600 hover:text-gray-800"
            }`}
          >
            Email
          </button>
        </div>

        {tab === "notes" ? <NotesFeed caseId={caseId} /> : <EmailFeed />}
      </div>
    </div>
  );
}
