import { useCallback, useEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import {
  XeniaApi,
  type XeniaConversationSummary,
  type XeniaMessage,
  type XeniaStreamHandle,
  xeniaKeys,
} from '@/shared/api/endpoints/Xenia';

type DisplayMessage = XeniaMessage & { pending?: boolean };

function localMessage(conversationId: string, role: 'User' | 'Assistant', content: string) {
  return {
    id: `local-${Date.now()}-${role}`,
    conversationId,
    role,
    content,
    createdAtUtc: new Date().toISOString(),
    citations: [],
    pending: role === 'Assistant',
  } satisfies DisplayMessage;
}

export function useXeniaChat() {
  const queryClient = useQueryClient();
  const streamRef = useRef<XeniaStreamHandle | undefined>(undefined);
  const [conversationId, setConversationId] = useState<string>();
  const [messages, setMessages] = useState<DisplayMessage[]>([]);
  const [isSending, setIsSending] = useState(false);
  const [sendError, setSendError] = useState<string>();

  const bootstrapQuery = useQuery({
    queryKey: xeniaKeys.bootstrap(),
    queryFn: XeniaApi.getBootstrap,
    retry: false,
  });
  const conversationsQuery = useQuery({
    queryKey: xeniaKeys.conversations(),
    queryFn: XeniaApi.listConversations,
    enabled: bootstrapQuery.data?.enabled === true,
  });

  const activeAgent =
    bootstrapQuery.data?.agents.find(
      (agent) => agent.enabled && agent.agentKey === bootstrapQuery.data.preferences.defaultAgentKey
    ) ?? bootstrapQuery.data?.agents.find((agent) => agent.enabled);

  const closeStream = useCallback(() => {
    streamRef.current?.close();
    streamRef.current = undefined;
  }, []);

  useEffect(() => closeStream, [closeStream]);

  const newChat = useCallback(() => {
    closeStream();
    setConversationId(undefined);
    setMessages([]);
    setSendError(undefined);
    setIsSending(false);
  }, [closeStream]);

  const selectConversation = useCallback(
    async (conversation: XeniaConversationSummary) => {
      closeStream();
      setConversationId(conversation.id);
      setSendError(undefined);
      setIsSending(false);
      const detail = await queryClient.fetchQuery({
        queryKey: xeniaKeys.conversation(conversation.id),
        queryFn: () => XeniaApi.getConversation(conversation.id),
      });
      setMessages(detail.messages);
    },
    [closeStream, queryClient]
  );

  const send = useCallback(
    async (rawContent: string) => {
      const content = rawContent.trim();
      if (!content || isSending || !activeAgent) return;

      closeStream();
      setSendError(undefined);
      setIsSending(true);

      try {
        let currentConversationId = conversationId;
        if (!currentConversationId) {
          const conversation = await XeniaApi.createConversation({
            agentKey: activeAgent.agentKey,
            source: 'mobile',
          });
          currentConversationId = conversation.id;
          setConversationId(conversation.id);
        }

        const clientMessageId = XeniaApi.createClientMessageId();
        const request = { content, clientMessageId };
        const optimisticUser = localMessage(currentConversationId, 'User', content);
        const optimisticAssistant = localMessage(currentConversationId, 'Assistant', '');
        setMessages((current) => [...current, optimisticUser, optimisticAssistant]);

        const finish = (message: XeniaMessage) => {
          setMessages((current) => [
            ...current.filter((item) => item.id !== optimisticAssistant.id),
            message,
          ]);
          setIsSending(false);
          streamRef.current = undefined;
          void queryClient.invalidateQueries({ queryKey: xeniaKeys.conversations() });
          void queryClient.invalidateQueries({
            queryKey: xeniaKeys.conversation(currentConversationId),
          });
        };

        const fallback = async (error: Error) => {
          try {
            finish(await XeniaApi.createMessage(currentConversationId, request));
          } catch (fallbackError) {
            setMessages((current) => current.filter((item) => item.id !== optimisticAssistant.id));
            setIsSending(false);
            setSendError(fallbackError instanceof Error ? fallbackError.message : error.message);
          }
        };

        if (bootstrapQuery.data?.featureFlags.streaming === 'true') {
          streamRef.current = await XeniaApi.streamMessage(currentConversationId, request, {
            onDelta: (delta) => {
              setMessages((current) =>
                current.map((item) =>
                  item.id === optimisticAssistant.id
                    ? { ...item, content: `${item.content}${delta}` }
                    : item
                )
              );
            },
            onCompleted: finish,
            onError: (error) => {
              streamRef.current = undefined;
              void fallback(error);
            },
          });
        } else {
          finish(await XeniaApi.createMessage(currentConversationId, request));
        }
      } catch (error) {
        setIsSending(false);
        setSendError(error instanceof Error ? error.message : 'Xenia could not send the message.');
      }
    },
    [
      activeAgent,
      bootstrapQuery.data?.featureFlags.streaming,
      closeStream,
      conversationId,
      isSending,
      queryClient,
    ]
  );

  return {
    activeAgent,
    bootstrapQuery,
    conversationId,
    conversations: conversationsQuery.data ?? [],
    conversationsQuery,
    isSending,
    messages,
    newChat,
    selectConversation,
    send,
    sendError,
  };
}
