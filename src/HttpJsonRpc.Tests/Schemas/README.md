# Test schema provenance

- `openrpc.json`: official OpenRPC meta-schema release 1.14.9, from https://github.com/open-rpc/meta-schema/releases/download/1.14.9/open-rpc-meta-schema.json. Its accepted document versions include 1.3.0, which this library emits.
- `json-schema.json`: https://meta.json-schema.tools/, downloaded 2026-09-17. Required by the OpenRPC meta-schema and registered locally as a Draft 7 dialect for validation.
- `LICENSE.md`: Apache 2.0 license from open-rpc/meta-schema.

The files are vendored so tests do not require network access. Do not silently replace the validators with permissive stubs.
