namespace TonTuyau.Application.Streaming;

/// <summary>One part of a video played in parts (2.0, C3): from a keyframe to the next part's.</summary>
/// <param name="Start">Where it starts in the file, in seconds: a keyframe's time.</param>
public sealed record StreamPart(int Index, double Start, double Duration)
{
    public double End => Start + Duration;
}
