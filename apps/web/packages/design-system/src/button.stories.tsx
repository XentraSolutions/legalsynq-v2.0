import type { Meta, StoryObj } from '@storybook/nextjs';
import { fn } from 'storybook/test';
import { Download, Plus, Settings, Trash } from 'lucide-react';
import { Button } from './button';

const meta = {
  title: 'Components/Button',
  component: Button,
  parameters: {
    layout: 'centered',
    // Figma prep — swap in the real file/node URL once the design is linked.
    design: {
      type: 'figma',
      url: '',
    },
  },
  args: {
    onClick: fn(),
    children: 'Button',
  },
  argTypes: {
    variant: {
      control: 'select',
      options: [
        'primary',
        'secondary',
        'tertiary',
        'ghost',
        'destructive',
        'icon-rounded',
        'icon-square',
      ],
    },
  },
} satisfies Meta<typeof Button>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Playground — use the controls to explore every variant/state combination. */
export const Interactive: Story = {
  args: { variant: 'primary' },
};

export const AsLink: Story = {
  name: 'asChild (rendered as <a>)',
  parameters: {
    controls: { disable: true },
    docs: {
      description: {
        story:
          'For a read-only trigger that needs to actually be a link (or another primitive\'s element, e.g. a Radix `DropdownMenuTrigger`) rather than a `<button>`. The Button styles are applied to the child via Radix `Slot`; the child owns its own element and behavior.',
      },
    },
  },
  render: () => (
    <Button asChild variant="secondary">
      <a href="#case-detail">View case</a>
    </Button>
  ),
};

/** Every variant/state at a glance, plus forced :hover / :focus-visible / :active — regression check only, not part of the docs page. */
export const AllVariants: Story = {
  parameters: {
    docs: { disable: true },
    controls: { disable: true },
    pseudo: {
      hover: '.pseudo-hover',
      focusVisible: '.pseudo-focus-visible',
      active: '.pseudo-active',
    },
  },
  render: () => (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-2">
        <span className="text-xs font-medium text-gray-500">Variants</span>
        <div className="flex flex-wrap items-center gap-3">
          <Button variant="primary">Primary</Button>
          <Button variant="secondary">Secondary</Button>
          <Button variant="tertiary">Tertiary</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="destructive">Destructive</Button>
          <Button variant="icon-rounded" aria-label="Settings">
            <Settings className="h-4 w-4" />
          </Button>
          <Button variant="icon-square" aria-label="Settings">
            <Settings className="h-4 w-4" />
          </Button>
        </div>
      </section>

      <section className="flex flex-col gap-2">
        <span className="text-xs font-medium text-gray-500">States</span>
        <div className="flex flex-wrap items-center gap-3">
          <Button variant="primary" loading>
            Loading
          </Button>
          <Button variant="primary" disabled>
            Disabled
          </Button>
        </div>
      </section>

      <section className="flex flex-col gap-2">
        <span className="text-xs font-medium text-gray-500">With icons</span>
        <div className="flex flex-wrap items-center gap-3">
          <Button variant="primary" leftIcon={<Plus className="h-4 w-4" />}>
            Add Company
          </Button>
          <Button variant="secondary" rightIcon={<Download className="h-4 w-4" />}>
            Export
          </Button>
          <Button
            variant="tertiary"
            leftIcon={<Settings className="h-4 w-4" />}
            rightIcon={<Download className="h-4 w-4" />}
          >
            Both icons
          </Button>
          <Button variant="destructive" leftIcon={<Trash className="h-4 w-4" />}>
            Delete
          </Button>
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <span className="text-xs font-medium text-gray-500">Interaction states</span>
        <div className="flex flex-wrap items-center gap-6">
          {(['primary', 'secondary', 'tertiary', 'ghost', 'destructive'] as const).map((variant) => (
            <div key={variant} className="flex flex-col gap-2">
              <span className="text-xs font-medium text-gray-500">{variant}</span>
              <div className="flex items-center gap-3">
                <div className="flex flex-col items-center gap-1.5">
                  <span className="text-[10px] text-gray-400">Default</span>
                  <Button variant={variant}>Button</Button>
                </div>
                <div className="flex flex-col items-center gap-1.5">
                  <span className="text-[10px] text-gray-400">Hover</span>
                  <Button className="pseudo-hover" variant={variant}>
                    Button
                  </Button>
                </div>
                <div className="flex flex-col items-center gap-1.5">
                  <span className="text-[10px] text-gray-400">Focus-visible</span>
                  <Button className="pseudo-focus-visible" variant={variant}>
                    Button
                  </Button>
                </div>
                <div className="flex flex-col items-center gap-1.5">
                  <span className="text-[10px] text-gray-400">Active</span>
                  <Button className="pseudo-active" variant={variant}>
                    Button
                  </Button>
                </div>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  ),
};
