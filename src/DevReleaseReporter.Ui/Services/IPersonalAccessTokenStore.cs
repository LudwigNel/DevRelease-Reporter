namespace DevReleaseReporter.Ui.Services;

public interface IPersonalAccessTokenStore
{
    bool IsSupported { get; }

    string? Load(string profileName);

    void Save(string profileName, string personalAccessToken);

    void Delete(string profileName);
}
