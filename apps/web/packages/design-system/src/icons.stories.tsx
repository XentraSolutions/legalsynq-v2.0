import { useMemo, useState } from 'react';
import type { Meta, StoryObj } from '@storybook/nextjs';
import { DynamicIcon, iconNames, type IconName } from 'lucide-react/dynamic';

function IconTile({ name }: { name: IconName }) {
  const [copied, setCopied] = useState(false);

  return (
    <button
      type="button"
      title={name}
      onClick={() => {
        navigator.clipboard?.writeText(name);
        setCopied(true);
        setTimeout(() => setCopied(false), 1000);
      }}
      className="flex flex-col items-center gap-2 rounded-lg border border-gray-200 p-3 text-center hover:border-gray-300 hover:bg-gray-50"
    >
      <DynamicIcon name={name} size={20} className="text-gray-700" />
      <span className="line-clamp-2 w-full text-[10px] break-words text-gray-500">
        {copied ? 'Copied!' : name}
      </span>
    </button>
  );
}

function IconsPage() {
  const [query, setQuery] = useState('');

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return iconNames;
    return iconNames.filter((name) => name.includes(q));
  }, [query]);

  return (
    <div className="p-6">
      <h2 className="mb-1 text-lg font-semibold text-gray-900">Icons</h2>
      <p className="mb-4 text-sm text-gray-500">
        Lucide icon set via <code>lucide-react</code>. Use <code>{"<DynamicIcon name=\"…\" />"}</code>{' '}
        (from <code>lucide-react/dynamic</code>) to load an icon by name, or import the named
        component directly (e.g. <code>{'import { ArrowRight } from \'lucide-react\''}</code>) when it's
        known at build time. Click an icon to copy its name.
      </p>

      <div className="mb-6 flex flex-wrap items-center gap-3">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search icons…"
          className="w-64 rounded-md border border-gray-300 px-3 py-1.5 text-sm outline-none focus:border-gray-400"
        />
        <span className="text-xs text-gray-400">{filtered.length} icons</span>
      </div>

      <div className="grid grid-cols-[repeat(auto-fill,minmax(84px,1fr))] gap-2">
        {filtered.map((name) => (
          <IconTile key={name} name={name} />
        ))}
      </div>
    </div>
  );
}

const meta: Meta<typeof IconsPage> = {
  title: 'Design System/Icons',
  component: IconsPage,
  parameters: { layout: 'fullscreen' },
};

export default meta;
type Story = StoryObj<typeof IconsPage>;

export const AllIcons: Story = {};
