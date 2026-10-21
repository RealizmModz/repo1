# Encoded-save research and disposable-profile probe

## Status

Read-only ScsC/BSII v3 inspection is implemented for copied saves. It does not
modify profiles or create playable jobs. Unknown binary versions/types fail
closed. ScsC envelope decoding checks declared size, zlib integrity and AES
padding; the legacy envelope MAC is **not authenticated**. A SHA-256 fingerprint
identifies the input bytes, not their authenticity.

The current field layout was observed locally in ATS **1.61.3.18s**, save-container
version **102**, on a new unmodded disposable profile. No private saves, profile
identifiers, account data or proprietary game fixtures are stored in this repo.
Synthetic fixtures exercise the reader in CI.

## Local experiment — not a supported adapter

A staged copy replaces one generated offer's destination with the destination
of another offer from the **same source company**. Distance/ferry fields come
from that same-origin route template. Cargo, trailer, truck, quantity, pickup
placement, expiry event and every unrelated unit retain their original bytes.

The initial staged probe is Fruits from New Orleans to Lafayette (221 km).
Only target and shortest_distance_km differ. 16,472 other units compare
byte-for-byte equal. Existing offers provide a preliminary receiver-company
family witness; that is **not** proof of the precise depot's compatibility.

This is a candidate for an offer-replacement adapter, not arbitrary insertion,
exact prefab selection, payment editing or GPS waypoint support. Raw BSII
output acceptance has not yet been demonstrated in ATS. The staged research
copy has not been applied to a profile.

Required before enabling dispatch:

1. Run against a verified local disposable profile with ATS closed; reject Cloud.
2. Verify exact build/container/schema and unchanged source hash.
3. Keep an independent verified backup and atomic staged replacement receipt.
4. Confirm ATS loads the candidate, displays the intended custom offer, and
   permits accepting it with the expected cargo/truck/trailer.
5. Complete pickup/delivery, verify payment/history and errors, then test rollback.
6. Add transaction failure tests and preserve the capability gates.

## Format references and notice

This is an original managed reader using format facts from
[liam-dong/SII-Decrypt-cpp](https://github.com/liam-dong/SII-Decrypt-cpp)
(MIT, copyright 2026 Liam Dong). The 32-byte public AES format constant is
not a user credential. The upstream notice is retained in
[THIRD_PARTY_SII_NOTICE.txt](THIRD_PARTY_SII_NOTICE.txt).

The official [g_save_format documentation](https://modding.scssoft.com/wiki/Documentation/Engine/Configuration_variables/g_save_format)
describes plaintext/binary save configuration. This project does not change a
user's global save setting while inspecting a copy.

Do not run a legacy whole-save serializer over version 102. Preserve
unrecognized fields and reject unsupported data instead of dropping it.
