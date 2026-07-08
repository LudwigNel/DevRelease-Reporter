using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace DevReleaseReporter.Ui.Services;

public sealed class PersonalAccessTokenStore : IPersonalAccessTokenStore
{
    private const string CredentialTargetPrefix = "DevReleaseReporter:AzureDevOpsPat:";
    private const int GenericCredentialType = 1;
    private const int PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public bool IsSupported => OperatingSystem.IsWindows();

    public string? Load(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        EnsureSupported();

        if (!CredRead(BuildTargetName(profileName), GenericCredentialType, 0, out var credentialPointer))
        {
            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode == ErrorNotFound)
            {
                return null;
            }

            throw CreateFailure("load", errorCode);
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var secretBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, secretBytes, 0, secretBytes.Length);
            return Encoding.UTF8.GetString(secretBytes);
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Save(string profileName, string personalAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(personalAccessToken);

        EnsureSupported();

        var normalizedProfileName = profileName.Trim();
        var normalizedToken = personalAccessToken.Trim();
        var secretBytes = Encoding.UTF8.GetBytes(normalizedToken);
        var secretPointer = Marshal.AllocHGlobal(secretBytes.Length);

        try
        {
            Marshal.Copy(secretBytes, 0, secretPointer, secretBytes.Length);

            var credential = new Credential
            {
                Type = GenericCredentialType,
                TargetName = BuildTargetName(normalizedProfileName),
                CredentialBlobSize = secretBytes.Length,
                CredentialBlob = secretPointer,
                Persist = PersistLocalMachine,
                UserName = normalizedProfileName,
            };

            if (!CredWrite(ref credential, 0))
            {
                throw CreateFailure("save", Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            ZeroAndFree(secretPointer, secretBytes.Length);
        }
    }

    public void Delete(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        EnsureSupported();

        if (CredDelete(BuildTargetName(profileName), GenericCredentialType, 0))
        {
            return;
        }

        var errorCode = Marshal.GetLastWin32Error();
        if (errorCode != ErrorNotFound)
        {
            throw CreateFailure("delete", errorCode);
        }
    }

    private static string BuildTargetName(string profileName) =>
        CredentialTargetPrefix + profileName.Trim();

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Secure PAT storage is currently only available on Windows.");
        }
    }

    private static InvalidOperationException CreateFailure(string operation, int errorCode) =>
        new($"Unable to {operation} the PAT in Windows Credential Manager: {new Win32Exception(errorCode).Message}");

    private static void ZeroAndFree(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero)
        {
            return;
        }

        var zeroBytes = new byte[length];
        Marshal.Copy(zeroBytes, 0, pointer, length);
        CryptographicOperations.ZeroMemory(zeroBytes);
        Marshal.FreeHGlobal(pointer);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        int type,
        int reservedFlag,
        out IntPtr credentialPointer);

    [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(
        [In] ref Credential userCredential,
        int flags);

    [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(
        string target,
        int type,
        int flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = false)]
    private static extern void CredFree(IntPtr credentialPointer);
}
