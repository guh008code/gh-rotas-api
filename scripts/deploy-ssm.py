"""Entrega o script por SSM e aguarda o resultado real, sem enviar segredos."""
import base64
import json
import os
from pathlib import Path
import shlex
import subprocess
import time


def aws(*args):
    return subprocess.run(
        ["aws", *args, "--output", "json"], check=True, capture_output=True, text=True
    ).stdout


def main():
    instance = os.environ["EC2_INSTANCE_ID"]
    image = os.environ["DEPLOY_IMAGE"]
    region = os.environ["AWS_REGION"]
    payload = base64.b64encode(Path("scripts/deploy-ec2.sh").read_bytes()).decode()
    command = (
        "set -eu; script=$(mktemp); trap 'rm -f \"$script\"' EXIT; "
        f"printf %s {shlex.quote(payload)} | base64 -d > \"$script\"; "
        f"bash \"$script\" {shlex.quote(image)} {shlex.quote(region)}"
    )
    result = json.loads(aws(
        "ssm", "send-command", "--instance-ids", instance,
        "--document-name", "AWS-RunShellScript", "--timeout-seconds", "120",
        "--parameters", json.dumps({"commands": [command], "executionTimeout": ["900"]}),
        "--comment", "Deploy GH Rotas API; preserva SQL Server e volume",
    ))
    command_id = result["Command"]["CommandId"]
    print(f"SSM command: {command_id}", flush=True)
    for _ in range(210):
        try:
            status = json.loads(aws(
                "ssm", "get-command-invocation", "--command-id", command_id,
                "--instance-id", instance,
            ))
        except subprocess.CalledProcessError as error:
            if "InvocationDoesNotExist" not in error.stderr:
                raise
            time.sleep(5)
            continue
        if status["Status"] not in ("Pending", "InProgress", "Delayed", "Cancelling"):
            print(status.get("StandardOutputContent", ""))
            print(status.get("StandardErrorContent", ""))
            if status["Status"] != "Success":
                raise RuntimeError(f"Deploy SSM: {status['Status']}")
            return
        time.sleep(5)
    raise TimeoutError(f"Consulte o comando {command_id} no SSM antes de tentar novamente.")


if __name__ == "__main__":
    main()