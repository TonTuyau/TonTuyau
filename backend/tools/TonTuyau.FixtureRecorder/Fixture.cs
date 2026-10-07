using System.Text.Json.Nodes;

namespace TonTuyau.FixtureRecorder;

/// <summary>One recorded TubeArchivist answer. <see cref="Json"/> for JSON bodies, <see cref="Text"/> otherwise.</summary>
internal sealed record Fixture(
    string Name,
    string Request,
    bool Authenticated,
    int Status,
    string? ContentType,
    JsonNode? Json,
    string? Text);
