import asyncio
import os
import json
import nats
from nats.js.errors import BadRequestError

ANOMALY_SUBJECT = "traffic.anomaly"
ANOMALY_THRESHOLD = 0.80

async def tail_log_and_publish(js, filepath, subject):
    """Acts as the bridge: safely tails the file and pushes to NATS."""
    print(f"Waiting for Zeek to create {filepath}...")
    
    while not os.path.exists(filepath):
        await asyncio.sleep(1)

    print(f"Found log file. Streaming into NATS...")

    offset = os.path.getsize(filepath)

    while True:
        try:
            with open(filepath, 'r') as f:
                f.seek(offset)
                lines = f.readlines()
                offset = f.tell()
        except OSError as e:
            print(f"Could not read log file: {e}")
            await asyncio.sleep(0.5)
            continue

        for line in lines:
            raw_line = line.strip()
            # Only publish valid JSON lines to avoid Zeek startup text
            if raw_line.startswith('{'):
                await js.publish(subject, raw_line.encode())

        if not lines:
            await asyncio.sleep(0.1) 

def compute_anomaly_score(data: dict) -> float:
    """Compute a heuristic anomaly score (0.0–1.0) from a Zeek conn record."""
    orig_bytes = int(data.get('orig_bytes', 0) or 0)
    resp_bytes = int(data.get('resp_bytes', 0) or 0)
    total_bytes = orig_bytes + resp_bytes

    if total_bytes > 10_000_000:
        return 0.97
    if total_bytes > 1_000_000: 
        return 0.92
    if orig_bytes > 5_000:
        return 0.88
    if orig_bytes > 500:
        return 0.82
    return 0.0

async def process_anomalies(nc, js):
    """Acts as the brain: scores Zeek connections and forwards anomalies to Sentinel."""
    sub = await js.subscribe("zeek.raw.conn", durable="anomaly-detector")
    print("Anomaly Detector listening for traffic...")

    async for msg in sub.messages:
        try:
            data = json.loads(msg.data.decode())
            score = compute_anomaly_score(data)

            if score >= ANOMALY_THRESHOLD:
                # Publish scored anomaly to core NATS for the C# Sentinel worker
                payload = json.dumps({"score": score, "data": data}).encode()
                await nc.publish(ANOMALY_SUBJECT, payload)
                print(f"ANOMALY: {data.get('id.orig_h')} → score={score:.0%}")
        except Exception as e:
            print(f"Failed to process message: {e}")
        finally:
            await msg.ack()

async def main():
    nats_url = os.getenv("ConnectionStrings__nats", "nats://localhost:4222")
    nc = await nats.connect(nats_url)
    js = nc.jetstream()

    try:
        await js.add_stream(name="ZEEK_TRAFFIC", subjects=["zeek.raw.conn"])
    except BadRequestError:
        await js.update_stream(name="ZEEK_TRAFFIC", subjects=["zeek.raw.conn"])

    log_path = os.path.join(os.getenv("ZEEK_LOGS_PATH", "/output"), "conn.log")
    
    await asyncio.gather(
        tail_log_and_publish(js, log_path, "zeek.raw.conn"),
        process_anomalies(nc, js)
    )

if __name__ == "__main__":
    asyncio.run(main())