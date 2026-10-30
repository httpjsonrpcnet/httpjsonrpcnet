# Code conventions

This document records agreed conventions for HttpJsonRpc. Follow .NET conventions
and the surrounding code. Apply these rules to new and changed code without
reorganizing unrelated legacy files.

## Type and file organization

- Declare one type per file by default. This applies to classes, interfaces,
  structs, records, enums, and delegates, including test support types.
- Name each file after its type, using PascalCase, and place it in the relevant
  feature directory. Do not collect unrelated types into a shared contexts,
  models, or helpers file.
- Closely related generic and non-generic variants of the same type may share a
  file when that makes the API easier to navigate. This is an exception, not a
  reason to group distinct types that merely belong to the same feature.
- Partial types may span files. Use a descriptive suffix for additional parts,
  while keeping each file focused on that one type.
- Nest a type only when nesting is meaningful to its design, not to avoid the
  single-type-per-file convention. Put reusable test fixtures and support models
  in appropriately named files and namespaces.

## Naming and scope

- Use PascalCase for types and public members, and camelCase for parameters and
  local variables, following .NET naming conventions.
- Keep names, examples, and contracts independent of any consuming application.
- Preserve existing file encoding and line endings when editing files.

## Runtime and documentation attributes

- Keep routing and parameter binding settings on `JsonRpc*` attributes.
- Put API descriptions and schema constraints on `OpenRpc*` attributes. These are
  documentation only and must not influence deserialization or invocation.
- Use OpenRPC and JSON Schema terminology for documentation APIs, including
  `OpenRpcProperty` for JSON object properties, even when backed by CLR fields.
- Derive method and parameter names from the functional contract and property
  names from the serializer contract. Only reusable schema component names may
  be overridden by documentation attributes.
