namespace TonTuyau.Application.Store;

/// <summary>TonTuyau's Elasticsearch failed or refused an operation.</summary>
public sealed class SearchStoreException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
