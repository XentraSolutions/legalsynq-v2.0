import { useTanStackGlobalSearch } from '@/hooks/use-global-search';
import { LienListItem } from '@/lib/liens';
import { Search } from 'lucide-react';
import { useRouter } from 'next/navigation';
import React, { useRef } from 'react';

export const GlobalSearch: React.FC = () => {
  const router = useRouter()
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
  console.log(results)
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
          onChange={(e) => {  setInputValue(e.target.value);
            if (!isOpen) setIsOpen(true)}}
          onFocus={()=>{ if (inputValue.trim().length >= 2) setIsOpen(true);}}
          onBlur={()=> setIsOpen(false)}
          className="bg-white w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
        />
     </div>

      {/* Dropdown Content */}
      {isOpen && inputValue.trim().length >= 2 && (
        <div className="absolute left-0 right-0 mt-2 bg-white border-gray-100 rounded-lg shadow-lg z-50 max-h-[60vh] overflow-y-auto">
          {error && <div className="p-4 text-center text-sm text-red-500">{error}</div>}

          {isLoading && results?.length === 0 && (
            <div className="p-4 text-center text-sm text-gray-400">Searching...</div>
          )}

          {!isLoading && results?.length > 0 && (
            results.map((l) => (
            <button
                key={l.id}
                type="button"
                onClick={() => {
                //   setSearchFocused(false);
                router.push(lienDetailHref(l));
                }}
                className="w-full text-left px-4 py-2.5 hover:bg-gray-50 border-b border-gray-100 last:border-b-0"
            >
                <div className="text-sm font-semibold text-gray-800">
                {l.isConfidential
                    ? "Confidential"
                    : l.plaintiff || l.subjectName}
                </div>
                <div className="text-xs text-gray-500 mt-0.5">
                <span className="text-primary">Initial Service Date: </span>
                <span className="text-gray-700">
                    {l.initialServiceDate || "—"}
                </span>
                {", "}
                <span className="text-primary">Purchase Date: </span>
                <span className="text-gray-700">{l.purchaseDate || "—"}</span>
                {l.lawFirm ? `, ${l.lawFirm}` : ""}{" "}
                <span className="text-primary">Lien ID: </span>
                <span className="text-gray-700">{l.lienNumber}</span>
                </div>
            </button>
        ))
      )}

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