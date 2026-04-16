namespace SynqComm.Domain;

public static class SynqCommPermissions
{
    public const string ProductCode = "SYNQ_COMMS";

    public const string ConversationRead   = "SYNQ_COMMS.conversation:read";
    public const string ConversationCreate = "SYNQ_COMMS.conversation:create";
    public const string ConversationUpdate = "SYNQ_COMMS.conversation:update";

    public const string MessageRead   = "SYNQ_COMMS.message:read";
    public const string MessageCreate = "SYNQ_COMMS.message:create";

    public const string ParticipantRead   = "SYNQ_COMMS.participant:read";
    public const string ParticipantManage = "SYNQ_COMMS.participant:manage";

    public const string AttachmentManage = "SYNQ_COMMS.attachment:manage";
}
