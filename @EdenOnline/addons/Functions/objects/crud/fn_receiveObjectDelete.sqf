// EOEX_fnc_receiveObjectDelete


params ["_id"];


// TODO handle all types of objects, not just 3DEN entities select 0
private _objects = ((all3DENEntities # 0) select { _x getVariable ["EOEX_var_objectID","-1"] == _id });
delete3DENEntities _objects;
