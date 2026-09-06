import json
import os
import urllib.request
import urllib.error

# Read CLERK_SECRET_KEY from backend/.env
env_vars = {}
with open("/mnt/shared/Code/UIU-Sim/backend/.env") as f:
    for line in f:
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            k, v = line.split("=", 1)
            env_vars[k.strip()] = v.strip()

clerk_secret = env_vars["CLERK_SECRET_KEY"]
session_id = "sess_3Ixq7MGCZf6MaObaK3BrNqwRLMV"

print("1. Minting initial token from Clerk API...")
req = urllib.request.Request(
    f"https://api.clerk.com/v1/sessions/{session_id}/tokens",
    data=b'{"expires_in_seconds":60}',
    headers={
        "Authorization": f"Bearer {clerk_secret}",
        "Content-Type": "application/json",
        "User-Agent": "UIU-Sim-Backend/1.0"
    }
)
with urllib.request.urlopen(req) as resp:
    data = json.loads(resp.read().decode())
    initial_jwt = data["jwt"]
    print("   Initial JWT minted successfully.")

print("2. Browser posting token to dev bridge...")
bridge_session_id = "e2e-bridge-test-1"
req = urllib.request.Request(
    f"http://localhost:8080/auth/dev/bridge/{bridge_session_id}",
    data=json.dumps({"token": initial_jwt, "sessionId": session_id}).encode(),
    headers={"Content-Type": "application/json"}
)
with urllib.request.urlopen(req) as resp:
    res = json.loads(resp.read().decode())
    assert res.get("success") is True, f"Failed: {res}"
    print("   Token posted to bridge.")

print("3. Unity polling dev bridge...")
req = urllib.request.Request(f"http://localhost:8080/auth/dev/bridge/{bridge_session_id}")
with urllib.request.urlopen(req) as resp:
    handshake = json.loads(resp.read().decode())
    assert handshake.get("ready") is True, f"Not ready: {handshake}"
    assert handshake.get("token") == initial_jwt
    secret_a = handshake.get("refreshSecret")
    assert secret_a, "Missing refreshSecret in handshake!"
    print(f"   Received token and initial secret_a (len={len(secret_a)}).")

print("4. Verifying initial JWT with GET /api/players/me...")
req = urllib.request.Request(
    "http://localhost:8080/api/players/me",
    headers={"Authorization": f"Bearer {initial_jwt}"}
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    player_data = json.loads(resp.read().decode())
    print(f"   Player authenticated: {player_data['username']} (Aura: {player_data['aura']}, Rep: {player_data['academicReputation']})")

print("5. Refreshing token via dev bridge using secret_a...")
req = urllib.request.Request(
    "http://localhost:8080/auth/dev/bridge/refresh",
    data=json.dumps({"bridgeSessionId": bridge_session_id, "refreshSecret": secret_a}).encode(),
    headers={"Content-Type": "application/json"}
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    ref_res = json.loads(resp.read().decode())
    assert ref_res.get("success") is True
    jwt_refreshed_1 = ref_res["token"]
    secret_b = ref_res["refreshSecret"]
    assert secret_b != secret_a, "Secret was not rotated!"
    print("   Token refreshed successfully and secret rotated (Secret A -> Secret B).")

print("6. Testing replay attack with invalidated secret_a...")
try:
    req = urllib.request.Request(
        "http://localhost:8080/auth/dev/bridge/refresh",
        data=json.dumps({"bridgeSessionId": bridge_session_id, "refreshSecret": secret_a}).encode(),
        headers={"Content-Type": "application/json"}
    )
    urllib.request.urlopen(req)
    assert False, "Replay attack should have returned 401!"
except urllib.error.HTTPError as e:
    assert e.code == 401, f"Expected 401, got {e.code}"
    print("   PASS: Replaying old secret_a returned HTTP 401 Unauthorized.")

print("7. Refreshing with rotated secret_b...")
req = urllib.request.Request(
    "http://localhost:8080/auth/dev/bridge/refresh",
    data=json.dumps({"bridgeSessionId": bridge_session_id, "refreshSecret": secret_b}).encode(),
    headers={"Content-Type": "application/json"}
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    ref_res_2 = json.loads(resp.read().decode())
    jwt_refreshed_2 = ref_res_2["token"]
    secret_c = ref_res_2["refreshSecret"]
    assert secret_c != secret_b
    print("   PASS: Rotated secret_b succeeded, new secret_c issued.")

print("8. Testing gameplay stat mutation with fresh token...")
req = urllib.request.Request(
    "http://localhost:8080/api/players/me/stats",
    data=json.dumps({"auraDelta": 2, "academicReputationDelta": 1}).encode(),
    headers={
        "Authorization": f"Bearer {jwt_refreshed_2}",
        "Content-Type": "application/json"
    },
    method="PATCH"
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    stats = json.loads(resp.read().decode())
    print(f"   PASS: Stat mutation succeeded! Aura: {stats['aura']}, Rep: {stats['academicReputation']}")

print("\nALL VERIFICATION CHECKS PASSED SUCCESSFULLY!")
