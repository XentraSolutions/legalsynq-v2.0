import type { Meta, StoryObj } from '@storybook/nextjs';
import { fn } from 'storybook/test';
import { Download, Plus, Settings, Trash } from 'lucide-react';
import { Button } from './button';

const meta = {
  title: 'Components/Button',
  component: Button,
  parameters: {
    layout: 'centered',
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

export const Loading: Story = {
  args: { variant: 'primary', loading: true },
};

export const Disabled: Story = {
  args: { variant: 'primary', disabled: true },
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

/** Icon composition gallery — regression check only, not part of the docs page. */
export const WithIcons: Story = {
  parameters: { docs: { disable: true }, controls: { disable: true } },
  render: () => (
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
      <Button variant="icon-rounded" aria-label="Settings">
        <Settings className="h-4 w-4" />
      </Button>
      <Button variant="icon-square" aria-label="Add">
        <Plus className="h-4 w-4" />
      </Button>
    </div>
  ),
};

/** Every variant/state at a glance — regression check only, not part of the docs page. */
export const AllVariants: Story = {
  parameters: { docs: { disable: true }, controls: { disable: true } },
  render: () => (
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
      <Button variant="primary" loading>
        Loading
      </Button>
      <Button variant="primary" disabled>
        Disabled
      </Button>
    </div>
  ),
};
