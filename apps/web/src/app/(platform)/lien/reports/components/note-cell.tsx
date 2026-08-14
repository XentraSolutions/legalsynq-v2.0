import { useState } from "react";

export const NoteCell = ({ value, maxLength= 70 }: { value: any, maxLength?:number }) => {
  const [expanded, setExpanded] = useState(false);

  const text = String(value ?? "");
  const shouldTruncate = text.length > maxLength;

  const displayedText =
    expanded || !shouldTruncate
      ? text
      : `${text.slice(0, maxLength)}...`;

  return (
    <div className="text-sm text-gray-700">
      <span>{displayedText}</span>

      {shouldTruncate && (
        <button
          type="button"
          onClick={() => setExpanded((prev) => !prev)}
          className="ml-2 text-blue-600 hover:text-blue-800 hover:underline"
        >
          {expanded ? "See Less" : "See More"}
        </button>
      )}
    </div>
  );
};
