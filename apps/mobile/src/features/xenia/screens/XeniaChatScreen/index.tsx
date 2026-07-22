import { useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, type NavigationProp } from '@react-navigation/native';

import { useXeniaChat } from '@/features/xenia/hooks/useXeniaChat';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { XeniaConversationSummary, XeniaMessage } from '@/shared/api/endpoints/Xenia';
import { cx } from '@/shared/styles';

const ACCENT = '#ee7132';
const SHADOW = '#000000';
const SUGGESTIONS = ['Search liens by client, case, or status', 'Summarize my lien queue'];

export function XeniaChatScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const scrollRef = useRef<ScrollView>(null);
  const [draft, setDraft] = useState('');
  const [menuVisible, setMenuVisible] = useState(false);
  const chat = useXeniaChat();
  const agentName = chat.activeAgent?.name ?? 'SynqLien Agent';

  const submit = (value = draft) => {
    if (!value.trim() || chat.isSending) return;
    setDraft('');
    void chat.send(value);
  };

  if (chat.bootstrapQuery.isLoading) {
    return <CenteredState loading title="Loading Xenia AI" />;
  }

  if (chat.bootstrapQuery.isError || chat.bootstrapQuery.data?.enabled !== true) {
    return (
      <CenteredState
        title="Xenia AI is unavailable"
        description="Your account does not currently have access to the assistant."
        onBack={() => navigation.goBack()}
      />
    );
  }

  return (
    <SafeAreaView edges={['top']} className="flex-1 bg-[#fafafa] dark:bg-[#09090b]">
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        <ChatHeader
          agentName={agentName}
          hasConversation={Boolean(chat.conversationId)}
          onBack={() => navigation.goBack()}
          onMenu={() => setMenuVisible(true)}
          onNewChat={chat.newChat}
        />

        <ScrollView
          ref={scrollRef}
          className="flex-1"
          contentContainerStyle={{ flexGrow: 1, paddingBottom: 12 }}
          keyboardShouldPersistTaps="handled"
          onContentSizeChange={() => scrollRef.current?.scrollToEnd({ animated: true })}
        >
          {chat.messages.length === 0 ? (
            <WelcomeState onSuggestion={submit} />
          ) : (
            <View className="py-2">
              {chat.messages.map((message) => (
                <MessageRow key={message.id} message={message} />
              ))}
            </View>
          )}
          {chat.sendError ? (
            <Text className="px-6 py-2 font-jakarta text-xs text-red-600">{chat.sendError}</Text>
          ) : null}
        </ScrollView>

        <Composer
          disabled={chat.isSending}
          value={draft}
          onChange={setDraft}
          onSubmit={() => submit()}
        />
      </KeyboardAvoidingView>

      <ConversationDrawer
        activeId={chat.conversationId}
        conversations={chat.conversations}
        visible={menuVisible}
        onClose={() => setMenuVisible(false)}
        onNewChat={() => {
          chat.newChat();
          setMenuVisible(false);
        }}
        onSelect={(conversation) => {
          setMenuVisible(false);
          void chat.selectConversation(conversation);
        }}
      />
    </SafeAreaView>
  );
}

function ChatHeader({
  agentName,
  hasConversation,
  onBack,
  onMenu,
  onNewChat,
}: {
  agentName: string;
  hasConversation: boolean;
  onBack: () => void;
  onMenu: () => void;
  onNewChat: () => void;
}) {
  return (
    <View className="flex-row items-center justify-between px-6 py-4">
      <View className="flex-row items-center">
        <HeaderButton accessibilityLabel="Go back" icon="arrow-back" onPress={onBack} />
        {hasConversation ? (
          <View className="ml-4">
            <Text className="font-jakarta-semibold text-base leading-6 text-[#18181b] dark:text-white">
              Xenia AI
            </Text>
            <Text className="font-jakarta text-xs leading-4 text-[#71717a]">{agentName}</Text>
          </View>
        ) : null}
      </View>
      {!hasConversation ? (
        <View className="pointer-events-none absolute left-20 right-20 items-center">
          <Text className="font-jakarta-semibold text-base leading-6 text-[#18181b] dark:text-white">
            Xenia AI
          </Text>
        </View>
      ) : null}
      <View className="flex-row gap-4">
        {hasConversation ? (
          <HeaderButton
            accessibilityLabel="Start new chat"
            icon="create-outline"
            onPress={onNewChat}
          />
        ) : null}
        <HeaderButton accessibilityLabel="Open recent chats" icon="list-outline" onPress={onMenu} />
      </View>
    </View>
  );
}

function HeaderButton({
  accessibilityLabel,
  icon,
  onPress,
}: {
  accessibilityLabel: string;
  icon: keyof typeof Ionicons.glyphMap;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      className="h-10 w-10 items-center justify-center rounded-full bg-white dark:bg-[#18181b]"
      style={{ elevation: 2, shadowColor: SHADOW, shadowOpacity: 0.08, shadowRadius: 4 }}
      onPress={onPress}
    >
      <Ionicons color="#71717a" name={icon} size={20} />
    </Pressable>
  );
}

export function WelcomeState({ onSuggestion }: { onSuggestion: (value: string) => void }) {
  return (
    <View className="flex-1 justify-between">
      <View className="flex-1 items-center justify-center px-6">
        <View className="h-10 w-10 items-center justify-center rounded-full bg-[#efeff0]">
          <Ionicons color="#71717a" name="chatbubble-outline" size={20} />
        </View>
        <Text className="mt-6 font-jakarta-semibold text-base leading-6 text-[#18181b] dark:text-white">
          Ask SynqLien Agent
        </Text>
        <Text className="mt-2 text-center font-jakarta text-xs leading-4 text-[#71717a]">
          Tenant-aware assistant for lien context, case tasks, financial summaries, and reporting.
        </Text>
      </View>
      <ScrollView
        horizontal
        contentContainerStyle={{
          alignItems: 'center',
          gap: 8,
          paddingHorizontal: 16,
          paddingVertical: 12,
        }}
        showsHorizontalScrollIndicator={false}
        style={{ flexGrow: 0, height: 60 }}
        testID="xenia-suggestion-row"
      >
        {SUGGESTIONS.map((suggestion) => (
          <Pressable
            accessibilityLabel={suggestion}
            accessibilityRole="button"
            className="self-center rounded-[24px] border border-[#e4e4e7] bg-[#fafafa] px-[14px] py-2"
            key={suggestion}
            onPress={() => onSuggestion(suggestion)}
          >
            <Text className="font-jakarta text-sm leading-5 text-[#71717a]">{suggestion}</Text>
          </Pressable>
        ))}
      </ScrollView>
    </View>
  );
}

function MessageRow({ message }: { message: XeniaMessage & { pending?: boolean } }) {
  const isUser = message.role.toLowerCase() === 'user';
  if (!isUser && message.role.toLowerCase() !== 'assistant') return null;

  if (isUser) {
    return (
      <View className="items-end px-6 py-4">
        <View className="max-w-[200px] rounded-[20px] bg-[#efeff0] px-4 py-3">
          <Text className="font-jakarta text-sm leading-5 text-[#18181b]">{message.content}</Text>
        </View>
      </View>
    );
  }

  if (message.pending && !message.content) {
    return (
      <View className="flex-row items-center gap-1.5 px-6 py-4">
        <Ionicons color={ACCENT} name="sparkles" size={18} />
        <Text className="font-jakarta text-xs text-[#71717a]">Thinking...</Text>
      </View>
    );
  }

  return (
    <View className="px-6 py-4">
      <Text className="font-jakarta text-sm leading-5 text-[#71717a] dark:text-[#a1a1aa]">
        {message.content}
      </Text>
      {message.citations.length ? (
        <View className="mt-3 gap-2">
          {message.citations.map((citation) => (
            <View className="flex-row items-center" key={citation.id}>
              <Ionicons color={ACCENT} name="document-text-outline" size={14} />
              <Text className="ml-1.5 flex-1 font-jakarta text-xs text-[#71717a]">
                {citation.label}
              </Text>
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
}

function Composer({
  disabled,
  value,
  onChange,
  onSubmit,
}: {
  disabled: boolean;
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
}) {
  return (
    <View className="flex-row items-center gap-4 border-t border-[#e4e4e7] bg-white px-6 pb-6 pt-4 dark:bg-[#18181b]">
      <TextInput
        accessibilityLabel="Ask Xenia"
        className="h-10 flex-1 rounded-full bg-[#efeff0] px-4 py-0 font-jakarta text-sm leading-5 text-[#18181b]"
        editable={!disabled}
        placeholder="Ask Xenia"
        placeholderTextColor="#71717a"
        returnKeyType="send"
        value={value}
        onChangeText={onChange}
      />
      <Pressable
        accessibilityLabel="Send message"
        accessibilityRole="button"
        className={cx(
          'h-9 w-9 items-center justify-center rounded-full bg-[#ee7132]',
          disabled && 'opacity-70'
        )}
        disabled={!value.trim() || disabled}
        onPress={onSubmit}
      >
        {disabled ? (
          <ActivityIndicator color="white" size="small" />
        ) : (
          <Ionicons color="white" name="paper-plane-outline" size={16} />
        )}
      </Pressable>
    </View>
  );
}

function ConversationDrawer({
  activeId,
  conversations,
  visible,
  onClose,
  onNewChat,
  onSelect,
}: {
  activeId?: string;
  conversations: XeniaConversationSummary[];
  visible: boolean;
  onClose: () => void;
  onNewChat: () => void;
  onSelect: (conversation: XeniaConversationSummary) => void;
}) {
  const insets = useSafeAreaInsets();
  const [searching, setSearching] = useState(false);
  const [search, setSearch] = useState('');
  const filtered = useMemo(
    () =>
      conversations.filter((item) =>
        (item.title ?? 'Untitled conversation').toLowerCase().includes(search.trim().toLowerCase())
      ),
    [conversations, search]
  );

  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 flex-row bg-black/25">
        <Pressable accessibilityLabel="Close recent chats" className="w-[18%]" onPress={onClose} />
        <View
          className="flex-1 bg-white px-3 dark:bg-[#18181b]"
          style={{ paddingBottom: Math.max(insets.bottom, 8), paddingTop: insets.top }}
        >
          <View className="flex-row items-start px-3 py-6">
            <View className="h-6 w-6 items-center justify-center rounded-md bg-[#ee7132]">
              <Ionicons color="white" name="sparkles" size={15} />
            </View>
            <View className="ml-2">
              <Text className="font-jakarta-semibold text-base text-[#18181b] dark:text-white">
                Xenia AI
              </Text>
              <Text className="font-jakarta text-xs text-[#262626] dark:text-[#d4d4d8]">
                SynqLien Agent
              </Text>
            </View>
          </View>
          <View className="border-b border-[#e4e4e7] pb-2">
            <DrawerAction icon="create-outline" label="New Chat" onPress={onNewChat} />
            <DrawerAction
              icon="search-outline"
              label="Search"
              onPress={() => setSearching((current) => !current)}
            />
            {searching ? (
              <TextInput
                autoFocus
                className="mx-4 mb-2 rounded-xl bg-[#efeff0] px-3 py-2 font-jakarta text-sm"
                placeholder="Search recent chats"
                value={search}
                onChangeText={setSearch}
              />
            ) : null}
          </View>
          <Text className="px-4 pb-2 pt-6 font-jakarta-medium text-sm text-[#71717a]">Recent</Text>
          <ScrollView>
            {filtered.map((conversation) => (
              <Pressable
                accessibilityRole="button"
                className={cx(
                  'rounded-xl p-4',
                  activeId === conversation.id && 'bg-[#efeff0] dark:bg-[#27272a]'
                )}
                key={conversation.id}
                onPress={() => onSelect(conversation)}
              >
                <Text
                  className="font-jakarta-medium text-sm text-[#18181b] dark:text-white"
                  numberOfLines={1}
                >
                  {conversation.title || 'Untitled conversation'}
                </Text>
              </Pressable>
            ))}
            {!filtered.length ? (
              <Text className="px-4 py-6 font-jakarta text-sm text-[#71717a]">
                No recent conversations.
              </Text>
            ) : null}
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

function DrawerAction({
  icon,
  label,
  onPress,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable accessibilityRole="button" className="flex-row items-center p-4" onPress={onPress}>
      <Ionicons color="#71717a" name={icon} size={20} />
      <Text className="ml-2 font-jakarta-medium text-sm text-[#18181b] dark:text-white">
        {label}
      </Text>
    </Pressable>
  );
}

function CenteredState({
  description,
  loading,
  title,
  onBack,
}: {
  description?: string;
  loading?: boolean;
  title: string;
  onBack?: () => void;
}) {
  return (
    <SafeAreaView className="flex-1 bg-[#fafafa] dark:bg-[#09090b]">
      {onBack ? (
        <View className="px-6 py-4">
          <HeaderButton accessibilityLabel="Go back" icon="arrow-back" onPress={onBack} />
        </View>
      ) : null}
      <View className="flex-1 items-center justify-center px-8">
        {loading ? (
          <ActivityIndicator color={ACCENT} />
        ) : (
          <Ionicons color="#71717a" name="sparkles-outline" size={30} />
        )}
        <Text className="mt-3 text-center font-jakarta-semibold text-base text-[#18181b] dark:text-white">
          {title}
        </Text>
        {description ? (
          <Text className="mt-2 text-center font-jakarta text-sm text-[#71717a]">
            {description}
          </Text>
        ) : null}
      </View>
    </SafeAreaView>
  );
}
