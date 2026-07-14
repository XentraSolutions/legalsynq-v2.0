import type { CaseNoteCategory } from "@/lib/liens/lien-case-notes.service";

const NOTE_CATEGORY_LABELS: Record<string, string> = {
  general: "General",
  internal: "Internal",
  "follow-up": "Follow-Up",
};

export function NotesFilterBarSection({
  searchQuery,
  onSearchQueryChange,
  categoryFilter,
  onCategoryFilterChange,
  sortOrder,
  onSortOrderChange,
}: {
  searchQuery: string;
  onSearchQueryChange: (v: string) => void;
  categoryFilter: "all" | CaseNoteCategory;
  onCategoryFilterChange: (v: "all" | CaseNoteCategory) => void;
  sortOrder: "newest" | "oldest";
  onSortOrderChange: (v: "newest" | "oldest") => void;
}) {
  return (
    <div className="px-5 py-2.5 border-b border-gray-100 flex items-center gap-2 flex-wrap">
      <div className="relative flex-1 min-w-[160px] max-w-[240px]">
        <i className="ri-search-line absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-xs" />
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => onSearchQueryChange(e.target.value)}
          placeholder="Search notes..."
          className="w-full pl-7 pr-3 py-1.5 text-xs border border-gray-200 rounded-lg bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
        />
        {searchQuery && (
          <button
            onClick={() => onSearchQueryChange("")}
            className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
          >
            <i className="ri-close-line text-xs" />
          </button>
        )}
      </div>

      <div className="flex items-center bg-gray-100 rounded-lg p-0.5">
        {(["all", "general", "internal", "follow-up"] as const).map((cat) => (
          <button
            key={cat}
            onClick={() => onCategoryFilterChange(cat)}
            className={[
              "px-2.5 py-1 text-[11px] font-medium rounded-md transition-colors",
              categoryFilter === cat
                ? "bg-white text-gray-800 shadow-sm"
                : "text-gray-500 hover:text-gray-700",
            ].join(" ")}
          >
            {cat === "all" ? "All" : NOTE_CATEGORY_LABELS[cat]}
          </button>
        ))}
      </div>

      <div className="ml-auto flex items-center gap-1.5">
        <button
          onClick={() =>
            onSortOrderChange(sortOrder === "newest" ? "oldest" : "newest")
          }
          className="px-2.5 py-1.5 text-[11px] font-medium text-gray-500 border border-gray-200 rounded-lg bg-white hover:border-gray-300 inline-flex items-center gap-1 transition-colors"
        >
          <i
            className={`ri-sort-${sortOrder === "newest" ? "desc" : "asc"} text-xs`}
          />
          {sortOrder === "newest" ? "Newest First" : "Oldest First"}
        </button>
      </div>
    </div>
  );
}
