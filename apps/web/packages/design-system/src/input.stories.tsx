import type { Meta, StoryObj } from '@storybook/nextjs';
import { fn } from 'storybook/test';
import { Input } from './input';

const meta = {
  title: 'Components/Input',
  component: Input,
  parameters: {
    layout: 'centered',
    // Figma prep — swap in the real file/node URL once the design is linked.
    design: {
      type: 'figma',
      url: '',
    },
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

/** Every input type/state at a glance, plus forced :focus — regression check only, not part of the docs page. Input has no :hover style to force. */
export const AllVariants: Story = {
  parameters: {
    docs: { disable: true },
    controls: { disable: true },
    pseudo: {
      focus: '.pseudo-focus',
    },
  },
  render: () => (
    <div className="flex flex-col gap-6">
      <section className="flex flex-col gap-2">
        <span className="text-xs font-medium text-gray-500">Types</span>
        <div className="flex flex-col gap-3">
          <Input placeholder="Enter text..." />
          <Input defaultValue="LS-2024-00042" />
          <Input type="password" defaultValue="secret123" />
          <Input type="number" defaultValue={42} />
        </div>
      </section>

      <section className="flex flex-col gap-2">
        <span className="text-xs font-medium text-gray-500">States</span>
        <div className="flex flex-col gap-3">
          <Input disabled defaultValue="Read only value" />
          <Input className="pseudo-focus" placeholder="Forced :focus" />
        </div>
      </section>
    </div>
  ),
};
