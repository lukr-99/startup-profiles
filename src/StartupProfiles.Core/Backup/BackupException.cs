namespace StartupProfiles.Core.Backup;

/// <summary>A backup file that cannot be restored, or a restore that failed and was rolled back. The message is for the user.</summary>
public sealed class BackupException : Exception
{
    public BackupException(string message, Exception? inner = null) : base(message, inner) { }
}
