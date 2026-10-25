# Capturing the official server

Evidence for a behaviour comes from a capture of the official server, the client's data, or a measurement. This page
shows how to record and read a capture with the tools in `captures/`. A capture holds the login traffic of the
account that made it, so it **never** leaves your computer: no commit, no issue, no chat, no Discord. See
[CAPTURE_FINDINGS_2026-10-25.md](CAPTURE_FINDINGS_2026-10-25.md) for what one session already showed.

Only record sessions of your own account, and do not share what you see of other players' accounts.

## What you need

- The game client and your own account on the official server.
- .NET 9 SDK (the same as for the server).

## Record

1. Make a copy of the client's `connect.cfg` and `connectn.cfg` as `*.official` in the game folder, and point the
   originals at `127.0.0.1` (see [GETTING_STARTED.md](GETTING_STARTED.md)). Keep a `*.local` copy of those too.
2. Stop your own server (the proxy uses the same port, 25860).
3. Build and run the proxy. It listens on `127.0.0.1:25860`, forwards to the official server and writes one line per
   chunk to a `.jsonl` file:

```powershell
dotnet build captures/LogProxy -c Release
dotnet captures/LogProxy/bin/Release/net9.0/LogProxy.dll <official-host> 25860 captures/session.jsonl
```

4. Start the game and log in. Do one thing at a time, wait 3 to 5 seconds between actions, and write down the clock
   time of each action. Times are how you find the right packets later.
5. Log out from the game menu and close the client. The proxy writes a `closed` line when the session ends.

`captures/Use-Official.ps1` and `captures/Use-Local.ps1` switch the client between the official and the local server
(`-GameDir` sets the game folder).

## Read

```powershell
dotnet build captures/DecodeCapture -c Release
dotnet captures/DecodeCapture/bin/Release/net9.0/DecodeCapture.dll captures/session.jsonl 10:00:00 10:05:00 all
```

The arguments are the file, the first and last time to show, and `all` for every field (or `hex` for raw bytes). Decode
the whole session so the tool learns which process ids belong to which object. Copy the file first if the proxy is still
writing to it.

## Reading tips

- The client's frames are obfuscated, the server's are not. The decoder undoes it.
- Message names the decoder prints are guesses from a catalog (`Sphere.NetworkProtocol/mbc_commands.jsonl`); the same
  wire message can carry a different real meaning (a "move into a container" shows up under an unrelated name).
  Compare the bytes with what you did in game before trusting a name.
- Compare two samples before naming a field: change one thing in game, capture, and see which byte moved.

## Rules

- No password, token or capture is committed, pasted or repeated (see `AGENTS.md`).
- Label every behaviour you cannot back with a capture as "custom" and make it configurable.
