import type { Meta, StoryObj } from '@storybook/nextjs';
import { fn } from 'storybook/test';
import { Button } from './button';

const meta = {
  title: 'Design System/Button',
  component: Button,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
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

export const Primary: Story = {
  args: { variant: 'primary' },
};

export const Secondary: Story = {
  args: { variant: 'secondary' },
};

export const Tertiary: Story = {
  args: { variant: 'tertiary' },
};

export const Ghost: Story = {
  args: { variant: 'ghost' },
};

export const Destructive: Story = {
  args: { variant: 'destructive' },
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

export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button variant="primary">Primary</Button>
      <Button variant="secondary">Secondary</Button>
      <Button variant="tertiary">Tertiary</Button>
      <Button variant="ghost">Ghost</Button>
      <Button variant="destructive">Destructive</Button>
      <Button variant="primary" loading>
        Loading
      </Button>
      <Button variant="primary" disabled>
        Disabled
      </Button>
    </div>
  ),
};
