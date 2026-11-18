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

Dragging sends `StartObjectDrag` / `UpdateObjectDrag` / `EndObjectDrag`. Query the selection per type
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

## Create message attributes

`CreateObject` carries the entity's full attribute list from `get3DENAttributes ""`, so a created
entity keeps init, name, variable name, presence, loadout, pylons and module specific settings.
`fn_sendCreateObject` forces `ItemClass` (from `typeOf`), `Position` and `Layer` into that list and
adds the EOEX bookkeeping keys. Unset attributes are `nil` and are dropped, since they cannot be
serialized.

`EOEX_IS_EMPTY` is only sent when a **vehicle really is empty** (no crew, and not a
unit/trigger/logic); receivers default to a crewed object, which is what Eden creates for
`isEmpty = false`. Do not send `false` explicitly — omit the attribute instead.

`EOEX_GROUP` is only sent for objects, and only when the leader has its own ID. Receivers consume
`EOEX_IS_EMPTY` and `EOEX_GROUP` first and then skip every remaining `EOEX_*` key when applying the
attributes, because those keys are not Eden attributes.

Markers, comments and waypoints still send a short hand-built list.

Attribute values can contain quotes (e.g. `Init`, `ammoBox`), and Arma parses the callback data with
`parseSimpleArray`. `Serializer.PrintArray` therefore doubles quotes inside strings; keep that
escaping when adding values to a message sent to Arma.

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
class) for every entity type, including markers. A received class change therefore goes through
[`EOEX_fnc_recreateEntity`](@EdenOnline/addons/functions/objects/helpers/fn_recreateEntity.sqf):
it stores the attributes, removes the registry ID, deletes the entity, creates it again with the
new class and re-applies every attribute. Supported for objects, triggers, logics and markers;
comments and layers have no class and waypoints are referenced by `[group, index]` (Eden does not
reuse the freed index), so those still log an error.

`EOEX_fnc_receiveObjectUpdate` triggers the recreation whenever the update payload contains an
`ItemClass` that differs from the current one.

## C# extension

Hot reload is not supported. Rebuild with `Tools\BuildForArma.ps1` and restart Arma 3 (see
[`DEVSETUP.md`](DEVSETUP.md)).
