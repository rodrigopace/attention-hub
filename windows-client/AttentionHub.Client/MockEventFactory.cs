namespace AttentionHub.Client;

public static class MockEventFactory
{
    public static IReadOnlyList<MockAttentionEvent> CreateInitialEvents()
    {
        var now = DateTimeOffset.Now;

        return
        [
            new MockAttentionEvent(
                EventId: "evt_mock_calendar_001",
                EventType: "calendar.busy",
                SourceId: "empresa-a",
                SourceDisplayName: "Empresa A",
                SourceApp: "outlook",
                OccurredAt: now.AddMinutes(15),
                Priority: "normal",
                Summary: "[Empresa A] Busy 09:30-10:00",
                DedupeKey: "mock:empresa-a:calendar:001",
                Calendar: new CalendarPayload(
                    StartsAt: now.AddMinutes(15),
                    EndsAt: now.AddMinutes(45),
                    Availability: "busy",
                    MaskedTitle: "[Empresa A] Busy",
                    IsRecurring: false)),
            new MockAttentionEvent(
                EventId: "evt_mock_email_001",
                EventType: "email.direct",
                SourceId: "empresa-a",
                SourceDisplayName: "Empresa A",
                SourceApp: "outlook",
                OccurredAt: now.AddMinutes(-4),
                Priority: "high",
                Summary: "E-mail direto recebido",
                DedupeKey: "mock:empresa-a:email:001",
                Email: new EmailPayload(
                    Direction: "inbound",
                    SenderHash: "sha256:mock-sender-a",
                    RecipientMatch: "to",
                    HasAttachments: false)),
            new MockAttentionEvent(
                EventId: "evt_mock_message_001",
                EventType: "message.direct",
                SourceId: "empresa-b",
                SourceDisplayName: "Empresa B",
                SourceApp: "teams",
                OccurredAt: now.AddMinutes(-2),
                Priority: "normal",
                Summary: "Mensagem direta via notificação Windows",
                DedupeKey: "mock:empresa-b:teams:001",
                Message: new MessagePayload(
                    ConversationType: "direct",
                    SenderHash: "sha256:mock-sender-b")),
            new MockAttentionEvent(
                EventId: "evt_mock_mention_001",
                EventType: "message.mention",
                SourceId: "empresa-c",
                SourceDisplayName: "Empresa C",
                SourceApp: "slack",
                OccurredAt: now.AddMinutes(-1),
                Priority: "urgent",
                Summary: "Menção urgente em canal",
                DedupeKey: "mock:empresa-c:slack:001",
                Message: new MessagePayload(
                    ConversationType: "channel",
                    ChannelHash: "sha256:mock-channel-c"))
        ];
    }
}

