import { xeniaConversationListSchema } from './schemas';

describe('xeniaConversationListSchema', () => {
  it('parses the conversations response envelope returned by the gateway', () => {
    const result = xeniaConversationListSchema.parse({
      conversations: [
        {
          id: '019f8a58-0960-71eb-a4d9-4f9f7223e78d',
          agentKey: 'generic',
          agentVersion: '1.4.0',
          title: 'New Generic Assistant conversation',
          source: 'mobile',
          status: 'Active',
          lastMessageAtUtc: '2026-07-22T15:01:02.670191',
          createdAtUtc: '2026-07-22T15:00:53.216632',
          updatedAtUtc: '2026-07-22T15:01:02.673135',
        },
      ],
    });

    expect(result.conversations).toHaveLength(1);
    expect(result.conversations[0]?.title).toBe('New Generic Assistant conversation');
  });
});
