import type {
  CaseNoteCategory,
  CaseNoteResponse,
} from "@/lib/liens/lien-case-notes.service";
import { isNoteOwner } from "@/lib/liens/note-utils";
import {
  avatarColor,
  formatNoteDate,
  formatNoteTimestamp,
  getInitials,
} from "../../../utils/case-detail-utils";

const NOTE_CATEGORY_LABELS: Record<string, string> = {
  general: "General",
  internal: "Internal",
  "follow-up": "Follow-Up",
};

const NOTE_CATEGORY_COLORS: Record<string, string> = {
  general: "bg-blue-50 text-blue-600 border-blue-200",
  internal: "bg-purple-50 text-purple-600 border-purple-200",
  "follow-up": "bg-amber-50 text-amber-600 border-amber-200",
};

export function NotesListSection({
  notesLoading,
  notesError,
  onRetry,
  filteredNotes,
  hasActiveFilters,
  onClearFilters,
  currentUserId,
  timezone,
  editingNoteId,
  editingText,
  onEditingTextChange,
  editingCategory,
  onEditingCategoryChange,
  editSubmitting,
  onStartEdit,
  onCancelEdit,
  onSaveEdit,
  deletingNoteId,
  onDelete,
  pinningNoteId,
  onPin,
}: {
  notesLoading: boolean;
  notesError: string | null;
  onRetry: () => void;
  filteredNotes: CaseNoteResponse[];
  hasActiveFilters: boolean;
  onClearFilters: () => void;
  currentUserId: string | undefined;
  timezone: string;
  editingNoteId: string | null;
  editingText: string;
  onEditingTextChange: (v: string) => void;
  editingCategory: CaseNoteCategory;
  onEditingCategoryChange: (v: CaseNoteCategory) => void;
  editSubmitting: boolean;
  onStartEdit: (note: CaseNoteResponse) => void;
  onCancelEdit: () => void;
  onSaveEdit: (note: CaseNoteResponse) => void;
  deletingNoteId: string | null;
  onDelete: (noteId: string) => void;
  pinningNoteId: string | null;
  onPin: (note: CaseNoteResponse) => void;
}) {
  if (notesLoading) {
    return (
      <div className="text-center py-8">
        <i className="ri-loader-4-line text-2xl text-gray-300 animate-spin" />
        <p className="text-sm text-gray-400 mt-2">Loading notes...</p>
      </div>
    );
  }

  if (notesError) {
    return (
      <div className="text-center py-8">
        <i className="ri-error-warning-line text-2xl text-red-300" />
        <p className="text-sm text-red-500 mt-2">{notesError}</p>
        <button
          onClick={onRetry}
          className="text-xs text-primary hover:text-primary/80 mt-2 transition-colors"
        >
          Retry
        </button>
      </div>
    );
  }

  if (filteredNotes.length === 0) {
    return (
      <div className="text-center py-8">
        <i
          className={`${hasActiveFilters ? "ri-filter-off-line" : "ri-chat-quote-line"} text-2xl text-gray-300`}
        />
        <p className="text-sm text-gray-400 mt-2">
          {hasActiveFilters
            ? "No notes match the current filters"
            : "No notes yet"}
        </p>
        {hasActiveFilters && (
          <button
            onClick={onClearFilters}
            className="text-xs text-primary hover:text-primary/80 mt-1 transition-colors"
          >
            Clear filters
          </button>
        )}
        {!hasActiveFilters && (
          <p className="text-xs text-gray-300 mt-1">
            Use the composer above to add the first note
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="relative">
      <div className="absolute left-[19px] top-4 bottom-4 w-px bg-gray-100" />

      <div className="space-y-0">
        {filteredNotes.map((note, idx) => {
          const noteDate = new Date(note.createdAtUtc);
          const noteDateStr = isNaN(noteDate.getTime())
            ? ""
            : noteDate.toDateString();
          const prevDate =
            idx > 0 ? new Date(filteredNotes[idx - 1].createdAtUtc) : null;
          const prevDateStr =
            prevDate && !isNaN(prevDate.getTime())
              ? prevDate.toDateString()
              : "";
          const showDateSeparator = idx === 0 || noteDateStr !== prevDateStr;
          const isOwner = isNoteOwner(currentUserId, note.createdByUserId);
          const isEditing = editingNoteId === note.id;
          const isDeleting = deletingNoteId === note.id;
          const isPinning = pinningNoteId === note.id;

          return (
            <div key={note.id}>
              {showDateSeparator && noteDateStr && (
                <div className="flex items-center gap-3 py-2 pl-[30px]">
                  <span className="text-[10px] font-semibold text-gray-400 uppercase tracking-wide">
                    {noteDate.toLocaleDateString("en-US", {
                      weekday: "long",
                      month: "short",
                      day: "numeric",
                      timeZone: timezone,
                    })}
                  </span>
                  <div className="flex-1 h-px bg-gray-100" />
                </div>
              )}

              <div className="flex gap-3 py-2.5 group relative">
                <div className="relative z-10 shrink-0">
                  <div
                    className={`w-[38px] h-[38px] rounded-full flex items-center justify-center text-[11px] font-semibold ${avatarColor(note.createdByName)}`}
                  >
                    {getInitials(note.createdByName)}
                  </div>
                </div>

                <div className="flex-1 min-w-0">
                  {isEditing ? (
                    <div className="bg-white rounded-lg border border-primary/30 shadow-sm ring-1 ring-primary/10 px-4 py-3">
                      <div className="flex items-center gap-2 mb-2">
                        <div className="relative">
                          <select
                            value={editingCategory}
                            onChange={(e) =>
                              onEditingCategoryChange(
                                e.target.value as CaseNoteCategory,
                              )
                            }
                            className="pl-2 pr-6 py-0.5 text-[11px] font-medium border border-gray-200 rounded-md bg-white appearance-none cursor-pointer focus:border-primary/40 outline-none"
                          >
                            <option value="general">General</option>
                            <option value="internal">Internal</option>
                            <option value="follow-up">Follow-Up</option>
                          </select>
                          <i className="ri-arrow-down-s-line absolute right-1.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-[10px]" />
                        </div>
                      </div>
                      <textarea
                        value={editingText}
                        onChange={(e) => onEditingTextChange(e.target.value)}
                        rows={4}
                        className="w-full text-sm text-gray-700 focus:outline-none resize-none bg-transparent"
                        autoFocus
                      />
                      <div className="flex items-center justify-end gap-2 mt-2 pt-2 border-t border-gray-100">
                        <button
                          onClick={onCancelEdit}
                          className="px-3 py-1 text-xs font-medium text-gray-500 hover:text-gray-700 transition-colors"
                        >
                          Cancel
                        </button>
                        <button
                          onClick={() => onSaveEdit(note)}
                          disabled={!editingText.trim() || editSubmitting}
                          className="px-3 py-1 text-xs font-medium text-white bg-primary rounded-md hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed inline-flex items-center gap-1 transition-colors"
                        >
                          {editSubmitting ? (
                            <i className="ri-loader-4-line text-xs animate-spin" />
                          ) : null}
                          Save
                        </button>
                      </div>
                    </div>
                  ) : (
                    <div className="bg-gray-50 rounded-lg px-4 py-3 border border-gray-100 hover:border-gray-200 transition-colors">
                      <div className="flex items-center gap-2 mb-1.5">
                        <span className="text-xs font-semibold text-gray-700">
                          {note.createdByName}
                        </span>
                        {note.category && note.category !== "general" && (
                          <span
                            className={`inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded border ${NOTE_CATEGORY_COLORS[note.category]}`}
                          >
                            {NOTE_CATEGORY_LABELS[note.category]}
                          </span>
                        )}
                        {note.isPinned && (
                          <span className="inline-flex items-center gap-0.5 text-[10px] text-amber-500">
                            <i className="ri-pushpin-2-fill text-[10px]" />
                            Pinned
                          </span>
                        )}
                        {note.isEdited && (
                          <span
                            className="text-[10px] text-gray-400 italic"
                            title={
                              note.updatedAtUtc
                                ? `Edited ${formatNoteTimestamp(note.updatedAtUtc, timezone)}`
                                : "Edited"
                            }
                          >
                            edited
                          </span>
                        )}
                        <span
                          className="text-[11px] text-gray-400 ml-auto"
                          title={formatNoteTimestamp(
                            note.createdAtUtc,
                            timezone,
                          )}
                        >
                          {formatNoteDate(note.createdAtUtc, timezone)}
                        </span>
                        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                          <button
                            onClick={() => onPin(note)}
                            disabled={isPinning}
                            title={note.isPinned ? "Unpin" : "Pin"}
                            className="p-1 rounded text-gray-400 hover:text-amber-500 hover:bg-amber-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                          >
                            {isPinning ? (
                              <i className="ri-loader-4-line text-xs animate-spin" />
                            ) : (
                              <i
                                className={`${note.isPinned ? "ri-pushpin-fill" : "ri-pushpin-line"} text-xs`}
                              />
                            )}
                          </button>
                          {isOwner && (
                            <>
                              <button
                                onClick={() => onStartEdit(note)}
                                disabled={isDeleting || isPinning}
                                title="Edit"
                                className="p-1 rounded text-gray-400 hover:text-primary hover:bg-primary/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                              >
                                <i className="ri-edit-line text-xs" />
                              </button>
                              <button
                                onClick={() => onDelete(note.id)}
                                disabled={isDeleting || isPinning}
                                title="Delete"
                                className="p-1 rounded text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                              >
                                {isDeleting ? (
                                  <i className="ri-loader-4-line text-xs animate-spin" />
                                ) : (
                                  <i className="ri-delete-bin-line text-xs" />
                                )}
                              </button>
                            </>
                          )}
                        </div>
                      </div>
                      <p className="text-sm text-gray-600 leading-relaxed whitespace-pre-wrap">
                        {note.content}
                      </p>
                    </div>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
