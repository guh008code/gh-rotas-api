import base64
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("deploy_ssm", Path(__file__).with_name("deploy-ssm.py"))
deploy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deploy)


class DeploySsmTests(unittest.TestCase):
    def run_deploy(self, responses):
        env = {"EC2_INSTANCE_ID": "i-test", "DEPLOY_IMAGE": "registry/api@sha256:test", "AWS_REGION": "us-east-1"}
        with patch.dict(os.environ, env), patch.object(deploy, "aws", side_effect=responses) as aws, patch.object(deploy.time, "sleep"), contextlib.redirect_stdout(io.StringIO()):
            deploy.main()
        return aws

    def test_waits_for_success_and_transmits_script_without_env_file(self):
        responses = [
            json.dumps({"Command": {"CommandId": "command-test"}}),
            subprocess.CalledProcessError(1, "aws", stderr="InvocationDoesNotExist"),
            json.dumps({"Status": "InProgress"}),
            json.dumps({"Status": "Success", "StandardOutputContent": "ok"}),
        ]
        aws = self.run_deploy(responses)
        self.assertEqual(aws.call_count, 4)
        args = aws.call_args_list[0].args
        parameters = json.loads(args[args.index("--parameters") + 1])
        command = parameters["commands"][0]
        expected = base64.b64encode(Path("scripts/deploy-ec2.sh").read_bytes()).decode()
        self.assertIn(expected, command)
        self.assertNotIn("MSSQL_SA_PASSWORD", command)
        self.assertEqual(parameters["executionTimeout"], ["900"])

    def test_remote_failure_fails_workflow(self):
        with self.assertRaisesRegex(RuntimeError, "Failed"):
            self.run_deploy([
                json.dumps({"Command": {"CommandId": "command-test"}}),
                json.dumps({"Status": "Failed", "StandardErrorContent": "rollback"}),
            ])

    def test_access_denied_is_not_treated_as_pending(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_deploy([
                json.dumps({"Command": {"CommandId": "command-test"}}),
                subprocess.CalledProcessError(1, "aws", stderr="AccessDeniedException"),
            ])

    def test_timeout_does_not_report_success(self):
        with self.assertRaises(TimeoutError):
            self.run_deploy([
                json.dumps({"Command": {"CommandId": "command-test"}}),
                *[json.dumps({"Status": "InProgress"}) for _ in range(210)],
            ])


if __name__ == "__main__":
    unittest.main()