using System.Diagnostics.CodeAnalysis;

namespace TonTuyau.Application.Library;

[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "'Short' is the YouTube term.")]
public enum VideoType
{
    Video,
    Short,
    Stream,
}
