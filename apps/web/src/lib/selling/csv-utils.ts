// Minimal CSV helpers for the case-scoped bulk-upload template, which drops
// the "Case Code*" column (the case is already known from the page) and
// re-adds it before the file is sent to the shared bulk-import endpoint.
// Handles quoted fields but not embedded newlines within a quoted field —
// the bulk-import template's columns are plain scalars, so this is enough.

function splitCsvLine(line: string): string[] {
  const fields: string[] = [];
  let current = "";
  let inQuotes = false;

  for (let i = 0; i < line.length; i++) {
    const char = line[i];
    if (inQuotes) {
      if (char === '"') {
        if (line[i + 1] === '"') {
          current += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        current += char;
      }
    } else if (char === '"') {
      inQuotes = true;
    } else if (char === ",") {
      fields.push(current);
      current = "";
    } else {
      current += char;
    }
  }
  fields.push(current);
  return fields;
}

function joinCsvLine(fields: string[]): string {
  return fields
    .map((field) =>
      /[",\r\n]/.test(field) ? `"${field.replace(/"/g, '""')}"` : field,
    )
    .join(",");
}

function mapCsvLines(
  csvText: string,
  transform: (fields: string[], lineIndex: number) => string[],
): string {
  const lines = csvText.split(/\r\n|\n|\r/);
  const trailingBlank = lines.length > 0 && lines[lines.length - 1] === "";
  const body = trailingBlank ? lines.slice(0, -1) : lines;

  const out = body.map((line, idx) => joinCsvLine(transform(splitCsvLine(line), idx)));
  return trailingBlank ? out.join("\r\n") + "\r\n" : out.join("\r\n");
}

export function stripFirstCsvColumn(csvText: string): string {
  return mapCsvLines(csvText, (fields) => fields.slice(1));
}

export function prependCsvColumn(
  csvText: string,
  header: string,
  value: string,
): string {
  return mapCsvLines(csvText, (fields, idx) => [
    idx === 0 ? header : value,
    ...fields,
  ]);
}
