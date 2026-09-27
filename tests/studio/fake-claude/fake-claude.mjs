// A stand-in for the Claude Code command line, driven by tests/studio/BimOpenFlow.Studio.Tests
// (ClaudeCliBackendTests) and the IFC ask's equivalent. It speaks the same `--output-format
// stream-json` lines the real CLI does, but instead of reasoning it plays back a script the test
// wrote to <cwd>/fake-claude-script.json, and it calls the real MCP tool server named in
// --mcp-config over HTTP so the test's assertions see real store effects. See README.md for the
// script shape.

import fs from "node:fs";
import path from "node:path";

const args = process.argv.slice(2);
const cwd = process.cwd();

function argValue(name) {
  const i = args.indexOf(name);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : undefined;
}

function readStdin() {
  return new Promise((resolve) => {
    let data = "";
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (chunk) => (data += chunk));
    process.stdin.on("end", () => resolve(data));
  });
}

function println(obj) {
  process.stdout.write(JSON.stringify(obj) + "\n");
}

async function rpc(url, method, params) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params: params ?? {} }),
  });
  return response.json();
}

async function main() {
  const stdin = await readStdin();

  const callLogPath = path.join(cwd, "fake-claude-calls.jsonl");
  const runIndex = fs.existsSync(callLogPath)
    ? fs.readFileSync(callLogPath, "utf8").split("\n").filter((line) => line.trim().length > 0).length
    : 0;

  const envNames = Object.keys(process.env).filter((name) => /^(ANTHROPIC|CLAUDE)/i.test(name));
  fs.appendFileSync(callLogPath, JSON.stringify({ args, stdin, env: envNames }) + "\n");

  const resumeId = argValue("--resume");
  const sessionId = resumeId ?? `fake-session-${runIndex}`;

  const mcpConfig = JSON.parse(fs.readFileSync(argValue("--mcp-config"), "utf8"));
  const serverKey = Object.keys(mcpConfig.mcpServers)[0];
  const serverUrl = mcpConfig.mcpServers[serverKey].url;

  let toolNames = [];
  let connected = false;
  try {
    const listed = await rpc(serverUrl, "tools/list", {});
    toolNames = (listed.result?.tools ?? []).map((tool) => tool.name);
    connected = true;
  } catch {
    connected = false;
  }

  const disallowed = new Set(
    (argValue("--disallowedTools") ?? "")
      .split(",")
      .filter(Boolean)
      .map((name) => name.slice(`mcp__${serverKey}__`.length)),
  );
  const tools = connected
    ? toolNames.filter((name) => !disallowed.has(name)).map((name) => `mcp__${serverKey}__${name}`)
    : [];

  println({
    type: "system",
    subtype: "init",
    session_id: sessionId,
    mcp_servers: [{ name: serverKey, status: connected ? "connected" : "failed" }],
    tools,
    slash_commands: [],
  });

  const scriptPath = path.join(cwd, "fake-claude-script.json");
  const script = fs.existsSync(scriptPath) ? JSON.parse(fs.readFileSync(scriptPath, "utf8")) : { runs: [] };
  const run = script.runs[runIndex] ?? { steps: [] };

  let calls = 0;
  let nextToolUseId = 0;

  for (const step of run.steps) {
    if ("text" in step) {
      println({ type: "assistant", message: { content: [{ type: "text", text: step.text }] } });
    } else if ("call" in step) {
      const id = `t${++nextToolUseId}`;
      println({
        type: "assistant",
        message: { content: [{ type: "tool_use", id, name: `mcp__${serverKey}__${step.call}`, input: step.args ?? {} }] },
      });
      calls++;

      let resultText;
      let isError;
      try {
        const response = await rpc(serverUrl, "tools/call", { name: step.call, arguments: step.args ?? {} });
        if (response.error) {
          isError = true;
          resultText = JSON.stringify({ ok: false, error: response.error.message ?? "unknown error" });
        } else {
          resultText = response.result?.content?.[0]?.text ?? "";
          isError = !!response.result?.isError;
        }
      } catch (e) {
        isError = true;
        resultText = JSON.stringify({ ok: false, error: String(e) });
      }

      println({
        type: "user",
        message: {
          content: [{ type: "tool_result", tool_use_id: id, content: [{ type: "text", text: resultText }], is_error: isError }],
        },
      });
    } else if ("result" in step) {
      const turns = calls + 1;
      println({
        type: "result",
        subtype: "success",
        is_error: false,
        result: step.result,
        session_id: sessionId,
        num_turns: turns,
        usage: { input_tokens: 100 * turns, cache_read_input_tokens: 0, cache_creation_input_tokens: 0, output_tokens: 20 * turns },
      });
    } else if ("error" in step) {
      // S1 found the not-logged-in result keeps subtype "success" even though is_error is true.
      println({ type: "result", subtype: "success", is_error: true, result: step.error, terminal_reason: "api_error" });
    } else if ("maxTurns" in step) {
      println({ type: "result", subtype: "error_max_turns", is_error: true, result: "", session_id: sessionId });
    } else if ("exit" in step) {
      if (step.stderr) process.stderr.write(step.stderr);
      process.exit(step.exit);
    } else if ("sleep" in step) {
      // Ticks a heartbeat file while it waits, so a test that cancels mid-sleep can tell whether
      // the process tree was really killed (the file stops advancing) rather than just exiting on
      // its own.
      const heartbeatPath = path.join(cwd, "fake-claude-heartbeat.txt");
      let ticks = 0;
      const interval = setInterval(() => fs.writeFileSync(heartbeatPath, String(++ticks)), 100);
      await new Promise((resolve) => setTimeout(resolve, step.sleep));
      clearInterval(interval);
    }
  }

  process.exit(0);
}

main();
