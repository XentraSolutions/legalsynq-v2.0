import { Buffer } from 'buffer';
import CryptoJS from 'crypto-js';

(globalThis as typeof globalThis & { Buffer?: typeof Buffer }).Buffer = Buffer;

export const EncryptionService = {
  encrypt(data: unknown, key: string, iv: string): string {
    const parsedKey = CryptoJS.enc.Utf8.parse(key);
    const parsedIv = CryptoJS.enc.Base64.parse(iv);

    if (!parsedKey) return 'invalid key';
    if (!parsedIv) return 'invalid IV';
    if (!data) return 'invalid Data';

    return CryptoJS.AES.encrypt(JSON.stringify(data), parsedKey, {
      iv: parsedIv,
      mode: CryptoJS.mode.CBC,
    }).toString();
  },

  decrypt(data: string, key: string, iv: string): unknown {
    const parsedKey = CryptoJS.enc.Utf8.parse(key);
    const parsedIv = CryptoJS.enc.Base64.parse(iv);

    if (!parsedKey) return 'invalid key';
    if (!parsedIv) return 'invalid IV';
    if (!data) return 'invalid Data';

    try {
      const decryptedBase64 = CryptoJS.AES.decrypt(data, parsedKey, {
        iv: parsedIv,
        mode: CryptoJS.mode.CBC,
      }).toString(CryptoJS.enc.Base64);

      const decoded = Buffer.from(decryptedBase64, 'base64').toString();

      try {
        return JSON.parse(decoded);
      } catch {
        return decoded;
      }
    } catch (error) {
      return error;
    }
  },
};
