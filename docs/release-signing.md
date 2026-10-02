# Release signing

The release workflow signs the published executables (before they are zipped and packed into the
installer) and then the installer itself, `StartupProfiles-Setup-<version>.exe`. The `.sha256` files are
written after signing, so they match the signed downloads; the in-app updater refuses an installer whose
hash does not match.

This page describes how release executables get an Authenticode signature today. Two scripts in
`tools/` and one step in `.github/workflows/release.yml` do the work.

## Current state

- While no certificate is enrolled, releases ship unsigned. The signing step warns and the release
  continues.
- The workflow does not pass `-Require` yet. Once a certificate is enrolled, add `-Require` to the
  signing step so a missing certificate fails the release instead of shipping unsigned files.

## What the release workflow does

On a `v*` tag push, after `dotnet publish` writes the app to `publish/`:

1. **Sign executables** runs `tools/sign-windows-artifacts.ps1` on `publish/StartupProfiles*.exe`
   with the description `Startup Profiles` and the repository URL. It gets two repository secrets as
   environment variables: `WINDOWS_CERT_PFX_BASE64` and `WINDOWS_CERT_PASSWORD`. DLLs are not signed.
2. **Package** zips `publish/` after signing. A zip has no signature of its own, so the signed
   executables inside are what SmartScreen checks once a user extracts them.
3. **Report signing status** runs even when an earlier step failed. It writes each executable's
   `Get-AuthenticodeSignature` status (for example `Valid` or `NotSigned`) to the job summary.

The workflow passes only the PFX secrets. It does not pass `WINDOWS_CERT_THUMBPRINT`, so the
thumbprint path below works only after the workflow is changed to provide it.

## Enrolling a certificate: `tools/setup-windows-signing.ps1`

Run it once, yourself, from a normal terminal. It needs the GitHub CLI `gh`, signed in to this
repository.

### With a PFX file

```powershell
./tools/setup-windows-signing.ps1 -PfxPath 'E:\signing\codesign.pfx'
```

The script:

1. Asks for the PFX password with a hidden prompt.
2. Opens the PFX in memory and prints its subject, issuer, and expiry date.
3. Stops with an error when the PFX has no private key, the certificate has expired, or the
   certificate lists enhanced key usages without Code Signing (`1.3.6.1.5.5.7.3.3`). A TLS
   certificate opens fine but makes signatures Windows rejects, so this is checked here.
4. Warns when the certificate expires within 30 days, and when it is self-signed, because a
   self-signed certificate does not stop SmartScreen warnings.
5. With `-WhatIfOnly`, stops here and uploads nothing.
6. Otherwise writes `WINDOWS_CERT_PFX_BASE64` and `WINDOWS_CERT_PASSWORD` to a short-lived dotenv
   file in the temp folder, runs `gh secret set --env-file` with it, and deletes the file in a
   `finally` block. It does not pipe the password, because Windows PowerShell adds a newline to piped
   input, and it does not use `--body`, which would put the password on a command line.

Keep the PFX and its password in offline storage and a password manager. GitHub secrets cannot be
read back.

### With a hardware token or a cloud signing service

```powershell
./tools/setup-windows-signing.ps1 -Thumbprint 'A1B2C3...'
```

The key cannot be exported, so nothing is uploaded. The script finds the certificate in
`Cert:\CurrentUser\My`, prints its subject and expiry, and tells you the options: set
`WINDOWS_CERT_THUMBPRINT` on a self-hosted runner that has the token, or switch the workflow to the
provider's signing action (Azure Trusted Signing, SSL.com eSigner, DigiCert KeyLocker).

### Getting a certificate

Since June 2023 a publicly trusted code-signing certificate cannot be issued as a plain PFX file; the
key must live on hardware or in a cloud signing service. Such a key uses the thumbprint path above.

## Signing: `tools/sign-windows-artifacts.ps1`

Parameters:

| Parameter | Meaning |
| --- | --- |
| `-Path` | Files to sign. Wildcards are allowed. Required. |
| `-Description` | Text shown in the UAC prompt. Defaults to the file name. |
| `-DescriptionUrl` | URL shown with the signature. Optional. |
| `-TimestampUrl` | RFC 3161 timestamp server. Defaults to `http://timestamp.digicert.com`. |
| `-Require` | Turn "no files matched" and "no certificate configured" into errors. |

What it does:

1. Expands each `-Path` pattern. A pattern that matches nothing is a warning, or an error with
   `-Require`. When nothing at all matches, it warns and returns, or throws with `-Require`.
2. Looks for a certificate, in this order: `WINDOWS_CERT_PFX_BASE64` (with `WINDOWS_CERT_PASSWORD`),
   then `WINDOWS_CERT_THUMBPRINT`. When neither is set, it warns that the files will be published
   unsigned and returns, or throws with `-Require`. This is the only quiet path. Once a certificate is
   configured, any signing failure stops the script.
3. Finds `signtool.exe` on `PATH`, or the newest x64 copy under the Windows 10 SDK `bin` folders. It
   is present on GitHub's `windows-latest` runners.
4. For a PFX secret, writes the PFX to a temp file, imports it into `Cert:\CurrentUser\My`, and signs
   by thumbprint. It does not pass the password to `signtool /p`, which would show it in the process
   list. It stops when the certificate has expired.
5. Signs each file with `signtool sign /sha1 <thumbprint> /fd sha256 /tr <timestamp url> /td sha256`
   plus `/d` and, when given, `/du`. The timestamp keeps signatures valid after the certificate
   expires.
6. Verifies each file with `signtool verify /pa /q`, the Authenticode policy Windows applies when a
   user runs the file. A file that does not verify stops the script.
7. In a `finally` block, removes the imported certificate from the store and deletes the temp PFX.
