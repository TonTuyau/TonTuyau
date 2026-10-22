namespace TonTuyau.Api.Contracts;

/// <summary>A channel's subscription (contract v2.3): which of its tabs to download, each one required.</summary>
public sealed record SubscriptionRequest(bool? Videos, bool? Shorts, bool? Streams);
