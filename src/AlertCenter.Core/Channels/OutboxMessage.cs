using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Channels;

/// <summary>
/// The rendered message stored on the outbox entry at enqueue time (RF-005-D),
/// so dispatch needs no cross-module reads. Credentials are never carried here
/// (NFR-4) — only the resolved recipient/content.
/// </summary>
public sealed record OutboxMessage(Channel Channel, string Recipient, string Subject, string Body);
