import { useEffect, useRef, useState } from 'react';
import type { Meta, StoryObj } from '@storybook/nextjs';

/** rgb(r, g, b) -> #RRGGBB, so the swatch shows the actual resolved token value. */
function rgbToHex(rgb: string): string {
  const match = rgb.match(/\d+/g);
  if (!match) return rgb;
  return (
    '#' +
    match
      .slice(0, 3)
      .map((n) => Number(n).toString(16).padStart(2, '0'))
      .join('')
      .toUpperCase()
  );
}

/**
 * Tailwind's content scanner only generates a utility for class names it can
 * see literally as a string in scanned source — it can't follow the
 * `bg-${name}-${shade}` template literal below. This unused string keeps
 * every shade's utility generated so the dynamic classes actually resolve.
 * Generated from the SCALES/SHADES data below — regenerate if either changes.
 */
const _TAILWIND_SAFELIST =
  'bg-gray-50 bg-gray-100 bg-gray-200 bg-gray-300 bg-gray-400 bg-gray-500 bg-gray-600 bg-gray-700 bg-gray-800 bg-gray-900 bg-gray-950 bg-brand-orange-50 bg-brand-orange-100 bg-brand-orange-200 bg-brand-orange-300 bg-brand-orange-400 bg-brand-orange-500 bg-brand-orange-600 bg-brand-orange-700 bg-brand-orange-800 bg-brand-orange-900 bg-brand-slate-50 bg-brand-slate-100 bg-brand-slate-200 bg-brand-slate-300 bg-brand-slate-400 bg-brand-slate-500 bg-brand-slate-600 bg-brand-slate-700 bg-brand-slate-800 bg-brand-slate-900 bg-red-50 bg-red-100 bg-red-200 bg-red-300 bg-red-400 bg-red-500 bg-red-600 bg-red-700 bg-red-800 bg-red-900 bg-red-950 bg-yellow-50 bg-yellow-100 bg-yellow-200 bg-yellow-300 bg-yellow-400 bg-yellow-500 bg-yellow-600 bg-yellow-700 bg-yellow-800 bg-yellow-900 bg-yellow-950 bg-green-50 bg-green-100 bg-green-200 bg-green-300 bg-green-400 bg-green-500 bg-green-600 bg-green-700 bg-green-800 bg-green-900 bg-green-950 bg-blue-50 bg-blue-100 bg-blue-200 bg-blue-300 bg-blue-400 bg-blue-500 bg-blue-600 bg-blue-700 bg-blue-800 bg-blue-900 bg-blue-950 bg-indigo-50 bg-indigo-100 bg-indigo-200 bg-indigo-300 bg-indigo-400 bg-indigo-500 bg-indigo-600 bg-indigo-700 bg-indigo-800 bg-indigo-900 bg-indigo-950 bg-pink-50 bg-pink-100 bg-pink-200 bg-pink-300 bg-pink-400 bg-pink-500 bg-pink-600 bg-pink-700 bg-pink-800 bg-pink-900 bg-pink-950 bg-teal-50 bg-teal-100 bg-teal-200 bg-teal-300 bg-teal-400 bg-teal-500 bg-teal-600 bg-teal-700 bg-teal-800 bg-teal-900 bg-teal-950 bg-purple-50 bg-purple-100 bg-purple-200 bg-purple-300 bg-purple-400 bg-purple-500 bg-purple-600 bg-purple-700 bg-purple-800 bg-purple-900';

const SHADES = ["50", "100", "200", "300", "400", "500", "600", "700", "800", "900", "950"];

const SCALES: { name: string; description: string; upTo950: boolean }[] = [
  { name: "gray", description: "Neutral scale used for text, borders, and surfaces.", upTo950: true },
  { name: "brand-orange", description: "Static LegalSynq brand orange. Not tenant-dynamic — use --color-primary for brand-aware UI.", upTo950: false },
  { name: "brand-slate", description: "Blue-gray neutral, distinct from gray.", upTo950: false },
  { name: "red", description: "Danger / destructive.", upTo950: true },
  { name: "yellow", description: "Warning.", upTo950: true },
  { name: "green", description: "Success.", upTo950: true },
  { name: "blue", description: "Info.", upTo950: true },
  { name: "indigo", description: "", upTo950: false },
  { name: "pink", description: "", upTo950: false },
  { name: "teal", description: "", upTo950: false },
  { name: "purple", description: "", upTo950: false },
];

/** One shade: color swatch plus its resolved hex and the utility class name to use it with. */
function Swatch({ name, shade }: { name: string; shade: string }) {
  const swatchRef = useRef<HTMLDivElement>(null);
  const [hex, setHex] = useState('');
  const token = `${name}-${shade}`;

  useEffect(() => {
    if (!swatchRef.current) return;
    setHex(rgbToHex(getComputedStyle(swatchRef.current).backgroundColor));
  }, []);

  return (
    <div className="flex-1">
      <div ref={swatchRef} className={`h-16 bg-${name}-${shade}`} />
      <div className="px-1.5 py-1 text-center">
        <div className="text-[10px] font-medium text-gray-700">{shade}</div>
        <div className="font-mono text-[9px] text-gray-500">{hex}</div>
        <div className="font-mono text-[9px] text-gray-400">bg-{token}</div>
      </div>
    </div>
  );
}

function ColorScale({ name, description, upTo950 }: { name: string; description: string; upTo950: boolean }) {
  const shades = upTo950 ? SHADES : SHADES.slice(0, -1);
  return (
    <div className="mb-8">
      <div className="mb-2 flex items-baseline gap-2">
        <h3 className="text-sm font-semibold text-gray-900">{name}</h3>
        {description && <span className="text-xs text-gray-500">{description}</span>}
      </div>
      <div className="flex overflow-hidden rounded-lg border border-gray-200">
        {shades.map((shade) => (
          <Swatch key={shade} name={name} shade={shade} />
        ))}
      </div>
    </div>
  );
}

function ColorsPage() {
  return (
    <div className="p-6">
      {/* Keeps _TAILWIND_SAFELIST referenced so the class-name strings above stay live for the scanner. */}
      <div className="hidden" aria-hidden="true">
        {_TAILWIND_SAFELIST}
      </div>
      <h2 className="mb-1 text-lg font-semibold text-gray-900">Design system colors</h2>
      <p className="mb-6 text-sm text-gray-500">
        LegalSynq V3 palette, wired into Tailwind via <code>@legalsynq/design-system/theme.css</code>. Every
        shade below is a real utility (e.g. <code>bg-teal-400</code>, <code>text-brand-orange-600</code>).
      </p>
      {SCALES.map((scale) => (
        <ColorScale key={scale.name} {...scale} />
      ))}
    </div>
  );
}

const meta: Meta<typeof ColorsPage> = {
  title: 'Design System/Colors',
  component: ColorsPage,
  parameters: { layout: 'fullscreen' },
};

export default meta;
type Story = StoryObj<typeof ColorsPage>;

export const AllColors: Story = {};
