// Shared column-data typography for lien selling tables — every column uses the
// same weight/size/line-height/color so no column visually stands out from another.
export const TABLE_CELL_CLASSNAME =
  "text-sm font-normal leading-[160%] tracking-normal text-[#0A0A0A]";

// Row-level links (e.g. a company or lien name) use the same base styling,
// but resolve to the brand orange on hover/focus instead of the default blue primary color.
export const TABLE_LINK_CLASSNAME = `${TABLE_CELL_CLASSNAME} hover:text-[#EE7132]`;

// Header row background for lien selling tables, overriding BaseTable's default #eaeff3.
export const TABLE_HEADER_CLASSNAME = "bg-[#F5F5F5]";

// Header cell typography — same 14px/500/160%/0 treatment across every column,
// overriding BaseTable's default 12px header text. No new font-family — keeps
// whatever the app's default is instead of loading one in.
export const TABLE_HEADER_CELL_CLASSNAME =
  "text-sm font-medium leading-[160%] tracking-normal normal-case";
