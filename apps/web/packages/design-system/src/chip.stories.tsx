import type { Meta, StoryObj } from '@storybook/nextjs';
import { Chip } from './chip';

const COLORS = [
  'default',
  'success',
  'warning',
  'danger',
  'info',
  'purple',
  'teal',
  'gray',
  'red',
  'yellow',
  'green',
  'blue',
  'indigo',
  'pink',
  'brand-orange',
  'brand-slate',
] as const;

const VARIANTS = ['primary', 'secondary', 'tertiary', 'soft'] as const;

const meta = {
  title: 'Components/Chip',
  component: Chip,
  parameters: {
    layout: 'centered',
  },
  args: {
    children: 'Chip',
  },
  argTypes: {
    color: {
      control: 'select',
      options: COLORS,
    },
    variant: {
      control: 'select',
      options: VARIANTS,
    },
    size: {
      control: 'select',
      options: ['lg', 'md', 'sm', 'icon'],
    },
  },
} satisfies Meta<typeof Chip>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Playground — use the controls to explore every variant/color/size combination. */
export const Interactive: Story = {
  args: { variant: 'primary', color: 'default' },
};

/** Every variant x color combination at a glance — regression check only, not part of the docs page. */
export const AllVariants: Story = {
  parameters: { docs: { disable: true }, controls: { disable: true } },
  render: () => (
    <div className="flex flex-col gap-4">
      {VARIANTS.map((variant) => (
        <div key={variant} className="flex flex-wrap items-center gap-2">
          <span className="w-16 shrink-0 text-xs font-medium text-gray-500">{variant}</span>
          {COLORS.map((color) => (
            <Chip key={color} variant={variant} color={color}>
              {color}
            </Chip>
          ))}
        </div>
      ))}
    </div>
  ),
};
