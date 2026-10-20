using TonTuyau.Application.Library;

namespace TonTuyau.Application.TubeArchivist;

/// <summary>A channel of TubeArchivist's list (2.0), and whether TubeArchivist is subscribed to it (B4).</summary>
public sealed record TaChannel(Channel Channel, bool Subscribed);
