namespace AttentionHub.Client;

public static class MockEventFactory
{
    public static IReadOnlyList<MockAttentionEvent> CreateInitialEvents()
    {
        var now = DateTimeOffset.Now;

        return
        [
            new MockAttentionEvent(
                eventId: "evt_mock_calendar_001",
                eventType: "calendar.busy",
                sourceId: "empresa-a",
                sourceDisplayName: "Empresa A",
                sourceApp: "outlook",
                occurredAt: now.AddMinutes(15),
                priority: "normal",
                summary: "[Empresa A] Busy 09:30-10:00",
                dedupeKey: "mock:empresa-a:calendar:001",
                localStatus: "Visto",
                calendar: new CalendarPayload(
                    StartsAt: now.AddMinutes(15),
                    EndsAt: now.AddMinutes(45),
                    Availability: "busy",
                    MaskedTitle: "[Empresa A] Busy",
                    IsRecurring: false)),
            new MockAttentionEvent(
                eventId: "evt_mock_email_001",
                eventType: "email.direct",
                sourceId: "empresa-a",
                sourceDisplayName: "Empresa A",
                sourceApp: "outlook",
                occurredAt: now.AddMinutes(-4),
                priority: "high",
                summary: "E-mail direto recebido",
                dedupeKey: "mock:empresa-a:email:001",
                localStatus: "Novo",
                email: new EmailPayload(
                    Direction: "inbound",
                    SenderHash: "sha256:mock-sender-a",
                    RecipientMatch: "to",
                    HasAttachments: false)),
            new MockAttentionEvent(
                eventId: "evt_mock_message_001",
                eventType: "message.direct",
                sourceId: "empresa-b",
                sourceDisplayName: "Empresa B",
                sourceApp: "teams",
                occurredAt: now.AddMinutes(-2),
                priority: "normal",
                summary: "Mensagem direta via notificacao Windows",
                dedupeKey: "mock:empresa-b:teams:001",
                localStatus: "Novo",
                message: new MessagePayload(
                    ConversationType: "direct",
                    SenderHash: "sha256:mock-sender-b")),
            new MockAttentionEvent(
                eventId: "evt_mock_mention_001",
                eventType: "message.mention",
                sourceId: "empresa-c",
                sourceDisplayName: "Empresa C",
                sourceApp: "slack",
                occurredAt: now.AddMinutes(-1),
                priority: "urgent",
                summary: "Mencao urgente em canal",
                dedupeKey: "mock:empresa-c:slack:001",
                localStatus: "Novo",
                message: new MessagePayload(
                    ConversationType: "channel",
                    ChannelHash: "sha256:mock-channel-c"))
        ];
    }

    public static IReadOnlyList<AgendaItem> CreateAgendaItems(IEnumerable<MockAttentionEvent> events)
    {
        return events
            .Where(item => item.Calendar is not null)
            .Select(item => new AgendaItem(
                StartsAt: item.Calendar!.StartsAt,
                EndsAt: item.Calendar.EndsAt,
                SourceDisplayName: item.SourceDisplayName,
                MaskedTitle: item.Calendar.MaskedTitle,
                Availability: item.Calendar.Availability))
            .ToList();
    }

    public static IReadOnlyList<RuleSetting> CreateRules()
    {
        return
        [
            new RuleSetting("Enviar apenas metadados", "Ativa", "Nao envia corpo de e-mail, corpo de mensagem ou titulo real de reuniao."),
            new RuleSetting("Alertar e-mail no Para", "Ativa", "Gera notificacao local quando o usuario esta no campo To."),
            new RuleSetting("Alertar mensagens diretas", "Ativa", "Considera notificacoes Windows de Teams, Slack e WhatsApp."),
            new RuleSetting("Mascarar calendario", "Ativa", "Cria blocos como [Empresa] Busy no calendario central."),
            new RuleSetting("Silenciar fora do expediente", "Mock", "Configuracao visual para uma etapa posterior.")
        ];
    }

    public static IReadOnlyList<SourceStatus> CreateSourceStatuses(IEnumerable<MockAttentionEvent> events)
    {
        return events
            .GroupBy(item => new { item.SourceDisplayName, item.SourceApp })
            .Select(group => new SourceStatus(
                SourceDisplayName: group.Key.SourceDisplayName,
                App: group.Key.SourceApp,
                Status: "Mock OK",
                LastEventAt: group.Max(item => item.OccurredAt),
                Notes: "Fonte mockada; nenhuma integracao real habilitada."))
            .OrderBy(item => item.SourceDisplayName)
            .ToList();
    }
}
