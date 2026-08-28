import { useEffect, useRef, useState } from 'react';
import type { Meta, StoryObj } from '@storybook/nextjs';

/**
 * Mirrors the Figma "Color Tokens" page (Typography / Surface / Border /
 * Button / Sidebar / Tabs Color Tokens tables): for each semantic token,
 * which primitive it resolves to in light mode and in dark mode.
 *
 * Swatches read the primitive CSS variable directly (e.g. `--color-gray-950`)
 * rather than the semantic token, so each column always shows the fixed
 * light/dark value regardless of the viewer's OS color scheme — the same
 * thing the Figma table is documenting.
 */

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

type TokenRow = {
  token: string;
  light: string;
  dark: string;
  usage: string;
};

type TokenGroup = {
  title: string;
  description: string;
  rows: TokenRow[];
};

const GROUPS: TokenGroup[] = [
  {
    title: 'Typography Color Tokens',
    description:
      'Use these tokens for all things typography - they can also be applied to icons when those elements are paired with type.',
    rows: [
      { token: 'text-primary', light: 'gray-950', dark: 'gray-50', usage: 'Bold accent for main titles' },
      { token: 'text-secondary', light: 'gray-700', dark: 'gray-300', usage: 'Balanced hue for paragraphs' },
      { token: 'text-tertiary', light: 'gray-500', dark: 'gray-400', usage: 'Subtle touch for tertiary texts' },
      { token: 'text-invert', light: 'gray-50', dark: 'gray-950', usage: 'Inverted text color for brand backgrounds' },
      { token: 'text-disabled', light: 'gray-400', dark: 'gray-400', usage: 'Represent disabled text' },
      { token: 'text-brand-orange', light: 'brand-orange-500', dark: 'brand-orange-400', usage: 'Orange branded text for accent' },
      { token: 'text-brand-orange-dark', light: 'brand-orange-700', dark: 'brand-orange-600', usage: 'Bolder orange branded text for accent' },
      { token: 'text-brand-slate', light: 'brand-slate-500', dark: 'brand-slate-400', usage: 'Slate branded text for accent' },
      { token: 'text-brand-slate-dark', light: 'brand-slate-700', dark: 'brand-slate-500', usage: 'Bolder slate branded text for accent' },
      { token: 'text-success', light: 'green-500', dark: 'green-500', usage: 'Success text when no success background is used' },
      { token: 'text-success-dark', light: 'green-700', dark: 'green-700', usage: 'Bolder success text when no success background is used' },
      { token: 'text-warning', light: 'yellow-500', dark: 'yellow-500', usage: 'Warning text when no success background is used' },
      { token: 'text-warning-dark', light: 'yellow-700', dark: 'yellow-700', usage: 'Bolder warning text when no success background is used' },
      { token: 'text-error', light: 'red-500', dark: 'red-500', usage: 'Error text when no success background is used' },
      { token: 'text-error-dark', light: 'red-700', dark: 'red-700', usage: 'Bolder error text when no success background is used' },
    ],
  },
  {
    title: 'Surface Color Tokens',
    description: 'Use these tokens for any background or surface elements, think cards or sections.',
    rows: [
      { token: 'surface-primary', light: 'white', dark: 'gray-950', usage: 'Primary background of the application' },
      { token: 'surface-secondary', light: 'gray-50', dark: 'gray-900', usage: 'First level elevated surface - this is the primary token for cards' },
      { token: 'surface-tertiary', light: 'gray-100', dark: 'gray-800', usage: 'Contrasted background color' },
      { token: 'surface-invert', light: 'gray-950', dark: 'gray-50', usage: 'Inverted background for bold accents' },
      { token: 'surface-disabled', light: 'gray-200', dark: 'gray-600', usage: 'Represents disabled state both for cards and buttons' },
      { token: 'surface-brand-orange', light: 'brand-orange-500', dark: 'brand-orange-500', usage: 'Bold orange background accent when brand color is needed' },
      { token: 'surface-brand-orange-dark', light: 'brand-orange-700', dark: 'brand-orange-700', usage: 'Bolder orange background accent when brand color is needed' },
      { token: 'surface-brand-slate', light: 'brand-slate-500', dark: 'brand-orange-500', usage: 'Bold slate background accent when brand color is needed' },
      { token: 'surface-brand-slate-dark', light: 'brand-slate-700', dark: 'brand-orange-700', usage: 'Bolder slate background accent when brand color is needed' },
      { token: 'surface-success', light: 'green-500', dark: 'green-500', usage: 'Background for success elements' },
      { token: 'surface-success-dark', light: 'green-700', dark: 'green-700', usage: 'Bolder background for success elements' },
      { token: 'surface-warning', light: 'yellow-500', dark: 'yellow-500', usage: 'Background for warning elements' },
      { token: 'surface-warning-dark', light: 'yellow-700', dark: 'yellow-700', usage: 'Bolder background for warning elements' },
      { token: 'surface-error', light: 'red-500', dark: 'red-500', usage: 'Background for error elements' },
      { token: 'surface-error-dark', light: 'red-700', dark: 'red-700', usage: 'Bolder background for error elements' },
    ],
  },
  {
    title: 'Border Color Tokens',
    description: 'Use these for borders and stroke, especially useful for form fields.',
    rows: [
      { token: 'border-primary', light: 'gray-200', dark: 'gray-800', usage: 'Primary stroke for form fields or divider lines' },
      { token: 'border-secondary', light: 'gray-300', dark: 'gray-700', usage: 'More contrasted stroke for accessibility' },
      { token: 'border-selected', light: 'gray-950', dark: 'gray-50', usage: 'Stroke representing a selected state' },
      { token: 'border-disabled', light: 'gray-400', dark: 'gray-400', usage: 'Subtle color to indicate non-interactivity, can be used on buttons' },
      { token: 'border-brand-orange', light: 'brand-orange-500', dark: 'brand-orange-500', usage: 'Orange branded border for a pop of color' },
      { token: 'border-brand-orange-dark', light: 'brand-orange-700', dark: 'brand-orange-700', usage: 'Bolder orange branded border for a pop of color' },
      { token: 'border-brand-slate', light: 'brand-slate-500', dark: 'brand-slate-500', usage: 'Slate branded border for a pop of color' },
      { token: 'border-brand-slate-dark', light: 'brand-slate-700', dark: 'brand-slate-700', usage: 'Bolder slate branded border for a pop of color' },
      { token: 'border-success', light: 'green-500', dark: 'green-500', usage: 'Success border' },
      { token: 'border-success-dark', light: 'green-700', dark: 'green-700', usage: 'Bolder success border' },
      { token: 'border-warning', light: 'yellow-500', dark: 'yellow-500', usage: 'Warning border' },
      { token: 'border-warning-dark', light: 'yellow-700', dark: 'yellow-700', usage: 'Bolder warning border' },
      { token: 'border-error', light: 'red-500', dark: 'red-500', usage: 'Error border' },
      { token: 'border-error-dark', light: 'red-700', dark: 'red-700', usage: 'Bolder error border' },
    ],
  },
  {
    title: 'Button Color Tokens',
    description: 'These tokens are used only in the button components.',
    rows: [
      { token: 'button-primary-text', light: 'white', dark: 'white', usage: 'Primary button text color' },
      { token: 'button-primary-bg-default', light: 'brand-orange-500', dark: 'brand-orange-500', usage: 'Primary button background color' },
      { token: 'button-primary-bg-hover', light: 'brand-orange-600', dark: 'brand-orange-600', usage: 'Primary button hover state background color' },
      { token: 'button-border', light: 'gray-200', dark: 'gray-800', usage: 'Button border color' },
      { token: 'button-secondary-bg-hover', light: 'gray-100', dark: 'gray-900', usage: 'Secondary button hover state background color' },
      { token: 'button-tertiary-bg-default', light: 'gray-100', dark: 'gray-900', usage: 'Tertiary button background color' },
      { token: 'button-tertiary-bg-hover', light: 'gray-200', dark: 'gray-800', usage: 'Tertiary button hover state background color' },
      { token: 'button-ghost-bg-hover', light: 'gray-100', dark: 'gray-900', usage: 'Ghost button background color' },
      { token: 'button-destructive-bg-default', light: 'red-600', dark: 'red-600', usage: 'Destructive button background color' },
      { token: 'button-destructive-bg-hover', light: 'red-700', dark: 'red-700', usage: 'Destructive button hover state background color' },
    ],
  },
  {
    title: 'Sidebar Color Tokens',
    description: 'These tokens are used only in the sidebar components.',
    rows: [{ token: 'sidebar-bg', light: 'gray-50', dark: 'gray-900', usage: 'Sidebar background color' }],
  },
  {
    title: 'Tabs Color Tokens',
    description: 'These tokens are used only in the tabs components.',
    rows: [
      { token: 'tab-primary-default-bg', light: 'gray-50', dark: 'gray-900', usage: 'Primary default tab background color' },
      { token: 'tab-primary-active-bg', light: 'brand-orange-500', dark: 'brand-orange-500', usage: 'Primary active tab background color' },
      { token: 'tab-secondary-default-bg', light: 'gray-100', dark: 'gray-900', usage: 'Secondary default tab background color' },
      { token: 'tab-secondary-active-bg', light: 'white', dark: 'gray-800', usage: 'Secondary active tab background color' },
    ],
  },
];

/** e.g. "brand-orange-500" -> "var(--color-brand-orange-500)"; "white" -> "#FFFFFF". */
function refToCssValue(ref: string): string {
  if (ref === 'white') return '#FFFFFF';
  return `var(--color-${ref})`;
}

function RefSwatch({ colorRef }: { colorRef: string }) {
  const swatchRef = useRef<HTMLDivElement>(null);
  const [hex, setHex] = useState('');

  useEffect(() => {
    if (!swatchRef.current) return;
    setHex(rgbToHex(getComputedStyle(swatchRef.current).backgroundColor));
  }, []);

  return (
    <div className="flex items-center gap-2">
      <div
        ref={swatchRef}
        className="h-5 w-5 shrink-0 rounded border border-gray-200"
        style={{ backgroundColor: refToCssValue(colorRef) }}
      />
      <div className="leading-tight">
        <div className="font-mono text-[11px] text-gray-800">{colorRef}</div>
        <div className="font-mono text-[9px] text-gray-400">{hex}</div>
      </div>
    </div>
  );
}

function TokenTable({ group }: { group: TokenGroup }) {
  return (
    <div className="mb-10">
      <div className="mb-1 flex items-baseline gap-2">
        <h3 className="text-sm font-semibold text-gray-900">{group.title}</h3>
      </div>
      <p className="mb-3 text-xs text-gray-500">{group.description}</p>
      <div className="overflow-hidden rounded-lg border border-gray-200">
        <table className="w-full border-collapse text-left text-xs">
          <thead>
            <tr className="bg-gray-50 text-gray-500">
              <th className="px-3 py-2 font-medium">Token</th>
              <th className="px-3 py-2 font-medium">Light Mode</th>
              <th className="px-3 py-2 font-medium">Dark Mode</th>
              <th className="px-3 py-2 font-medium">How to use this?</th>
            </tr>
          </thead>
          <tbody>
            {group.rows.map((row) => (
              <tr key={row.token} className="border-t border-gray-100">
                <td className="px-3 py-2 font-mono text-gray-900">{row.token}</td>
                <td className="px-3 py-2">
                  <RefSwatch colorRef={row.light} />
                </td>
                <td className="px-3 py-2">
                  <RefSwatch colorRef={row.dark} />
                </td>
                <td className="px-3 py-2 text-gray-500">{row.usage}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function ColorTokensPage() {
  return (
    <div className="p-6">
      <h2 className="mb-1 text-lg font-semibold text-gray-900">Color Tokens</h2>
      <p className="mb-6 text-sm text-gray-500">
        Semantic tokens from <code>@legalsynq/design-system/theme.css</code>, mirroring the Figma "Color
        Tokens" spec — which primitive each token resolves to in light vs. dark mode.
      </p>
      {GROUPS.map((group) => (
        <TokenTable key={group.title} group={group} />
      ))}
    </div>
  );
}

const meta: Meta<typeof ColorTokensPage> = {
  title: 'Design System/Color Tokens',
  component: ColorTokensPage,
  parameters: { layout: 'fullscreen' },
};

export default meta;
type Story = StoryObj<typeof ColorTokensPage>;

export const AllTokens: Story = {};
