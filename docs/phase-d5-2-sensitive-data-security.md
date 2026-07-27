# Sensitive Data Security

Sensitive custody data is protected by design:

- raw serial numbers are not stored in `WeaponAsset`;
- `SerialNumberEncrypted` holds reversible ASP.NET Data Protection ciphertext;
- `SerialNumberHash` stores SHA-256 of the normalized serial for uniqueness/search;
- list/workspace/timeline/audit payloads use masked serials or entity references;
- full serial exposure is behind `SensitiveCustody.ViewSerialNumbers`;
- armory location details are behind `SensitiveCustody.ViewArmoryLocations`;
- no serials in URLs, audit payloads, logs, or frontend workspace state;
- export remains a separate permission and is not enabled as an unbounded endpoint in this slice.

Regression coverage: `Sensitive_audit_does_not_store_raw_serial`, workspace redaction test, and serial masking unit test.

## Data Protection key ring

Protected serials are only recoverable while the application can load the same Data Protection key ring that encrypted them. Configure an absolute durable path:

| Environment | Setting |
|-------------|---------|
| Development | `export DataProtection__KeysPath="$PWD/.local/data-protection-keys"` |
| Production / containers | `export DataProtection__KeysPath="/var/lib/baseera/data-protection-keys"` |

`appsettings.example.json` leaves `DataProtection:KeysPath` empty on purpose. Do not treat a relative repository path as a deployable setting.

### Restricted environments (Production, Staging, and any non-Development/non-Testing host)

Startup **fails** unless `DataProtection:KeysPath` is set to an absolute writable path. The API does **not** fall back to `Path.GetTempPath()` or derive a path from `Attachments:RootPath`.

### Shared persistent volume requirements

`/var/lib/baseera/data-protection-keys` (or the configured absolute path) must be:

- **Persistent** across process restarts and redeployments
- **Shared** by every Baseera API replica that reads/writes protected serials
- **Outside** the ephemeral container filesystem
- **Permission-restricted** to the Baseera process account only
- Included in the **backup/restore** plan for the environment
- **Stable** across upgrades so previously protected values remain decryptable

Example container mount (documentation only; no in-repo deploy manifests today):

```yaml
# docker-compose / Kubernetes volumeMount example
volumes:
  - type: bind
    source: /var/lib/baseera/data-protection-keys
    target: /var/lib/baseera/data-protection-keys
environment:
  DataProtection__KeysPath: /var/lib/baseera/data-protection-keys
```

Never commit key XML, key material, or secrets into the repository. Logs record the configured path only.
