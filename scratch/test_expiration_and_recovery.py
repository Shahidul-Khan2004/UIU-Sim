import json
import time
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

print("1. Establishing new bridge session...")
bridge_session_id = "e2e-bridge-expiry-test"
req = urllib.request.Request(
    f"https://api.clerk.com/v1/sessions/{session_id}/tokens",
    data=b'{"expires_in_seconds":30}',
    headers={
        "Authorization": f"Bearer {clerk_secret}",
        "Content-Type": "application/json",
        "User-Agent": "UIU-Sim-Backend/1.0"
    }
)
with urllib.request.urlopen(req) as resp:
    token_1 = json.loads(resp.read().decode())["jwt"]

req = urllib.request.Request(
    f"http://localhost:8080/auth/dev/bridge/{bridge_session_id}",
    data=json.dumps({"token": token_1, "sessionId": session_id}).encode(),
    headers={"Content-Type": "application/json"}
)
urllib.request.urlopen(req)

req = urllib.request.Request(f"http://localhost:8080/auth/dev/bridge/{bridge_session_id}")
with urllib.request.urlopen(req) as resp:
    handshake = json.loads(resp.read().decode())
    active_secret = handshake["refreshSecret"]

print("2. Immediate call GET /api/players/me (within valid window)...")
req = urllib.request.Request(
    "http://localhost:8080/api/players/me",
    headers={"Authorization": f"Bearer {token_1}"}
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    p = json.loads(resp.read().decode())
    print(f"   Hydration OK: Aura={p['aura']}, Rep={p['academicReputation']}")

print("3. Waiting 95 seconds for token_1 to expire past the 60s clock-skew leeway...")
time.sleep(95)

print("4. Attempting PATCH /api/players/me/stats with EXPIRED token_1 (simulating pre-fix failure)...")
try:
    req = urllib.request.Request(
        "http://localhost:8080/api/players/me/stats",
        data=json.dumps({"auraDelta": 2, "academicReputationDelta": 1}).encode(),
        headers={
            "Authorization": f"Bearer {token_1}",
            "Content-Type": "application/json"
        },
        method="PATCH"
    )
    urllib.request.urlopen(req)
    assert False, "Expired token should have been rejected with 401!"
except urllib.error.HTTPError as e:
    assert e.code == 401
    body = json.loads(e.read().decode())
    print(f"   PROVEN: Expired token rejected with HTTP 401: '{body['message']}'")

print("5. Executing token refresh via dev bridge (simulating AuthTokenProvider)...")
req = urllib.request.Request(
    "http://localhost:8080/auth/dev/bridge/refresh",
    data=json.dumps({"bridgeSessionId": bridge_session_id, "refreshSecret": active_secret}).encode(),
    headers={"Content-Type": "application/json"}
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    res = json.loads(resp.read().decode())
    token_2 = res["token"]
    rotated_secret = res["refreshSecret"]
    print("   Refresh OK: Fresh token acquired and secret rotated.")

print("6. Retrying PATCH /api/players/me/stats with fresh token_2 (simulating ApiClient 401 retry)...")
req = urllib.request.Request(
    "http://localhost:8080/api/players/me/stats",
    data=json.dumps({"auraDelta": 2, "academicReputationDelta": 1}).encode(),
    headers={
        "Authorization": f"Bearer {token_2}",
        "Content-Type": "application/json"
    },
    method="PATCH"
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    stats = json.loads(resp.read().decode())
    print(f"   SUCCESS! Stat mutation succeeded: Aura={stats['aura']}, Rep={stats['academicReputation']}")

print("\nEXPIRATION AND RECOVERY VERIFICATION 100% COMPLETE!")
