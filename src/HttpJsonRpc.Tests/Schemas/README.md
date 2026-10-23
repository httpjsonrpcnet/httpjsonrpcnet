# Validation fixtures

`openrpc.json` is the unmodified OpenRPC 1.4 schema from the official
`open-rpc/spec` v1.4.1 release:

https://raw.githubusercontent.com/open-rpc/spec/v1.4.1/spec/1.4/schema.json

SHA-256: `a8a733cd87ef6ba2e1a38e6500090581b0f52b91051a0bad8386004366b11c56`

`json-schema.json` was downloaded from https://meta.json-schema.tools/ on
2026-09-17 and is retained unchanged. Tests register it explicitly;
validation does not require downloading schemas at test time.

`LICENSE.md` contains the Apache 2.0 license from open-rpc/meta-schema.
Do not replace these validators with permissive stubs.
