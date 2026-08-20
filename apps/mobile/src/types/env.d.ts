declare const process: {
  env: {
    EXPO_PUBLIC_API_URL?: string;
    EXPO_PUBLIC_APP_ENV?: 'development' | 'qa' | 'production';
    EXPO_PUBLIC_DEEP_LINK_HOST?: string;
    [key: string]: string | undefined;
  };
};

declare module '*.png' {
  const value: number;
  export default value;
}
