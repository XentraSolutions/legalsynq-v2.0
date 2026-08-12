export type MarkdownBlock =
  | { type: 'heading'; level: 1 | 2 | 3 | 4 | 5 | 6; text: string }
  | { type: 'paragraph'; text: string }
  | { type: 'list'; ordered: boolean; items: MarkdownBlock[][] }
  | { type: 'blockquote'; blocks: MarkdownBlock[] }
  | { type: 'code'; language: string | null; code: string }
  | { type: 'table'; headers: string[]; rows: string[][] }
  | { type: 'rule' };

export type MarkdownInlineToken =
  | { type: 'text'; value: string }
  | { type: 'strong'; children: MarkdownInlineToken[] }
  | { type: 'emphasis'; children: MarkdownInlineToken[] }
  | { type: 'code'; value: string }
  | { type: 'link'; href: string; children: MarkdownInlineToken[] };

export function parseMarkdownBlocks(input: string): MarkdownBlock[] {
  const normalized = input.replace(/\r\n?/g, '\n').trim();
  if (!normalized) return [];
  return parseBlocksFromLines(normalized.split('\n'));
}

export function parseMarkdownInlines(input: string): MarkdownInlineToken[] {
  const tokens: MarkdownInlineToken[] = [];
  let buffer = '';
  let index = 0;

  const flush = () => {
    if (!buffer) return;
    tokens.push({ type: 'text', value: buffer });
    buffer = '';
  };

  while (index < input.length) {
    if (input.startsWith('**', index) || input.startsWith('__', index)) {
      const delimiter = input.slice(index, index + 2);
      const closeIndex = input.indexOf(delimiter, index + 2);
      if (closeIndex > index + 2) {
        flush();
        tokens.push({
          type: 'strong',
          children: parseMarkdownInlines(input.slice(index + 2, closeIndex)),
        });
        index = closeIndex + 2;
        continue;
      }
    }

    if (input[index] === '`') {
      const closeIndex = input.indexOf('`', index + 1);
      if (closeIndex > index + 1) {
        flush();
        tokens.push({
          type: 'code',
          value: input.slice(index + 1, closeIndex),
        });
        index = closeIndex + 1;
        continue;
      }
    }

    if (input[index] === '[') {
      const closeLabel = input.indexOf(']', index + 1);
      const openHref = closeLabel >= 0 ? input.indexOf('(', closeLabel + 1) : -1;
      const closeHref = openHref === closeLabel + 1 ? input.indexOf(')', openHref + 1) : -1;
      if (closeLabel > index + 1 && closeHref > openHref + 1) {
        const label = input.slice(index + 1, closeLabel);
        const href = input.slice(openHref + 1, closeHref).trim();
        if (isSafeHref(href)) {
          flush();
          tokens.push({
            type: 'link',
            href,
            children: parseMarkdownInlines(label),
          });
          index = closeHref + 1;
          continue;
        }
      }
    }

    if (input[index] === '*' || input[index] === '_') {
      const delimiter = input[index];
      const previous = index > 0 ? input[index - 1] : '';
      const next = index + 1 < input.length ? input[index + 1] : '';
      if (!isWordCharacter(previous) || !isWordCharacter(next)) {
        const closeIndex = input.indexOf(delimiter, index + 1);
        if (closeIndex > index + 1) {
          flush();
          tokens.push({
            type: 'emphasis',
            children: parseMarkdownInlines(input.slice(index + 1, closeIndex)),
          });
          index = closeIndex + 1;
          continue;
        }
      }
    }

    buffer += input[index];
    index += 1;
  }

  flush();
  return tokens;
}

function parseBlocksFromLines(lines: string[]): MarkdownBlock[] {
  const blocks: MarkdownBlock[] = [];
  let index = 0;

  while (index < lines.length) {
    if (isBlank(lines[index])) {
      index += 1;
      continue;
    }

    const codeFence = lines[index].match(/^\s*```([\w-]+)?\s*$/);
    if (codeFence) {
      const language = codeFence[1] ?? null;
      const codeLines: string[] = [];
      index += 1;
      while (index < lines.length && !/^\s*```\s*$/.test(lines[index])) {
        codeLines.push(lines[index]);
        index += 1;
      }
      if (index < lines.length) index += 1;
      blocks.push({
        type: 'code',
        language,
        code: codeLines.join('\n'),
      });
      continue;
    }

    const heading = lines[index].match(/^\s*(#{1,6})\s+(.+?)\s*$/);
    if (heading) {
      blocks.push({
        type: 'heading',
        level: heading[1].length as 1 | 2 | 3 | 4 | 5 | 6,
        text: heading[2],
      });
      index += 1;
      continue;
    }

    if (isTableHeader(lines, index)) {
      const headers = parseTableRow(lines[index]);
      index += 2;

      const rows: string[][] = [];
      while (index < lines.length && isTableRow(lines[index])) {
        rows.push(normalizeTableCells(parseTableRow(lines[index]), headers.length));
        index += 1;
      }

      blocks.push({
        type: 'table',
        headers,
        rows,
      });
      continue;
    }

    if (/^\s*([-*_])(?:\s*\1){2,}\s*$/.test(lines[index])) {
      blocks.push({ type: 'rule' });
      index += 1;
      continue;
    }

    if (/^\s*>\s?/.test(lines[index])) {
      const quoteLines: string[] = [];
      while (index < lines.length && /^\s*>\s?/.test(lines[index])) {
        quoteLines.push(lines[index].replace(/^\s*>\s?/, ''));
        index += 1;
      }
      blocks.push({
        type: 'blockquote',
        blocks: parseBlocksFromLines(quoteLines),
      });
      continue;
    }

    const listMarker = parseListMarker(lines[index]);
    if (listMarker) {
      const listBlock = collectListBlock(lines, index);
      blocks.push(listBlock.block);
      index = listBlock.nextIndex;
      continue;
    }

    const paragraphLines = [lines[index].trimEnd()];
    index += 1;

    while (index < lines.length) {
      if (isBlank(lines[index]) || startsNewBlock(lines, index)) break;
      paragraphLines.push(lines[index].trimEnd());
      index += 1;
    }

    blocks.push({
      type: 'paragraph',
      text: paragraphLines.join('\n'),
    });
  }

  return blocks;
}

function collectListBlock(
  lines: string[],
  startIndex: number,
): { block: Extract<MarkdownBlock, { type: 'list' }>; nextIndex: number } {
  const firstMarker = parseListMarker(lines[startIndex]);
  if (!firstMarker) {
    return {
      block: { type: 'list', ordered: false, items: [] },
      nextIndex: startIndex + 1,
    };
  }

  const ordered = firstMarker.ordered;
  const baseIndent = firstMarker.indent;
  const items: MarkdownBlock[][] = [];
  let index = startIndex;

  while (index < lines.length) {
    const marker = parseListMarker(lines[index]);
    if (!marker || marker.indent !== baseIndent || marker.ordered !== ordered) break;

    const itemLines = [marker.content];
    index += 1;

    while (index < lines.length) {
      if (isBlank(lines[index])) {
        const nextNonEmpty = findNextNonEmptyLine(lines, index + 1);
        const nextMarker = nextNonEmpty >= 0 ? parseListMarker(lines[nextNonEmpty]) : null;
        if (nextMarker && nextMarker.indent === baseIndent && nextMarker.ordered === ordered) {
          index = nextNonEmpty;
          break;
        }
        if (nextNonEmpty >= 0 && leadingSpaces(lines[nextNonEmpty]) <= baseIndent) {
          index = nextNonEmpty;
          break;
        }

        itemLines.push('');
        index += 1;
        continue;
      }

      const nextMarker = parseListMarker(lines[index]);
      if (nextMarker && nextMarker.indent === baseIndent && nextMarker.ordered === ordered) {
        break;
      }

      const currentIndent = leadingSpaces(lines[index]);
      if (currentIndent < baseIndent) break;

      itemLines.push(lines[index].slice(Math.min(currentIndent, marker.contentIndent)));
      index += 1;
    }

    items.push(parseMarkdownBlocks(itemLines.join('\n')));
  }

  return {
    block: {
      type: 'list',
      ordered,
      items,
    },
    nextIndex: index,
  };
}

function startsNewBlock(lines: string[], index: number): boolean {
  if (index >= lines.length) return false;
  if (isBlank(lines[index])) return true;
  if (/^\s*```/.test(lines[index])) return true;
  if (/^\s*(#{1,6})\s+/.test(lines[index])) return true;
  if (/^\s*>\s?/.test(lines[index])) return true;
  if (/^\s*([-*_])(?:\s*\1){2,}\s*$/.test(lines[index])) return true;
  if (parseListMarker(lines[index])) return true;
  return isTableHeader(lines, index);
}

function parseListMarker(line: string) {
  const match = line.match(/^(\s*)([-+*]|\d+\.)\s+(.*)$/);
  if (!match) return null;

  const indent = match[1].length;
  const marker = match[2];
  return {
    indent,
    ordered: /\d+\./.test(marker),
    marker,
    content: match[3],
    contentIndent: indent + marker.length + 1,
  };
}

function parseTableRow(line: string): string[] {
  return line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map(cell => cell.trim());
}

function normalizeTableCells(cells: string[], width: number): string[] {
  const next = cells.slice(0, width);
  while (next.length < width) next.push('');
  return next;
}

function isTableHeader(lines: string[], index: number): boolean {
  return index + 1 < lines.length && isTableRow(lines[index]) && isTableDivider(lines[index + 1]);
}

function isTableRow(line: string): boolean {
  if (!line.includes('|')) return false;
  const cells = parseTableRow(line);
  return cells.length > 1 && cells.some(cell => cell.length > 0);
}

function isTableDivider(line: string): boolean {
  return /^\s*\|?(?:\s*:?-{3,}:?\s*\|)+\s*:?-{3,}:?\s*\|?\s*$/.test(line);
}

function findNextNonEmptyLine(lines: string[], startIndex: number): number {
  for (let index = startIndex; index < lines.length; index += 1) {
    if (!isBlank(lines[index])) return index;
  }
  return -1;
}

function leadingSpaces(line: string): number {
  return line.length - line.trimStart().length;
}

function isBlank(line: string): boolean {
  return line.trim().length === 0;
}

function isWordCharacter(value: string): boolean {
  return /[A-Za-z0-9]/.test(value);
}

function isSafeHref(href: string): boolean {
  return /^(https?:\/\/|mailto:|\/|#)/i.test(href);
}
