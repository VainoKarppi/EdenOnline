# AGENTS.md

Guidance for AI agents working in this repository.

## What this project is

EdenOnline keeps Arma 3 Eden (3DEN) editor changes in sync between connected clients.

- SQF addon: `@EdenOnline/addons/functions` — one function per `fn_<name>.sqf` file, registered in
  [`config.cpp`](@EdenOnline/addons/functions/config.cpp) under `CfgFunctions` (tag `EOEX`, so the
  function variable is `EOEX_fnc_<name>`).
- C# extension: `src/` — NativeAOT DLL that owns the networking. See
  [`DEVSETUP.md`](DEVSETUP.md) and [`DOCUMENTATION.md`](DOCUMENTATION.md).

## Continuous SQF testing loop

SQF changes can be tested without restarting Arma: the **Arma 3 SQF Reloader** VS Code extension
launches the game, recompiles saved functions and exposes a REST API at
`http://127.0.0.1:8764/api`. Start Arma 3 with **Play Arma 3** from the SQF Reloader status bar
entry. If the SQF Reloader AI tool is available in the session, `action=context` tells you whether
the game is running and where the logs are.

1. Replay incoming network messages locally:

   ```sqf
   call EOEX_fnc_testReceive
   ```

   Run it from the VS Code Arma debug console, or through the SQF Reloader AI tool's `eval`
   action. `testReceive` disconnects, starts a local server, and then feeds object, marker, layer,
   comment, trigger, logic and waypoint **create / update / delete** messages through the same
   functions a remote client would use.

2. Read the RPT for errors. Take the newest `%LOCALAPPDATA%\Arma 3\arma3_x64_*.rpt` and look for
   `Error in expression`, `Error position`, `File z\eoex\...`, and the `diag_log` lines you added.

3. Fix the SQF, reload it (below), and run `call EOEX_fnc_testReceive` again.

When a run fails halfway it leaves entities in the editor; delete leftovers before the next run so
the next result is not polluted.

### Reloading changed functions

Arma keeps every SQF source it preprocessed locked for the whole session, so **right after Arma
starts, no `fn_*.sqf` can be edited or saved**. Running the reload script releases the locks,
because Arma no longer needs the original files once the function variables point at snapshots:

```powershell
Tools\ReloadArmaFunctions.ps1        # run once after launch to unlock all sources
```

- **Saved from VS Code**: saving an `fn_*.sqf` recompiles that function automatically
  (`reloadOnSave: reload` in [.vscode/sqfReloader-settings.json](.vscode/sqfReloader-settings.json)).
- **Written by a tool or agent**: plain file writes do not raise VS Code's save event, so the game
  keeps running the old code (and the file cannot be written at all until it is reloaded once).
  Reload explicitly:

  ```powershell
  Tools\ReloadArmaFunctions.ps1 -Function receiveObjectCreate,receiveObjectUpdate
  ```

  Omit `-Function` to reload every `fn_*.sqf`. The script compiles each file from an immutable
  snapshot under `<Arma 3>\z\SqfReloader\Snapshots` and assigns `EOEX_fnc_<name>` in
  `missionNamespace`, exactly like the editor's reload-on-save.

- **Never** run `compile preprocessFileLineNumbers "z\eoex\addons\functions\..."` on a workspace
  file from the debug console: Arma keeps the file-patched source open and locked until the game
  restarts, so the file can no longer be edited or saved. Use the script, which compiles snapshots.

### Adding a new function

1. Create `@EdenOnline/addons/functions/<folder>/fn_<name>.sqf`.
2. Register it in `config.cpp` (`class <name> {};` inside the matching `Functions_*` class) so the
   variable `EOEX_fnc_<name>` exists.
3. Run `Tools\ReloadArmaFunctions.ps1 -Function <name>`, then call `EOEX_fnc_<name>`.

`config.cpp` itself is only re-read when Arma starts (or the mod is rebuilt with
[`Tools/BuildForArma.ps1`](Tools/BuildForArma.ps1)), so until then reach a new helper through an
existing entry point.

### Debugging a failure

Add `diag_log format ["EOEX: %1", _value];` to the suspect function, reload it, run
`call EOEX_fnc_testReceive` again and read the new RPT lines. Remove the temporary logging once the
problem is understood.

## Testing the entity lock indicator

[`fn_testLock`](@EdenOnline/addons/functions/objects/helpers/fn_testLock.sqf) draws the lock indicator
(icon + "LOCKED by <user> (<mode>) - <id>") in the editor without a second client, so the visuals of
the planned attribute/drag lock can be checked:

```sqf
["lock"] call EOEX_fnc_testLock;                          # empty building in front of the camera, locked
["lock", cursorObject, "Razer", "drag"] call EOEX_fnc_testLock;
["hide"] call EOEX_fnc_testLock;                          # keep the lock, hide the indicator
["show"] call EOEX_fnc_testLock;
["unlock"] call EOEX_fnc_testLock;                        # release and delete the created building
```

The indicator is a `Draw3D` mission event handler, not an Eden marker or icon, so nothing else in the
scenario changes. The helper reads the ID with `[_entity, true] call EOEX_fnc_getId` — passing `false`
registers a new ID, which makes `fn_handleCreateObject` skip the entity and the create never reaches
the server. Creating a building must not pass the `isEmpty` argument either: Eden logs "Trying to
create an invalid center EMPTY" and kills the calling script.

## Entity locks (attributes window / drag)

Eden has no 3DEN event for "the attributes window of an entity was opened", so
[`fn_watchAttributesEdit`](@EdenOnline/addons/functions/objects/lock/fn_watchAttributesEdit.sqf) polls
it: the window is the separate display `Display3DENEditAttributes` (idd **315**), which Eden opens on
a double-click. `get3DENActionState` cannot be used for this — it returns `0` while the window is
open, but `do3DENAction "OpenAttributes"` opens it (handy in tests), and
`findDisplay 315 closeDisplay 0` closes it again.

The locked entities are only a **local mirror** per client: `EOEX_var_Locks` maps
`id -> [entity, username, mode, position]` with `mode` `"attributes"` or `"drag"`.

| message | sent when | receiver |
| --- | --- | --- |
| `StartAttributesEdit` (`ids`, `username`) | the attributes window opens, or the selection changes while it is open | `fn_receiveAttributesEditStart` locks every id |
| `StopAttributesEdit` (`ids`) | the window closes | `fn_receiveAttributesEditStop` unlocks them |
| `StartObjectDrag` (`moveId`, `ids`, `username`) | a drag starts | `fn_receiveDragStart` locks the moved ids |
| `EndObjectDrag` (`moveId`, `finalPositions`) | a drag ends | `fn_receiveDragEnd` unlocks them |

`fn_lockEntity` draws the indicator (one `Draw3D` mission event handler, removed again by
`fn_unlockEntity` when the last lock is gone) and `fn_onEntityDragStart` refuses to move a locked
entity: it notifies who holds the lock and puts the entity back to the position stored in the lock.
`fn_watchAttributesEdit` closes the attributes window of a locked entity the same way.

Eden cannot cancel a drag that already started, and its start handler fires only once, so the lock is
enforced continuously by [`fn_enforceLocks`](@EdenOnline/addons/functions/objects/lock/fn_enforceLocks.sqf):
an `EachFrame` handler writes the locked `Position` back whenever the entity is moved and removes the
entity from the selection (Eden drags the selection). `fn_sendObjectAttributes` ignores those writes
for attribute-locked entities, so a refused move never reaches the other clients. Only `"attributes"`
locks are pinned — a `"drag"` lock follows the client that owns the move.

Both clients must run the same extension build, because the method signatures are part of the
handshake hash.

Locks are sent **once per change** (`StartAttributesEdit` when the window opens or the selection
changes, `StopAttributesEdit` when it closes) — no heartbeat, it would only spam the other clients.
A release that never arrives is recovered without any traffic:

- `LocksReleased` (client id + username) is sent by `ClientNetworkEvents.OnOtherClientDisconnected`
  (the holder left the game with the window still open) and by
  `ClientNetworkMethods.RegisterUserName` (a client that registers again is either new or reconnected,
  its previous locks are gone).
- Locks store the holder's client id, `EOEX_var_Locks` entries are
  `id -> [entity, username, mode, position, clientId]`, so two clients with the same profile name do
  not release each other. `"drag"` locks carry `-1` (StartObjectDrag has no client id) and fall back
  to the name.
- `StartAttributesEdit` / `StopAttributesEdit` are in the logging blacklist, they fire on every
  attributes window.

Both clients must run the same addon and extension build: the method signatures are part of the
handshake hash.

## Testing the send path

```sqf
call EOEX_fnc_testSend          # create / update / class change / delete for every entity type
call EOEX_fnc_syncSendTest      # two objects + CreateSyncConnection / RemoveSyncConnection
call EOEX_fnc_syncReceiveTest   # incoming sync connection messages, verified with get3DENConnections
```

`testSend` covers objects (unit and vehicle), triggers, logics, markers, comments, layers and
waypoints: it creates them, changes representative attributes, sends a class change and deletes
them, so the 3DEN event handlers emit `CreateObject`, `UpdateObject` and `RemoveObject`.
There is no second client yet, so nothing is echoed back — verify the `SENT DATA` blocks in the RPT
(or the extension log) instead.

`syncReceiveTest` creates its two objects through `EOEX_fnc_receiveObjectCreate` and then logs
`connection exists = true` / `connection still exists = false`.

Note: creating a **unit** in the editor does not send `CreateObject` on its own.
`OnEditableEntityAdded` reports the new group instead of the unit, and
`EOEX_fnc_handleCreateObject` ignores groups (TODO), which is why the tests send the creation
explicitly.

A **crewed vehicle** is reported twice (first its group, then the vehicle). Both handler threads run
concurrently, so `EOEX_fnc_handleCreateObject` checks for an existing ID and registers a new one
without yielding in between; keep it that way, otherwise the same object is sent twice.

Dragging sends `StartObjectDrag` / `UpdateObjectDrag` / `EndObjectDrag`. `OnEntityDragged` only
fires when the drag starts, so `fn_sendObjectAttributes` feeds the dragged entity's `Position`
changes into `fn_onEntityDragStart`, which streams the delta over UDP at 10 Hz. While

`OnEntityDragged` can report the **group** instead of the grabbed unit (like
`OnEditableEntityAdded`), and a group has no `Position` (`get3DENAttribute` returns `[nil]`). Without
resolving `leader _entity` the delta became `nil`, `vectorDiff` failed and the extension rejected
`UpdateObjectDrag` with "Expected Object[], but received scalar". Keep that guard, the `nil` position
check and the group filter in `fn_onEntityDragStart`/`fn_onEntityDragEnd`. While
`EOEX_var_MoveIds` contains the entity, that function drops `Position`/`Rotation` instead of sending
them as TCP `UpdateObject`; `EndObjectDrag` carries the final position **and** rotation, and
`fn_receiveDragEnd` registers skips for both. On the receiving side `fn_moveInterpolation` writes
the position every frame, so `fn_sendObjectAttributes` ignores attribute events for entities in an
active remote move (`EOEX_var_RemoteMoveIds`) — otherwise every interpolated frame is echoed back to
the client that is dragging.

The change events of those writes are dispatched asynchronously and several of them (one per
interpolation frame) can read the final value right after `fn_receiveDragEnd` cleared the move. That
is why the end of a drag opens a short ignore window (`EOEX_var_RemoteMoveIgnoreIds` /
`_Until`, 0.5 s): a skip entry is single-use, so the first late event would consume it and the next
one would be sent back. `fn_receiveDragEnd` also stops the interpolation (`RemoteMoveTargets = []`)
before applying the final state, so a frame cannot overwrite it.

The mirror image applies to the client that dragged: right after `EndObjectDrag` it owns the final
state, so `fn_receiveObjectUpdate` drops `Position`/`Rotation` for those IDs for 0.5 s
(`EOEX_var_MoveIgnoreIncomingIds` / `_Until`). Echoes from a client that still runs an older build
therefore cannot snap the object back.

Query the selection per type
(`get3DENSelected "waypoint"` etc.): `get3DENSelected ""` nests the categories and `flatten` breaks
waypoint references (`[group, index]`) into a group and a bare index. Also never pass a waypoint to
`isNull`, and always call helpers with an array (`[_entity, true] call EOEX_fnc_getId`), because a
waypoint array is itself a parameter list for `params`.

The drag receiver resolves the ids through all four registries (`EOEX_var_Objects`, `_Markers`,
`_Waypoints`, `_Comments`), so a drag can carry every entity type. Their handles differ — object
handle, marker name, comment index, waypoint `[group, index]` — but `get3DENAttribute "Position"`
and `set3DENAttribute ["Position", …]` work for all of them. Only the liveness check differs, which
is why `fn_moveInterpolation` and `fn_receiveDragEnd` carry a local `_isEntityValid` (objects:
`isNull`, markers: `all3DENEntities # 5`, comments: `# 7`, waypoints: two elements). `Eden_RemoteMoveObjects`
stores `[id, entity, initialPosition]` and `Eden_RemoteMoveTargets` stores `[entity, targetPosition]`.

## Delete message

`RemoveObjects` carries every removal of one operation as a list of `[id, type]` pairs, so a
multi-select delete, a composition removal or an undo is one message instead of one per entity.
`fn_handleDeleteObject` queues the pair and flushes the queue 50 ms after the first removal;
`fn_receiveObjectDelete` receives the list as its only argument (`params ["_entities"]`, a single
delete is a one element list) and removes the whole batch with one `delete3DENEntities`.

The extension forwards it as `ObjectRemoved` with the same list (`ClientNetworkMethods.RemoveObjects`
wraps it once), and `ArmaMethods.RemoveObjects` deserializes Arma's `[[id, type], ...]` into
`object?[][]`. The payload shape changed with the method name, so sender and receiver must be updated
together.

Every element of the SQF arguments array becomes one separate `callExtension` argument, so a method
whose only parameter is an array needs it wrapped once: `["RemoveObjects", [_pairs], true]`. Passing
`_pairs` directly would spread the pairs over one argument each, and the extension would try to
deserialize the first pair as a list of pairs and fail ("Object cannot be stored in an array of this
type").

The same one-argument-per-parameter rule applies on the wire, and there it fails silently: remote
methods bind one wire argument to one parameter, so the batch must go out as a single argument —
`SendTcpMessageAsync(0, "RemoveObjects", [removed.ToArray()])`. Spreading `removed` instead makes the
receiver's `DynamicInvoke` see N arguments for a one-parameter method, the detached fire-and-forget
task throws `TargetParameterCountException`, and no other client ever hears about the deletion.
`SetInitialMissionAttributes` already follows the same pattern (`[attributeList]`).
`StopAttributesEdit` did not and lost every release that way — a method whose only parameter is an
array has to be wrapped exactly once, `SendTcpMessageAsync(-1, "StopAttributesEdit", [objectIds])`.

## Create message attributes

Every `fn_sendCreate*` function builds the receiver's attribute list. A normal creation only sends
what is needed to create the entity (`ItemClass`, `Position` and, when the entity is in a custom
layer, `EOEX_Layer`); every later edit is sent as an attribute update.

A **paste** can differ from the defaults in any attribute and cannot be detected attribute by
attribute, so `fn_handleCreateObject` marks pasted entities in `EOEX_var_PastedObjects` and passes
`_isCopy = true` (`_isCopy` is the last parameter of every `fn_sendCreate*`). The send functions then
start from the entity's full `get3DENAttributes ""` list, so a copied entity keeps init, name,
variable name, presence, loadout, pylons and module specific settings. Unset attributes are `nil` and
are dropped, since they cannot be serialized. Layers always send their whole attribute list.

`EOEX_IS_EMPTY` is only sent when a **vehicle really is empty** (no crew, and not a
unit/trigger/logic); receivers default to a crewed object, which is what Eden creates for
`isEmpty = false`. Do not send `false` explicitly — omit the attribute instead.

`EOEX_GROUP` is only sent for objects, and only when the leader has its own ID. `EOEX_Layer` carries
the custom layer ID and is only sent when the entity is in one — `""` means the root layer, which
is what receivers default to — because `Layer` is not an Eden attribute. `fn_updateObjectLayer`
sends the same key, so the layer change survives an `UpdateObject`.

Receivers consume `EOEX_Layer`, `EOEX_IS_EMPTY` and `EOEX_GROUP` first and then skip every remaining
`EOEX_*` key when applying the attributes, because those keys are not Eden attributes.

## Preventing the echo loop

Applying a received attribute raises `OnEntityAttributeChanged`, which `fn_sendObjectAttributes`
turns back into an `UpdateObject`. That is suppressed with `EOEX_var_SkipAttributeChanges`, a
per-ID list of `[property, value]` pairs.

Eden **normalizes** some values when it applies them: `set3DENAttribute ["Rotation", [0,0,202.886]]`
reads back as `202.885984375`, so the change event never matches the value that came over the wire.
`fn_receiveObjectCreate`, `fn_receiveObjectUpdate` and `fn_recreateEntity` therefore register the
**read-back** value (`get3DENAttribute` right after the successful set) in addition to the received
one. Without that, every received rotation is sent back, the other client applies and echoes it
again, and the two editors ping-pong forever. Keep the read-back when adding a new receive path
(`set3DENLayer`/`EOEX_Layer` excepted — its ID does not change).

Attribute values can contain quotes (e.g. `Init`, `ammoBox`), and Arma parses the callback data with
`parseSimpleArray`. `Serializer.PrintArray` therefore doubles quotes inside strings; keep that
escaping when adding values to a message sent to Arma.

The reverse direction has to undo the same escaping: `callExtension` hands the extension SQF's `str`
form, where a quote inside a string is written as `""`. `Serializer.NormalizeArmaJson` converts those
to JSON `\"` (and escapes `\`, newlines and control characters) before `JsonNode.Parse`. Without
that, any payload containing a quoted value — a copied vehicle's `ammoBox`, an `Init` with
`hint "..."` — fails to deserialize, and since a fire-and-forget request has no response channel the
message silently disappeared. `fn_callExtensionAsync` now logs the synchronous `["ERROR", [...]]`
result that the extension returns in that case.

A burst of callbacks is coalesced by the extension into one `BATCH` callback, and
`fn_initExtensionEvents` dispatches its entries synchronously and in order. Every entity of a paste
therefore passes through `fn_receiveObjectCreate` one after another, so never add an unconditional
`sleep` there: a 30 ms wait per entity turned a 100 entity composition into a 3 s long trickle. Wait
only when a dependency (layer, group leader, waypoint owner) is really missing — the messages are
ordered, so those are normally already applied.

## Grouping units

Eden groups a unit that is placed within 5 m of another unit. `create3DENEntity` always creates a
standalone group, so `fn_sendCreateObject` adds `["EOEX_GROUP", <leader id>]` (the ID of
`leader group _object`, skipped when it equals the object's own ID or when the leader is not a
synced object, e.g. vehicle crew). `fn_receiveObjectCreate` waits briefly for that leader and joins
the group with `add3DENConnection ["Group", [_entity], group _leader]`.

Groups themselves are still not synced (`handleCreateObject` ignores them), so only membership is
replicated. The join happens **before** the ID is registered, otherwise the resulting
`OnEntityParentChanged` event makes `fn_updateObjectLayer` send a layer update back to the server.

## Changing an entity class (`ItemClass`)

Eden silently ignores `set3DENAttribute ["ItemClass", ...]` (it returns `true` and keeps the old
class) for every entity type, including markers and waypoints. A received class change therefore
goes through [`EOEX_fnc_recreateEntity`](@EdenOnline/addons/functions/objects/helpers/fn_recreateEntity.sqf):
it stores the attributes, removes the registry ID, deletes the entity, creates it again with the
new class and re-applies every attribute. Supported for objects, triggers, logics, markers and
waypoints; comments and layers have no class.

A waypoint is addressed by `[group, index]` and Eden never reuses a freed index, so the recreated
waypoint gets a new index and the registry is updated to it. Its `Order` attribute must **not** be
replayed: it is the waypoint's slot inside the group, and applying it moves the waypoint there and
takes over the class stored in that slot (which silently turned a recreated `Hold` back into
`Move`). `fn_receiveObjectCreate` skips it for the same reason.

`EOEX_fnc_receiveObjectUpdate` triggers the recreation whenever the update payload contains an
`ItemClass` that differs from the current one.

On the **local** side Eden does the same thing itself when the user picks a new class: it removes
the entity and adds a replacement (new EOEX ID). `fn_handleDeleteObject` records the removal
(timestamp, type, position — the position of a vehicle is already gone at that point) and
`fn_handleCreateObject` treats an entity that appears within 0.5 s with the same type (and the same
position when both are known) as that replacement, passing `_isCopy = true` so the create carries
the full attribute list. Without it the other clients recreated a class-changed unit or vehicle
with default attributes and lost its `Init`, loadout and waypoint settings.

## C# extension

Hot reload is not supported. Rebuild with `Tools\BuildForArma.ps1` and restart Arma 3 (see
[`DEVSETUP.md`](DEVSETUP.md)).
