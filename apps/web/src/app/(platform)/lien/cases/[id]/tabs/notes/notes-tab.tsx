"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useLienStore } from "@/stores/lien-store";
import { useSession } from "@/hooks/use-session";
import { useTimezone } from "@/lib/use-timezone";
import {
  lienCaseNotesService,
  type CaseNoteResponse,
  type CaseNoteCategory,
} from "@/lib/liens/lien-case-notes.service";
import { emailToDisplayName } from "@/lib/liens/note-utils";
import { NoteComposerSection } from "./sections/note-composer-section";
import { NotesFilterBarSection } from "./sections/notes-filter-bar-section";
import { NotesListSection } from "./sections/notes-list-section";

export function NotesTab({ caseId }: { caseId: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const { session } = useSession();
  const timezone = useTimezone();

  const [notes, setNotes] = useState<CaseNoteResponse[]>([]);
  const [notesLoading, setNotesLoading] = useState(true);
  const [notesError, setNotesError] = useState<string | null>(null);

  const [composerText, setComposerText] = useState("");
  const [composerCategory, setComposerCategory] =
    useState<CaseNoteCategory>("general");
  const [composerExpanded, setComposerExpanded] = useState(false);
  const [composerSubmitting, setComposerSubmitting] = useState(false);

  const [editingNoteId, setEditingNoteId] = useState<string | null>(null);
  const [editingText, setEditingText] = useState("");
  const [editingCategory, setEditingCategory] =
    useState<CaseNoteCategory>("general");
  const [editSubmitting, setEditSubmitting] = useState(false);
  const [deletingNoteId, setDeletingNoteId] = useState<string | null>(null);
  const [pinningNoteId, setPinningNoteId] = useState<string | null>(null);

  const [sortOrder, setSortOrder] = useState<"newest" | "oldest">("newest");
  const [categoryFilter, setCategoryFilter] = useState<
    "all" | CaseNoteCategory
  >("all");
  const [searchQuery, setSearchQuery] = useState("");

  const authorName = emailToDisplayName(session?.email);
  const currentUserId = session?.userId;

  const loadNotes = useCallback(async () => {
    setNotesLoading(true);
    setNotesError(null);
    try {
      const data = await lienCaseNotesService.getNotes(caseId);
      setNotes(data);
    } catch {
      setNotesError("Failed to load notes");
    } finally {
      setNotesLoading(false);
    }
  }, [caseId]);

  useEffect(() => {
    loadNotes();
  }, [loadNotes]);

  const filteredNotes = useMemo(() => {
    let result = [...notes];

    if (categoryFilter !== "all") {
      result = result.filter((n) => n.category === categoryFilter);
    }

    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (n) =>
          n.content.toLowerCase().includes(q) ||
          n.createdByName.toLowerCase().includes(q),
      );
    }

    result.sort((a, b) => {
      const ta = new Date(a.createdAtUtc).getTime() || 0;
      const tb = new Date(b.createdAtUtc).getTime() || 0;
      return sortOrder === "newest" ? tb - ta : ta - tb;
    });

    const pinned = result.filter((n) => n.isPinned);
    const unpinned = result.filter((n) => !n.isPinned);
    return [...pinned, ...unpinned];
  }, [notes, categoryFilter, searchQuery, sortOrder]);

  const hasActiveFilters =
    categoryFilter !== "all" || searchQuery.trim() !== "";

  const handleSubmit = async () => {
    const text = composerText.trim();
    if (!text || composerSubmitting) return;
    setComposerSubmitting(true);
    try {
      const created = await lienCaseNotesService.createNote(
        caseId,
        text,
        composerCategory,
        authorName,
      );
      setNotes((prev) => [created, ...prev]);
      setComposerText("");
      setComposerCategory("general");
      setComposerExpanded(false);
      addToast({
        type: "success",
        title: "Note Added",
        description: "Your note was saved.",
      });
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to add note.",
      });
    } finally {
      setComposerSubmitting(false);
    }
  };

  const handleStartEdit = (note: CaseNoteResponse) => {
    setEditingNoteId(note.id);
    setEditingText(note.content);
    setEditingCategory(note.category);
  };

  const handleCancelEdit = () => {
    setEditingNoteId(null);
    setEditingText("");
  };

  const handleSaveEdit = async (note: CaseNoteResponse) => {
    if (editSubmitting) return;
    setEditSubmitting(true);
    try {
      const updated = await lienCaseNotesService.updateNote(
        caseId,
        note.id,
        editingText.trim(),
        editingCategory,
      );
      setNotes((prev) => prev.map((n) => (n.id === updated.id ? updated : n)));
      setEditingNoteId(null);
      addToast({
        type: "success",
        title: "Note Updated",
        description: "Your note was saved.",
      });
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to update note.",
      });
    } finally {
      setEditSubmitting(false);
    }
  };

  const handleDelete = async (noteId: string) => {
    if (deletingNoteId === noteId) return;
    setDeletingNoteId(noteId);
    try {
      await lienCaseNotesService.deleteNote(caseId, noteId);
      setNotes((prev) => prev.filter((n) => n.id !== noteId));
      addToast({
        type: "success",
        title: "Note Deleted",
        description: "The note was removed.",
      });
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to delete note.",
      });
    } finally {
      setDeletingNoteId(null);
    }
  };

  const handlePin = async (note: CaseNoteResponse) => {
    if (pinningNoteId === note.id) return;
    setPinningNoteId(note.id);
    try {
      const updated = note.isPinned
        ? await lienCaseNotesService.unpinNote(caseId, note.id)
        : await lienCaseNotesService.pinNote(caseId, note.id);
      setNotes((prev) => prev.map((n) => (n.id === updated.id ? updated : n)));
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to update pin status.",
      });
    } finally {
      setPinningNoteId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 flex items-center justify-between border-b border-gray-100">
          <div className="flex items-center gap-2">
            <i className="ri-chat-quote-line text-sm text-gray-500" />
            <h3 className="text-sm font-semibold text-gray-800">Case Notes</h3>
            {!notesLoading && (
              <span className="ml-1 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
                {filteredNotes.length}
                {hasActiveFilters ? `/${notes.length}` : ""}
              </span>
            )}
          </div>
          <p className="text-[11px] text-gray-400">
            Internal case commentary and collaboration
          </p>
        </div>

        <NoteComposerSection
          authorName={authorName}
          composerText={composerText}
          onComposerTextChange={setComposerText}
          composerCategory={composerCategory}
          onComposerCategoryChange={setComposerCategory}
          composerExpanded={composerExpanded}
          onComposerExpandedChange={setComposerExpanded}
          composerSubmitting={composerSubmitting}
          onSubmit={handleSubmit}
          onCancel={() => {
            setComposerExpanded(false);
            setComposerText("");
          }}
        />

        <NotesFilterBarSection
          searchQuery={searchQuery}
          onSearchQueryChange={setSearchQuery}
          categoryFilter={categoryFilter}
          onCategoryFilterChange={setCategoryFilter}
          sortOrder={sortOrder}
          onSortOrderChange={setSortOrder}
        />

        <div className="px-5 py-4">
          <NotesListSection
            notesLoading={notesLoading}
            notesError={notesError}
            onRetry={loadNotes}
            filteredNotes={filteredNotes}
            hasActiveFilters={hasActiveFilters}
            onClearFilters={() => {
              setCategoryFilter("all");
              setSearchQuery("");
            }}
            currentUserId={currentUserId}
            timezone={timezone}
            editingNoteId={editingNoteId}
            editingText={editingText}
            onEditingTextChange={setEditingText}
            editingCategory={editingCategory}
            onEditingCategoryChange={setEditingCategory}
            editSubmitting={editSubmitting}
            onStartEdit={handleStartEdit}
            onCancelEdit={handleCancelEdit}
            onSaveEdit={handleSaveEdit}
            deletingNoteId={deletingNoteId}
            onDelete={handleDelete}
            pinningNoteId={pinningNoteId}
            onPin={handlePin}
          />
        </div>

        <div className="px-5 py-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">
            {notesLoading
              ? "Loading..."
              : `${filteredNotes.length} note${filteredNotes.length !== 1 ? "s" : ""}${hasActiveFilters ? ` (filtered from ${notes.length})` : ""}`}
          </p>
        </div>
      </div>
    </div>
  );
}
