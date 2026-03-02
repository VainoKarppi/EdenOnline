
params ["_object"];

// ADD ID AND REGISTER TO LIST
_id = call EOE_fnc_getId;
_object setVariable ["EOE_var_objectID",_id];
EOE_var_objects pushBack _object;

// SYNC STATUS
// ADD EVENTS