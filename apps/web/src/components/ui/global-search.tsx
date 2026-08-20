import { useTanStackGlobalSearch } from "@/hooks/use-global-search";
import { LienListItem } from "@/lib/liens";
import { Search } from "lucide-react";
import { useRouter } from "next/navigation";
import React, { useRef } from "react";

export const GlobalSearch: React.FC = () => {
  const router = useRouter();
  const {
    inputValue,
    setInputValue,
    results,
    isLoading,
    isOpen,
    setIsOpen,
    error,
    clearSearch,
  } = useTanStackGlobalSearch(350, 2);
  console.log(results);
  function lienDetailHref(lien: LienListItem): string {
    return lien.caseId
      ? `/lien/cases/${lien.caseId}/liens/${lien.id}`
      : `/lien/liens/${lien.id}`;
  }

  const containerRef = useRef<HTMLDivElement>(null);
  return (
    <div className="relative" ref={containerRef}>
      {/* Input Field */}
      <div className="relative flex-1 min-w-[500px] max-w-2xl w-full">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
        <input
          type="text"
          placeholder={"Seach something..."}
          value={inputValue}
          onChange={(e) => {
            setInputValue(e.target.value);
            if (!isOpen) setIsOpen(true);
          }}
          onFocus={() => {
            if (inputValue.trim().length >= 2) setIsOpen(true);
          }}
          // onBlur={() => setIsOpen(false)}
          className="bg-white w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
        />
      </div>

      {/* Dropdown Content */}
      {isOpen && inputValue.trim().length >= 2 && (
        <div className="absolute left-0 right-0 mt-2 bg-white border-gray-100 rounded-lg shadow-lg z-50 max-h-[60vh] overflow-y-auto">
          {error && (
            <div className="p-4 text-center text-sm text-red-500">{error}</div>
          )}

          {isLoading && results?.length === 0 && (
            <div className="p-4 text-center text-sm text-gray-400">
              Searching...
            </div>
          )}

          {!isLoading &&
            results?.length > 0 &&
            results.map((l: any, index: number) => (
              <div
                key={index}
                className="w-full text-left py-2 border-b border-gray-100 last:border-b-0"
              >
                {/* Category Title Header */}
                <div className="text-sm font-bold px-4 text-primary uppercase tracking-wider mb-1">
                  {l.title}
                </div>

                {/* Items Loop */}
                {l.items.map((item: any, itemIndex: number) => (
                  <div
                    key={item.id || itemIndex}
                    className="py-2 px-4 hover:bg-gray-50 cursor-pointer transition-colors"
                    onClick={() => {
                      setIsOpen(false);
                      router.push(item.url);
                    }}
                  >
                    {/* Main Item Name */}
                    <div className="text-sm font-medium text-gray-900">
                      {item.name}
                    </div>

                    {/* Inline Nested Details Loop */}
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 mt-1">
                      {item.details &&
                        item.details.map((detail: any, detailIndex: number) => (
                          <div
                            key={detailIndex}
                            className="inline-flex items-center space-x-1 text-xs"
                          >
                            <span className="text-primary font-medium">
                              {detail.title}:
                            </span>
                            <span className="text-gray-700">
                              {detail.description}
                            </span>
                          </div>
                        ))}
                    </div>
                  </div>
                ))}
              </div>
            ))}

          {!isLoading && results.length === 0 && !error && (
            <div className="p-4 text-center text-sm text-gray-500">
              No results found for "{inputValue}"
            </div>
          )}
        </div>
      )}
    </div>
  );
};
