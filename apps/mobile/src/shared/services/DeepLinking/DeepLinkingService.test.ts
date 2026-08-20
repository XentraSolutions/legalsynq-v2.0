import * as Linking from 'expo-linking';

import { DeepLinkingService } from './DeepLinkingService';

jest.mock('expo-linking', () => ({
  addEventListener: jest.fn(),
  createURL: jest.fn(),
  getInitialURL: jest.fn(),
}));

const linking = Linking as unknown as {
  addEventListener: any;
  createURL: any;
  getInitialURL: any;
};

describe('DeepLinkingService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('preserves custom URL creation and initial URL delegation', async () => {
    linking.createURL.mockReturnValue('com.legalsynq.qa://dashboard');
    linking.getInitialURL.mockResolvedValue('https://links.qa.example.test/dashboard');

    expect(DeepLinkingService.createUrl('/dashboard')).toBe('com.legalsynq.qa://dashboard');
    await expect(DeepLinkingService.getInitialUrl()).resolves.toBe(
      'https://links.qa.example.test/dashboard'
    );
  });

  it('forwards runtime URL events and removes the Expo subscription', () => {
    const remove = jest.fn();
    linking.addEventListener.mockReturnValue({ remove });
    const listener = jest.fn();

    const unsubscribe = DeepLinkingService.subscribeToUrls(listener);
    const platformListener = linking.addEventListener.mock.calls[0][1];
    platformListener({ url: 'https://links.qa.example.test/dashboard' });
    unsubscribe();

    expect(linking.addEventListener.mock.calls[0][0]).toBe('url');
    expect(typeof linking.addEventListener.mock.calls[0][1]).toBe('function');
    expect(listener).toHaveBeenCalledWith('https://links.qa.example.test/dashboard');
    expect(remove).toHaveBeenCalledTimes(1);
  });
});
