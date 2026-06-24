declare const process: {
  env: Record<string, string | undefined>;
};

declare module '*.png' {
  const value: number;
  export default value;
}
