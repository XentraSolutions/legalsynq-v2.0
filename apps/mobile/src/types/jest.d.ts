declare function describe(name: string, fn: () => void): void;
declare function it(name: string, fn: () => void | Promise<void>): void;
declare function beforeEach(fn: () => void | Promise<void>): void;
declare function expect(actual: unknown): any;
declare const jest: {
  clearAllMocks: () => void;
  fn: (implementation?: (...args: any[]) => any) => any;
  mock: (moduleName: string, factory?: () => any) => void;
  restoreAllMocks: () => void;
  spyOn: <T extends object, K extends keyof T>(object: T, method: K) => any;
};
