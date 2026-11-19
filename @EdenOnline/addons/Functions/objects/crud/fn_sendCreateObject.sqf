// EOEX_fnc_sendCreateObject

// This file handles types Object, Trigger and Logic
// ItemClass, might not exist, for all objects, but we force it in the attributes

// [["ItemClass","B_AFV_Wheeled_01_cannon_F"],["Name",""],["Init","asd"],["Pylons",""],["Position",[5559.25,1769.27,0]],["Rotation",[-0,0,0]],["Size3",[0,0,0]],["IsRectangle",false],["PlacementRadius",0],["ControlSP",false],["ControlMP",false],["Description",""],["Lock",1],["Skill",0.5],["Health",1],["Fuel",1],["Ammo",1],["Rank","PRIVATE"],["UnitPos",3],["DynamicSimulation",false],["AddToDynSimGrid",true],["EnableSimulation",true],["ObjectIsSimple",false],["IsLocalOnly",false],["allowDamage",true],["DoorStates",[0,0,0]],["EnableRevive",false],["hideObject",false],["enableStamina",true],["NameSound",""],["speaker",""],["pitch",-1],["unitName","Error: No unit"],["unitInsignia",""],["face",""],["Presence",1],["PresenceCondition","true"],["ammoBox","[[[[""arifle_MXC_F""],[1]],[[""SmokeShell"",""SmokeShellBlue"",""30Rnd_65x39_caseless_mag""],[2,2,4]],[[""FirstAidKit""],[10]],[[],[]]],false]"],["VehicleCustomization",[[],[]]],["ReportRemoteTargets",false],["ReceiveRemoteTargets",false],["ReportOwnPosition",false],["RadarUsageAI",0]]
params ["_id", "_object", "_type", "_layerId", ["_isCopy", false]];

// A pasted entity can differ from the defaults in any attribute (init, name, variable name,
// presence, loadout, pylons, module settings, ...), so its full attribute list is sent then.
// A normal creation only sends what the receiver needs to build the entity; everything else is
// later sent as an attribute update. Unset attributes are nil and cannot be sent.
private _attributeMap = if (_isCopy) then {
    createHashMapFromArray ((_object get3DENAttributes "") select { !isNil { _x # 1 } })
} else {
    createHashMap
};



// Force the values the receiver needs to create the entity at all.
_attributeMap set ["ItemClass", typeOf _object];

// Add Rotation if not default (depends if snapping is enabled or not.)
private _rotation = (_object get3DENAttribute "Rotation") select 0;
if (!isNil "_rotation" && {_rotation isNotEqualTo [0,0,0]}) then { _attributeMap set ["Rotation", _rotation] };

_attributeMap set ["Position", (_object get3DENAttribute "Position") select 0];

if (_layerId != "") then {
    _attributeMap set ["EOEX_Layer", _layerId];
};

// Only a vehicle without crew is "empty"; units, triggers and logics are never empty.
private _isEmpty = _type == "Object" && {!(_object isKindOf "Man")} && {crew _object isEqualTo []};

// Receivers default to a crewed object, so the flag is only needed for empty vehicles.
if (_isEmpty) then {
    _attributeMap set ["EOEX_IS_EMPTY", true];
};

// Eden groups a unit placed within 5 m of another unit into that unit's group. The receiving
// clients create standalone groups, so they need the group leader's ID to rebuild the grouping.
// Leaders that are not sent on their own (e.g. vehicle crew) have no ID and are skipped.
if (_type == "Object") then {
    private _leader = leader group _object;
    if (_leader isNotEqualTo _object) then {
        private _leaderId = [_leader, true] call EOEX_fnc_getId;

        if (!isNil "_leaderId" && {_leaderId != _id}) then {
            _attributeMap set ["EOEX_GROUP", _leaderId];
            sleep 0.01; // Make sure the leader gets sent first.
            // TODO Wait on reciever instead "EOEX_GROUP" is set and not found
        };
    };
};


// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, _type, _attributeMap], true] call EOEX_fnc_callExtensionAsync;
