"""Checks a supporter code offline, for Python apps. Needs the `cryptography` package.
Copy this file into the app with the product's public key. Nothing is sent anywhere.

    status, license = verify(code, PUBLIC_KEY_PEM, withdrawn={"withdrawn-id"})
    # status: "valid", "empty", "malformed", "bad-signature", "withdrawn" or "unverifiable"

A code is "<payload>.<signature>" in base64url: JSON {"id","name","issued"} signed with ECDSA P-256 /
SHA-256, signature as r||s (64 bytes).
"""

import base64
import json


def _b64url(text):
    return base64.urlsafe_b64decode(text + "=" * (-len(text) % 4))


def verify(code, public_key_pem, withdrawn=()):
    code = "".join((code or "").split())
    if not code:
        return "empty", None
    parts = code.split(".")
    if len(parts) != 2:
        return "malformed", None
    try:
        payload, signature = _b64url(parts[0]), _b64url(parts[1])
    except ValueError:
        return "malformed", None
    try:
        from cryptography.exceptions import InvalidSignature
        from cryptography.hazmat.primitives import hashes, serialization
        from cryptography.hazmat.primitives.asymmetric import ec
        from cryptography.hazmat.primitives.asymmetric.utils import encode_dss_signature
    except ImportError:
        return "unverifiable", None
    if len(signature) != 64:
        return "bad-signature", None
    try:
        key = serialization.load_pem_public_key(public_key_pem.encode("ascii"))
        der = encode_dss_signature(int.from_bytes(signature[:32], "big"), int.from_bytes(signature[32:], "big"))
        key.verify(der, payload, ec.ECDSA(hashes.SHA256()))
    except InvalidSignature:
        return "bad-signature", None
    except ValueError:
        return "unverifiable", None
    try:
        data = json.loads(payload)
    except ValueError:
        return "malformed", None
    lic_id = str(data.get("id") or "").strip() if isinstance(data, dict) else ""
    if not lic_id:
        return "malformed", None
    if lic_id.lower() in {str(w).strip().lower() for w in withdrawn}:
        return "withdrawn", None
    return "valid", {"id": lic_id, "name": str(data.get("name") or ""), "issued": str(data.get("issued") or "")}
