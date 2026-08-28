import type { Preview } from '@storybook/nextjs';
import { Title, Description, Primary, Controls } from '@storybook/addon-docs/blocks';
import '../src/app/globals.css';

const preview: Preview = {
  tags: ['autodocs'],
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    backgrounds: {
      default: 'light',
      values: [
        { name: 'light', value: '#ffffff' },
        { name: 'dark', value: '#0a0a0a' },
      ],
    },
    docs: {
      // Default autodocs template also renders a "Stories" section that
      // re-lists every docs-visible story, duplicating Primary right below
      // itself. Most of our components only keep one story visible in docs
      // (a `Default`/args-driven example) with the rest hidden via
      // `docs.disable`, so that section is always a redundant repeat — drop
      // it globally instead of overriding `docs.page` per component.
      page: () => (
        <>
          <Title />
          <Description />
          <Primary />
          <Controls />
        </>
      ),
    },
  },
  globalTypes: {
    theme: {
      description: 'Global theme for components',
      toolbar: {
        title: 'Theme',
        icon: 'mirror',
        items: [
          { value: 'light', title: 'Light' },
          { value: 'dark', title: 'Dark' },
        ],
        dynamicTitle: true,
      },
    },
  },
  initialGlobals: {
    theme: 'light',
  },
  decorators: [
    (Story, context) => {
      const theme = context.globals.theme ?? 'light';
      if (typeof document !== 'undefined') {
        document.documentElement.setAttribute('data-theme', theme);
      }
      return <Story />;
    },
  ],
};

export default preview;
