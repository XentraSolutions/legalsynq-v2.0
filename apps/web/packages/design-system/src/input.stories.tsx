import type { Meta, StoryObj } from '@storybook/nextjs';
import { fn } from 'storybook/test';
import { Input } from './input';

const meta = {
  title: 'Design System/Input',
  component: Input,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
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

export const Default: Story = {};

export const WithValue: Story = {
  args: { defaultValue: 'LS-2024-00042' },
};

export const Disabled: Story = {
  args: { disabled: true, defaultValue: 'Read only value' },
};

export const Password: Story = {
  args: { type: 'password', defaultValue: 'secret123' },
};

export const Number: Story = {
  args: { type: 'number', defaultValue: 42 },
};
