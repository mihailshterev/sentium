import asyncio
import os
import json
import nats
import numpy as np

async def main():
    nats_url = os.getenv("ConnectionStrings__nats")
    
    if nats_url and not nats_url.startswith("nats://"):
        nats_url = f"nats://{nats_url}"

    print(f"Connecting to NATS at {nats_url}...")
    nc = await nats.connect(nats_url)
    js = nc.jetstream()

    subject_in = os.getenv("NATS_SUBJECT_IN", "traffic.raw")
    subject_out = os.getenv("NATS_SUBJECT_OUT", "traffic.anomaly")
    
    try:
        await js.add_stream(name="ZEEK_TRAFFIC", subjects=[subject_in, subject_out])
        print(f"Stream 'ZEEK_TRAFFIC' is ready.")
    except Exception as e:
        print(f"Stream info: {e}")

    sub = await js.pull_subscribe(subject_in, "ml-filter-group")
    print(f"Mock ML Filter started. Watching {subject_in}...")

    while True:
        try:
            msgs = await sub.fetch(10, timeout=1) 
            
            for msg in msgs:
                data = json.loads(msg.data.decode())
                
                # --- MOCK ML LOGIC --- WILL BE REPLACED WITH REAL MODEL LATER ---
                orig_bytes = data.get('orig_bytes', 0)
                try:
                    byte_count = int(orig_bytes) if str(orig_bytes).isdigit() else 0
                except:
                    byte_count = 0

                if byte_count > 500:
                    score = 0.99
                    alert = {
                        "score": score,
                        "data": data
                    }
                    print(f"Anomaly! {data.get('id.orig_h')} -> {byte_count} bytes")
                    await js.publish(subject_out, json.dumps(alert).encode('utf-8'))
                
                await msg.ack()

        except nats.errors.TimeoutError:
            continue
        except Exception as e:
            print(f"Loop error: {e}")
            await asyncio.sleep(1)

if __name__ == "__main__":
    asyncio.run(main())