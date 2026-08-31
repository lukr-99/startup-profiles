namespace StartupProfiles.App.Integration;

/// <summary>Thrown when a registration cannot be applied (e.g. the running instance rejected the request).</summary>
public sealed class RegistrationException : Exception
{
    public RegistrationException() { }

    public RegistrationException(string message) : base(message) { }

    public RegistrationException(string message, Exception innerException) : base(message, innerException) { }
}
