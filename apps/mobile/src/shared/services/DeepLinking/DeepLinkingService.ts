import * as Linking from 'expo-linking';

export const DeepLinkingService = {
  createUrl(path: string): string {
    return Linking.createURL(path);
  },

  async getInitialUrl(): Promise<string | null> {
    return Linking.getInitialURL();
  },

  subscribeToUrls(listener: (url: string) => void): () => void {
    const subscription = Linking.addEventListener('url', ({ url }) => listener(url));

    return () => subscription.remove();
  },
};
