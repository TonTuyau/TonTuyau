using TonTuyau.Application.Configuration;

namespace TonTuyau.Application.UnitTests.Configuration;

internal static class ValidOptions
{
    public const string Token = "secret-token-value-123";

    public static TonTuyauOptions Create() => new(
        new TubeArchivistOptions("http://tubearchivist.test:8000", Token),
        new ElasticsearchOptions("http://es.test:9200", "elastic", "es-password"),
        new StorageOptions("/youtube", "/data/art"),
        new SyncOptions("0 1 * * *", "0 1 * * 0", "Europe/Paris"),
        new DatabaseOptions("tontuyau-db", "tontuyau", "tontuyau", "db-password"),
        new AccountOptions("admin", "admin-password", true),
        new YouTubeOptions());
}
