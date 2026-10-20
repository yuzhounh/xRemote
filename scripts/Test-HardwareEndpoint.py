"""Read-only, certificate-pinned hardware endpoint check; never print pairing secrets."""
import argparse
import hashlib
import http.client
import json
import pathlib
import ssl
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument("--pairing", type=pathlib.Path)
parser.add_argument("--host-package", type=pathlib.Path)
parser.add_argument("--sandbox", type=pathlib.Path)
parser.add_argument("--output", type=pathlib.Path)
parser.add_argument("--storage-only", action="store_true")
args = parser.parse_args()
process = None
if args.host_package:
    config = args.sandbox.resolve() / "config"
    executable = args.host_package.resolve() / "xRemote.ControlHost.exe"
    subprocess.run([str(executable), "--init", "127.0.0.1", str(config), "24446", "lock"], check=True, stdout=subprocess.DEVNULL)
    args.pairing = config / "pairing.json"
    process = subprocess.Popen([str(executable), "--console", "--read-only", "--config", str(config / "server.json")], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
pairing = json.loads(args.pairing.read_text(encoding="utf-8-sig"))
endpoint = "/v1/storage" if args.storage_only else "/v1/hardware"

def request(path, authenticated=True):
    connection = http.client.HTTPSConnection(pairing["address"], pairing["port"], timeout=24, context=ssl._create_unverified_context())
    try:
        connection.connect()
        assert hashlib.sha256(connection.sock.getpeercert(binary_form=True)).hexdigest().lower() == pairing["certificateSha256"].lower(), "certificate pin mismatch"
        headers = {"Authorization": "Bearer " + pairing["token"]} if authenticated else {}
        connection.request("GET", path, headers=headers)
        response = connection.getresponse()
        payload = response.read(65537)
        assert len(payload) <= 65536, "response too large"
        return response.status, json.loads(payload) if payload else None
    finally:
        connection.close()

try:
    if process:
        for attempt in range(20):
            try:
                request("/v1/status")
                break
            except OSError:
                time.sleep(0.2)
    assert request(endpoint, False)[0] == 401, "information endpoint must require authentication"
    status, data = request(endpoint + "?refresh=true")
    assert status == 200 and data["schema"] == 1 and data["machine"], "hardware response invalid"
    if args.output:
        args.output.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    if args.storage_only:
        assert not {"windows", "cpu", "memoryBytes", "gpus"}.intersection(data), "storage response must exclude fixed hardware"
    else:
        assert data["windows"]["build"] and data["cpu"]["cores"] > 0 and data["memoryBytes"] > 0
    indices = {disk["index"] for disk in data["disks"]}
    for volume in data["volumes"]:
        assert set(volume["diskNumbers"]) <= indices, "partition mapping invalid"
        if volume["freeBytes"] is not None:
            assert 0 <= volume["freeBytes"] <= volume["sizeBytes"], "volume capacity invalid"
    assert request(endpoint)[1]["observedAt"] == data["observedAt"], "cache not reused"
    assert request("/v1/status")[0] == 200, "session status compatibility"
    print(json.dumps({"machine": data["machine"], "endpoint": endpoint, "physicalDisks": len(data["disks"]), "volumes": len(data["volumes"]), "unavailable": data["unavailable"], "validated": True}, ensure_ascii=False))
finally:
    if process:
        process.terminate()
        process.wait(timeout=10)
