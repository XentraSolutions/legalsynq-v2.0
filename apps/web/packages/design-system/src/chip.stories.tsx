import type { Meta, StoryObj } from '@storybook/nextjs';
import { Chip } from './chip';

const meta = {
  title: 'Design System/Chip',
  component: Chip,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  args: {
    children: 'Chip',
  },
  argTypes: {
    color: {
      control: 'select',
      options: ['default', 'success', 'warning', 'danger', 'info', 'purple', 'teal'],
    },
    variant: {
      control: 'select',
      options: ['solid', 'light', 'soft'],
    },
    size: {
      control: 'select',
      options: ['lg', 'md', 'sm', 'icon'],
    },
  },
} satisfies Meta<typeof Chip>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Solid: Story = {
  args: { variant: 'solid', color: 'default' },
};

export const Light: Story = {
  args: { variant: 'light', color: 'success' },
};

export const Soft: Story = {
  args: { variant: 'soft', color: 'danger' },
};

export const AllColors: Story = {
  render: () => (
    <div className="flex flex-col gap-3">
      {(['solid', 'light', 'soft'] as const).map((variant) => (
        <div key={variant} className="flex flex-wrap items-center gap-2">
          {(['default', 'success', 'warning', 'danger', 'info', 'purple', 'teal'] as const).map(
            (color) => (
              <Chip key={color} variant={variant} color={color}>
                {color}
              </Chip>
            ),
          )}
        </div>
      ))}
    </div>
  ),
};
