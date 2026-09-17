# Changelog

## Unreleased (planned 2.7.0)

### Fixed
- Explicit request rejection now prevents RPC execution, including empty HTTP error responses.
- Response envelopes always contain the appropriate result/error member and preserve null results and IDs independently of application serialization settings.
- Correct JSON-RPC error classification and optional strict request validation.
- Framework multipart parsing, preservation of positional body parameters when a query string exists, and remaining-byte stream lengths.
- Host disposal and repeated OpenRPC-enabled start/stop behavior.
- Explicit RPC names are preserved; only inferred trailing Async suffixes are removed.
- Namespaced schema identities, explicit JSON property names, scalar and enum schemas, typed dictionaries, and recursive collections.

### Added
- Unit and HTTP integration tests on .NET Framework 4.8 and .NET 10, including official OpenRPC meta-schema validation.
- Opt-in StrictProtocol, bounded sequential batches, notifications in strict mode, sync/ValueTask methods, and injected request cancellation tokens.
- OnRequestFinished for guaranteed cleanup; library-owned API instance disposal; ListeningAddresses for dynamically bound listeners.
- Safe error-message defaults, CI, transitive dependency auditing and package-content verification.

### Migration
- Existing permissive HTTP behavior remains the default. StrictProtocol is opt-in; notification and case-sensitive behavior change when enabled.
- Exception messages and stack traces are hidden by default. If existing MDware/custom clients depend on approved business-error text, provide a custom error factory or deliberately enable IncludeExceptionMessagesInErrors after reviewing the disclosure implications. The original exception remains available to logging.
- OnCompletedRequest remains success-only. Move unconditional cleanup to OnRequestFinished; do not duplicate it in both callbacks.
- API instances constructed by the library and returned streams are disposed by the library. Container-resolved instances remain container-owned.
- Generated OpenRPC component names include namespaces, and explicit method names containing Async are no longer truncated. Regenerate consumers or provide explicit legacy method aliases where necessary.
- Response envelopes are corrected in both modes. Clients must tolerate absent error/result members as specified by JSON-RPC.
- The library remains netstandard2.0. Consumers using packages.config must install/update the transitive dependencies, including Microsoft.Bcl.AsyncInterfaces and System.ComponentModel.Annotations, and review binding redirects.

### Scope and validation limits
- No MDware production server or actual API proxy was changed or exercised during this library release. Framework 4.8 HTTP tests and representative JSON/property/wrapper contracts provide compatibility evidence, not a production rollout certification.
- Arbitrary custom JSON converters and per-request serializer changes require corresponding OpenRPC type converters; exhaustive contract inference and a DI/hosting redesign are outside this release.
- Static registrations/options remain startup-only configuration. Runtime mutation, automatic rollback of batch side effects, and forced cancellation of application code are not supported.
