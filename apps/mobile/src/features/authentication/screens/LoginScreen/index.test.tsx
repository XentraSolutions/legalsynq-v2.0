import { LoginScreen } from './index';

describe('LoginScreen', () => {
  it('exports the screen entrypoint', () => {
    expect(typeof LoginScreen).toBe('function');
  });
});
