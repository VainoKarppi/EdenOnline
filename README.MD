# Arma 3 3DEN Multiplayer Extension

## Overview
This project enables multiplayer functionality for **Arma 3 3DEN editor**, converting the normally single-player editor into a synchronized multiplayer experience.  
It allows object creation, camera movements, and other 3DEN interactions to be synced across clients via a server.



## Features

- Sync 3DEN object creation across multiple clients
- Sync camera movements for all players
- TODO

## Demo

<video src="Docs/ObjectSyncTest.mp4" autoplay loop muted controls width="800">
  Your browser does not support the video tag.
</video>

> Demonstrates object creation synchronized between multiple clients in 3DEN editor.  
> **Note:** There are currently **two objects** shown because the second object is a mirrored copy used for testing on a single client.

## Build from source

### Requirements

- .NET SDK version specified in `global.json`
- Arma 3, required for 3DEN integration testing
- Windows 10/11 x64, required for publishing the extension DLL
- Visual Studio 2022 Build Tools with the Desktop development with C++ workload, required for NativeAOT publish

Linux is supported for restore, build, and smoke tests. Mono is not used.

| Distribution | Package |
| --- | --- |
| Gentoo | `virtual/dotnet-sdk` |
| Debian/Ubuntu | `dotnet-sdk-10.0` |

Debian may require the Microsoft package repository if the SDK is not available from the default repositories:

```bash
. /etc/os-release
wget "https://packages.microsoft.com/config/debian/$VERSION_ID/packages-microsoft-prod.deb" -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
sudo apt install dotnet-sdk-10.0
```

Use the matching Debian version from `https://packages.microsoft.com/config/debian/`.

Ubuntu 24.04 and newer should use the Ubuntu feed or Ubuntu .NET backports for .NET 10. Microsoft repository packages for older supported Ubuntu versions are listed at `https://packages.microsoft.com/config/ubuntu/`.

### Build

```bash
dotnet build EdenOnline.csproj --configuration Debug
```

### Smoke tests

```bash
dotnet run --project tests/EdenOnline.SmokeTests/EdenOnline.SmokeTests.csproj --configuration Debug
```

### Cleanup

```bash
dotnet clean EdenOnline.csproj --configuration Debug
```

### Container build

Build and smoke tests can also be run with the official .NET SDK container image:

```bash
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -v "$PWD:/workspace" \
  -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet build EdenOnline.csproj --configuration Debug

docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -v "$PWD:/workspace" \
  -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet run --project tests/EdenOnline.SmokeTests/EdenOnline.SmokeTests.csproj --configuration Debug
```

Use `podman` instead of `docker` for Podman. On SELinux systems, append `:Z` to the bind mount, for example `-v "$PWD:/workspace:Z"`.

The container workflow does not publish the Windows NativeAOT DLL.

### Publish Windows DLL

The Windows NativeAOT settings are defined in `EdenOnline.csproj`:

```bash
dotnet publish EdenOnline.csproj
```

Shared Rider run configurations are available in `.run/`. VS Code tasks are available in `.vscode/tasks.json`.

---

### Project Structure

```text
EdenOnline/
├── @EdenOnline     # Mod / SQF Code
├── src/                        # C# extension source code
├── Tools/                      # .ps1 script that will run Arma 3 with the changes in C# and sqf code

```

## Edit and Run .SQF Scripts with VSCode and C#

You can edit your `.sqf` source code in the `src` folder (C# project) and then run the VSCode task `Test With Arma` to execute it.

The `src/Core` folder is its own project and should have minimal changes. If you wish to make modifications there, see this repository: [EdenOnline](https://github.com/VainoKarppi/EdenOnline)

### Steps

1. Open the `src` folder in VSCode.
2. Edit your `.sqf` scripts as needed in `@EdenOnline/addons` folder.
3. Run the VSCode task `Test With Arma` to launch your scripts or integration with Arma.

This workflow allows you to quickly test changes without manually copying files or launching Arma separately.

### Disable Message Mirroring for Testing

For testing purposes, the server currently mirrors edits back to the sender client.  

In `Server.cs` (line ~330):

```csharp
// if (message.SenderId == client.Id) continue; // Don't send back to client
```
