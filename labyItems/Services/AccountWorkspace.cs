using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace labyItems.Services;

/// <summary>One authenticated workspace in memory; other accounts remain encrypted on disk.</summary>
public sealed class AccountWorkspace(
    LabyrintheSession session, string vaultDirectory, string workingDirectory,
    string publicDatabase, Func<string, Task<byte[]>> getKey)
{
    private string? _account;
    private string? _directory;
    private byte[]? _key;
    private bool _guest;
    private long _sessionVersion;
    private readonly object _sync = new();
    public static string AccountKey(string email)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
    public string PublicDatabase => publicDatabase;
    public string RequireDirectory(string? account = null)
    {
        if (!session.IsAuthenticated || session.Version != _sessionVersion || _directory == null || _account != Identity()
            || (account != null && !string.Equals(account.Trim(), session.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Sign in to this account to access its local data.");
        return _directory;
    }
    public string RequireDatabase() => Path.Combine(RequireDirectory(), "laby.db");
    public string RequireMemberDatabase()
    {
        RequireDirectory();
        if (_guest) throw new InvalidOperationException("Sign in with a member account to access points apps.");
        return RequireDatabase();
    }
    public string RequireMemberKey()
    {
        RequireMemberDatabase();
        return AccountKey(_account!);
    }
    private string Identity() => session.IsFallback ? "guest" : session.Email.Trim().ToLowerInvariant();

    public async Task OpenAsync()
    {
        if (!session.IsAuthenticated) throw new InvalidOperationException("Sign in first.");
        if (_directory != null) throw new InvalidOperationException("Close the previous account before switching.");
        var identity = Identity();
        var epoch = session.Version;
        var guest = session.IsFallback;
        var key = guest ? RandomNumberGenerator.GetBytes(32) : await getKey(AccountKey(identity));
        var destination = Path.Combine(workingDirectory, guest ? Guid.NewGuid().ToString("N") : AccountKey(identity));
        Directory.CreateDirectory(destination);
        try
        {
            var archive = Path.Combine(vaultDirectory, AccountKey(identity) + ".vault");
            if (!guest && File.Exists(archive))
                Restore(await File.ReadAllBytesAsync(archive), key, destination);
            else
                File.Copy(publicDatabase, Path.Combine(destination, "laby.db"));
            session.RequireVersion(epoch);
            _account = identity;
            _guest = guest;
            _key = key;
            _directory = destination;
            _sessionVersion = epoch;
            Checkpoint();
        }
        catch
        {
            _directory = null;
            _account = null;
            _key = null;
            CryptographicOperations.ZeroMemory(key);
            Directory.Delete(destination, recursive: true);
            throw;
        }
    }

    public void Checkpoint()
    {
        lock (_sync)
        {
            RequireDirectory();
            SealCurrent();
        }
    }
    private void SealCurrent()
    {
        if (_guest || _directory == null) return;
        Directory.CreateDirectory(vaultDirectory);
        var path = Path.Combine(vaultDirectory, AccountKey(_account!) + ".vault");
        var bytes = SealDirectory(_directory, _key!);
        File.WriteAllBytes(path + ".tmp", bytes);
        File.Move(path + ".tmp", path, overwrite: true);
    }
    public IDisposable BeginWrite()
    {
        Monitor.Enter(_sync);
        try { RequireDirectory(); return new WriteLease(this); }
        catch { Monitor.Exit(_sync); throw; }
    }
    public IDisposable BeginRead()
    {
        Monitor.Enter(_sync);
        try { RequireDirectory(); return new ReadLease(this); }
        catch { Monitor.Exit(_sync); throw; }
    }
    private sealed class ReadLease(AccountWorkspace owner) : IDisposable
    {
        public void Dispose() => Monitor.Exit(owner._sync);
    }

    private sealed class WriteLease(AccountWorkspace owner) : IDisposable
    {
        public void Dispose()
        {
            try { owner.SealCurrent(); }
            finally { Monitor.Exit(owner._sync); }
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            if (_directory == null) return;
            // Fail closed if checkpointing fails; never discard the only copy of a write.
            SealCurrent();
            var directory = _directory;
            _directory = null;
            _account = null;
            if (_key != null) CryptographicOperations.ZeroMemory(_key);
            _key = null;
            Directory.Delete(directory, recursive: true);
        }
    }

    public static byte[] SealDirectory(string directory, byte[] key)
    {
        using var buffer = new MemoryStream();
        ZipFile.CreateFromDirectory(directory, buffer, CompressionLevel.Fastest, includeBaseDirectory: false);
        var plain = buffer.ToArray();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var encrypted = new byte[plain.Length];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, encrypted, tag);
        CryptographicOperations.ZeroMemory(plain);
        if (buffer.TryGetBuffer(out var data)) CryptographicOperations.ZeroMemory(data.AsSpan());
        return nonce.Concat(tag).Concat(encrypted).ToArray();
    }

    private static void Restore(byte[] bytes, byte[] key, string directory)
    {
        if (bytes.Length < 28) throw new CryptographicException("Invalid account archive.");
        var plain = new byte[bytes.Length - 28];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain);
            using var buffer = new MemoryStream(plain);
            ZipFile.ExtractToDirectory(buffer, directory);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
