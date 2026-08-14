import type { z } from 'zod';

import type {
  xeniaAgentSchema,
  xeniaBootstrapSchema,
  xeniaCitationSchema,
  xeniaConversationSchema,
  xeniaConversationSummarySchema,
  xeniaMessageSchema,
  xeniaPreferencesSchema,
  xeniaStreamEventSchema,
} from './schemas';

export type XeniaAgent = z.infer<typeof xeniaAgentSchema>;
export type XeniaBootstrap = z.infer<typeof xeniaBootstrapSchema>;
export type XeniaCitation = z.infer<typeof xeniaCitationSchema>;
export type XeniaConversation = z.infer<typeof xeniaConversationSchema>;
export type XeniaConversationSummary = z.infer<typeof xeniaConversationSummarySchema>;
export type XeniaMessage = z.infer<typeof xeniaMessageSchema>;
export type XeniaPreferences = z.infer<typeof xeniaPreferencesSchema>;
export type XeniaStreamEvent = z.infer<typeof xeniaStreamEventSchema>;

export interface CreateXeniaConversationRequest {
  agentKey?: string;
  title?: string;
  source?: string;
  contextJson?: string;
}

export interface UpdateXeniaConversationRequest {
  title?: string;
  archived?: boolean;
}

export interface CreateXeniaMessageRequest {
  content: string;
  contextJson?: string;
  clientMessageId?: string;
}

export interface UpdateXeniaPreferencesRequest {
  defaultAgentKey?: string;
  contextHintsEnabled?: boolean;
  preferencesJson?: string;
}

export interface XeniaStreamCallbacks {
  onDelta: (delta: string) => void;
  onCompleted: (message: XeniaMessage) => void;
  onError: (error: Error) => void;
}

export interface XeniaStreamHandle {
  close: () => void;
}
