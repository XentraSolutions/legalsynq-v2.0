import type { Meta, StoryObj } from '@storybook/nextjs';
import { Star } from 'lucide-react';
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
    // Figma prep — swap in the real file/node URL once the design is linked.
    design: {
      type: 'figma',
      url: '',
    },
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
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-4">
        <span className="text-xs font-medium text-gray-500">Colors</span>
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
      </section>

      <section className="flex flex-col gap-2">
        <span className="text-xs font-medium text-gray-500">Sizes</span>
        <div className="flex flex-wrap items-center gap-2">
          <Chip size="lg">Large</Chip>
          <Chip size="md">Medium</Chip>
          <Chip size="sm">Small</Chip>
          <Chip size="icon" leftIcon={<Star className="h-3 w-3" />} iconOnly aria-label="Starred" />
        </div>
      </section>

      <section className="flex flex-col gap-2">
        <span className="text-xs font-medium text-gray-500">With icons</span>
        <div className="flex flex-wrap items-center gap-2">
          <Chip leftIcon={<Star className="h-3 w-3" />}>Starred</Chip>
          <Chip rightIcon={<Star className="h-3 w-3" />}>Starred</Chip>
          <Chip variant="soft" color="success" leftIcon={<Star className="h-3 w-3" />}>
            Starred
          </Chip>
        </div>
      </section>
    </div>
  ),
};
