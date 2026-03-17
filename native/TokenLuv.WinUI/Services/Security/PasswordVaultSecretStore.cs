using Windows.Security.Credentials;

namespace TokenLuv.WinUI.Services.Security;

public sealed class PasswordVaultSecretStore : ISecretStore
{
    private const string ResourceName = "TokenLuv";
    private readonly PasswordVault _vault = new();

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PasswordCredential? existing = TryRetrieve(key);
        if (existing is not null)
        {
            _vault.Remove(existing);
        }

        _vault.Add(new PasswordCredential(ResourceName, key, secret));
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PasswordCredential? credential = TryRetrieve(key);
        if (credential is null)
        {
            return Task.FromResult<string?>(null);
        }

        credential.RetrievePassword();
        return Task.FromResult<string?>(credential.Password);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PasswordCredential? credential = TryRetrieve(key);
        if (credential is not null)
        {
            _vault.Remove(credential);
        }

        return Task.CompletedTask;
    }

    private PasswordCredential? TryRetrieve(string key)
    {
        try
        {
            return _vault.Retrieve(ResourceName, key);
        }
        catch
        {
            return null;
        }
    }
}
