import type { Meta, StoryObj } from '@storybook/nextjs';
import { fn } from 'storybook/test';
import { Input } from './input';

const meta = {
  title: 'Components/Input',
  component: Input,
  parameters: {
    layout: 'centered',
  },
  args: {
    onChange: fn(),
    placeholder: 'Enter text...',
  },
  decorators: [
    (Story) => (
      <div className="w-72">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Input>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Playground — use the controls to explore type/value/disabled combinations. */
export const Interactive: Story = {};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: 'Read only value' },
};

/** Every input type/state at a glance — regression check only, not part of the docs page. */
export const AllVariants: Story = {
  parameters: { docs: { disable: true }, controls: { disable: true } },
  render: () => (
    <div className="flex flex-col gap-3">
      <Input placeholder="Enter text..." />
      <Input defaultValue="LS-2024-00042" />
      <Input disabled defaultValue="Read only value" />
      <Input type="password" defaultValue="secret123" />
      <Input type="number" defaultValue={42} />
    </div>
  ),
};
