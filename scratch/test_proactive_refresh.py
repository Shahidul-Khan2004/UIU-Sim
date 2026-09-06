import json
import time
import base64
import urllib.request

env_vars = {}
with open("/mnt/shared/Code/UIU-Sim/backend/.env") as f:
    for line in f:
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            k, v = line.split("=", 1)
            env_vars[k.strip()] = v.strip()

clerk_secret = env_vars["CLERK_SECRET_KEY"]
session_id = "sess_3Ixq7MGCZf6MaObaK3BrNqwRLMV"

def parse_exp(jwt_token):
    payload_b64 = jwt_token.split(".")[1]
    padded = payload_b64 + "=" * (-len(payload_b64) % 4)
    payload = json.loads(base64.urlsafe_b64decode(padded).decode())
    return payload["exp"]

print("1. Minting token...")
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

bridge_id = "proactive-test"
req = urllib.request.Request(
    f"http://localhost:8080/auth/dev/bridge/{bridge_id}",
    data=json.dumps({"token": token_1, "sessionId": session_id}).encode(),
    headers={"Content-Type": "application/json"}
)
urllib.request.urlopen(req)

req = urllib.request.Request(f"http://localhost:8080/auth/dev/bridge/{bridge_id}")
with urllib.request.urlopen(req) as resp:
    handshake = json.loads(resp.read().decode())
    current_token = handshake["token"]
    current_secret = handshake["refreshSecret"]

exp = parse_exp(current_token)
print(f"   Initial token exp in {exp - int(time.time())}s")

# Simulate AuthTokenProvider:
# Proactively refresh when within 15 seconds of exp:
def get_valid_token(tok, sec):
    global current_token, current_secret
    t_exp = parse_exp(tok)
    now = int(time.time())
    if now >= t_exp - 15:
        print(f"   [AuthTokenProvider] Token expiring in {t_exp - now}s (< 15s buffer). Proactively refreshing...")
        r = urllib.request.Request(
            f"http://localhost:8080/auth/dev/bridge/refresh",
            data=json.dumps({"bridgeSessionId": bridge_id, "refreshSecret": sec}).encode(),
            headers={"Content-Type": "application/json"}
        )
        with urllib.request.urlopen(r) as res:
            d = json.loads(res.read().decode())
            current_token = d["token"]
            current_secret = d["refreshSecret"]
            print(f"   [AuthTokenProvider] Refreshed proactively! New exp in {parse_exp(current_token) - int(time.time())}s")
            return current_token, current_secret
    return tok, sec

print("2. Sleeping 16 seconds (token now within 15s of 30s exp)...")
time.sleep(16)

valid_tok, valid_sec = get_valid_token(current_token, current_secret)
assert valid_tok != token_1, "Token should have proactively refreshed!"

# Send mutation with valid_tok
req = urllib.request.Request(
    "http://localhost:8080/api/players/me/stats",
    data=json.dumps({"auraDelta": 1, "academicReputationDelta": 1}).encode(),
    headers={
        "Authorization": f"Bearer {valid_tok}",
        "Content-Type": "application/json"
    },
    method="PATCH"
)
with urllib.request.urlopen(req) as resp:
    assert resp.status == 200
    st = json.loads(resp.read().decode())
    print(f"   PROACTIVE REFRESH SUCCESS! Aura={st['aura']}, Rep={st['academicReputation']}")
