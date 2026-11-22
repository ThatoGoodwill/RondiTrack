#!/usr/bin/env python3
"""Verify the app's selected OpenAPI contract against source and handwritten DTOs.

No external Python packages and no authentication are needed. A renamed API
record property or a stale client DTO fails CI before the UI can silently drift.
This is a subset check; exact live OpenAPI comparison is separately documented.
"""
from pathlib import Path
import json
import re
import sys

mobile = Path(__file__).resolve().parents[1]
root = mobile.parent
contract = json.loads((mobile / "contract/openapi/v1.json").read_text())
models = (mobile / "lib/core/network/data/wire_models.dart").read_text()
schemas = contract["components"]["schemas"]

mapping = {
    "HomeWireResponse": "HomeResponse",
    "HomeStokvelWire": "HomeStokvelResponse",
    "HomeCycleWire": "HomeCycleResponse",
    "ContributionWire": "ContributionResponse",
    "ContributionPageWire": "PageResponseOfContributionResponse",
    "PayoutOrderWire": "PayoutOrderResponse",
    "PayoutEntryWire": "PayoutOrderEntryResponse",
    "TokenWire": "TokenResponse",
    "ActivityWire": "ActivityResponse",
    "ActivityPageWire": "PageResponseOfActivityResponse",
}

errors = []
for dart_type, schema_name in mapping.items():
    factory = re.search(
        r"const factory\s+" + dart_type +
        r"\s*\(\{(.*?)\}\)\s*=\s*_",
        models, re.DOTALL,
    )
    if factory is None:
        errors.append(f"Missing freezed model {dart_type}")
        continue
    fields = set()
    for line in factory.group(1).splitlines():
        m = re.match(
            r"\s*(?:required\s+)?[\w<>?]+\s+(\w+)\s*,?",
            line,
        )
        if m:
            fields.add(m.group(1))
    required = set(schemas[schema_name]["required"])
    if fields != required:
        errors.append(
            f"{dart_type}: model fields {sorted(fields)} != "
            f"contract fields {sorted(required)}"
        )

def cs_record_fields(path, name):
    source = (root / path).read_text()
    match = re.search(r"record\s+" + re.escape(name) + r"\s*\(", source)
    if match is None:
        errors.append(f"Backend record not found: {name}")
        return set()
    depth = 1
    i = match.end()
    content = []
    while i < len(source) and depth:
        char = source[i]
        if char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
            if depth == 0:
                break
        content.append(char)
        i += 1
    clean = re.sub(r"//[^\n]*", "", "".join(content))
    clean = re.sub(r"\[property:.*?\]", "", clean)
    fields = set()
    for piece in clean.split(","):
        result = re.search(r"(\w+)\s*$", piece.strip())
        if result:
            name = result.group(1)
            fields.add(name[0].lower() + name[1:])
    return fields

api_types = {
    "HomeResponse": ("RondiTrack.Api/Contracts/HomeContracts.cs", "HomeResponse"),
    "HomeStokvelResponse": ("RondiTrack.Api/Contracts/HomeContracts.cs", "HomeStokvelResponse"),
    "HomeCycleResponse": ("RondiTrack.Api/Contracts/HomeContracts.cs", "HomeCycleResponse"),
    "PayoutOrderResponse": ("RondiTrack.Api/Contracts/PayoutOrderResponse.cs", "PayoutOrderResponse"),
    "PayoutOrderEntryResponse": ("RondiTrack.Api/Contracts/PayoutOrderResponse.cs", "PayoutOrderEntryResponse"),
}
for schema, (path, record) in api_types.items():
    expected = set(schemas[schema]["properties"])
    actual = cs_record_fields(path, record)
    if expected != actual:
        errors.append(f"{record}: API record {sorted(actual)} != "
                      f"contract {sorted(expected)}")

expected_paths = {
    "/api/v1/auth/login", "/api/v1/me/home",
    "/api/v1/stokvels/{stokvelId}/contributions",
    "/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout/order",
    "/api/v1/activity",
}
if set(contract["paths"]) != expected_paths:
    errors.append("The selected API paths differ from the checked-in contract.")

if errors:
    print("Contract verification FAILED:")
    for problem in errors:
        print(" -", problem)
    sys.exit(1)
print("Selected OpenAPI contract, Freezed DTO fields and C# record names agree.")
