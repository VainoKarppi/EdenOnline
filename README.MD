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

## Getting Started

### Requirements

- **Arma 3** installed
- Windows machine for development
- .NET 10.0 or newer

---

### Project Structure

```text
arma3-3den-multiplayer/
├── Examples/@ArmaExtension     # Mod / SQF Code
├── src/                        # C# extension source code
├── Tools/                      # .ps1 script that will run Arma 3 with the changes in C# and sqf code