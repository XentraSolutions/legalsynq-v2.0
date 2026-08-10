import type { CaseNoteCategory } from "@/lib/liens/lien-case-notes.service";
import { avatarColor, getInitials } from "../../../utils/case-detail-utils";

export function NoteComposerSection({
  authorName,
  composerText,
  onComposerTextChange,
  composerCategory,
  onComposerCategoryChange,
  composerExpanded,
  onComposerExpandedChange,
  composerSubmitting,
  onSubmit,
  onCancel,
}: {
  authorName: string;
  composerText: string;
  onComposerTextChange: (v: string) => void;
  composerCategory: CaseNoteCategory;
  onComposerCategoryChange: (v: CaseNoteCategory) => void;
  composerExpanded: boolean;
  onComposerExpandedChange: (v: boolean) => void;
  composerSubmitting: boolean;
  onSubmit: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="px-5 py-4 border-b border-gray-100 bg-gray-50/30">
      <div
        className={[
          "border rounded-lg bg-white transition-all",
          composerExpanded
            ? "border-primary/30 shadow-sm ring-1 ring-primary/10"
            : "border-gray-200",
        ].join(" ")}
      >
        <div className="flex items-start gap-3 p-3">
          <div
            className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 text-xs font-semibold ${avatarColor(authorName)}`}
          >
            {getInitials(authorName)}
          </div>
          <div className="flex-1 min-w-0">
            <textarea
              value={composerText}
              onChange={(e) => onComposerTextChange(e.target.value)}
              onFocus={() => onComposerExpandedChange(true)}
              placeholder="Add a note to this case..."
              rows={composerExpanded ? 4 : 2}
              className="w-full text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none resize-none bg-transparent"
            />
          </div>
        </div>
        {composerExpanded && (
          <div className="px-3 pb-3 flex items-center justify-between border-t border-gray-100 pt-2.5">
            <div className="flex items-center gap-2">
              <div className="relative">
                <select
                  value={composerCategory}
                  onChange={(e) =>
                    onComposerCategoryChange(
                      e.target.value as CaseNoteCategory,
                    )
                  }
                  className="pl-2 pr-6 py-1 text-[11px] font-medium border border-gray-200 rounded-md bg-white appearance-none cursor-pointer focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none"
                >
                  <option value="general">General</option>
                  <option value="internal">Internal</option>
                  <option value="follow-up">Follow-Up</option>
                </select>
                <i className="ri-arrow-down-s-line absolute right-1.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-[10px]" />
              </div>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={onCancel}
                className="px-3 py-1.5 text-xs font-medium text-gray-500 hover:text-gray-700 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={onSubmit}
                disabled={!composerText.trim() || composerSubmitting}
                className="px-4 py-1.5 text-xs font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed transition-colors inline-flex items-center gap-1.5"
              >
                {composerSubmitting ? (
                  <i className="ri-loader-4-line text-xs animate-spin" />
                ) : (
                  <i className="ri-send-plane-line text-xs" />
                )}
                Add Note
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
