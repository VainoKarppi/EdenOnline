# Arma 3 3DEN Multiplayer Extension

## Overview
This project enables multiplayer functionality for **Arma 3 3DEN editor**, converting the normally single-player editor into a synchronized multiplayer experience.  
It allows object creation, camera movements, and other 3DEN interactions to be synced across clients via a server.

---

## Features

- Sync 3DEN object creation across multiple clients
- Sync camera movements for all players
- TODO

---

## Demo

![Object Sync Test](Docs/ObjectSyncTest.gif)

> Demonstrates object creation synchronized between multiple clients in 3DEN editor.

---

## Getting Started

### Requirements

- **Arma 3** installed
- Windows machine for development
- .NET 10.0 or newer

---

### Project Structure

```text
EdenOnline/
├── Examples/@ArmaExtension     # Mod / SQF Code
├── src/                        # C# extension source code
├── Tools/                      # .ps1 script that will run Arma 3 with the changes in C# and sqf code

```

## Edit and Run .SQF Scripts with VSCode and C#

You can edit your `.sqf` source code in the `src` folder (C# project) and then run the VSCode task `Test With Arma` to execute it.

The `src/Core` folder is its own project and should have minimal changes. If you wish to make modifications there, see this repository: [ArmaExtension](https://github.com/VainoKarppi/ArmaExtension)

### Steps

1. Open the `src` folder in VSCode.
2. Edit your `.sqf` scripts as needed in `Examples/@ArmaExtension/addons` folder.
3. Run the VSCode task `Test With Arma` to launch your scripts or integration with Arma.

This workflow allows you to quickly test changes without manually copying files or launching Arma separately.

### Disable Message Mirroring for Testing

For testing purposes, the server currently mirrors edits back to the sender client.  

In `Server.cs` (line ~330):

```csharp
// if (message.SenderId == client.Id) continue; // Don't send back to client
```