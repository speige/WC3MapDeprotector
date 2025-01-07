_G.__jass2luaTranspiler__pcall_errors = ''
_G.__jass2luaTranspiler__safePreload = function(data)
	local maxChunkSize = 259
	local i = 1
	while i <= #data do
		local chunk = data:sub(i, i + maxChunkSize - 1)
		Preload(chunk)
		i = i + maxChunkSize
	end
end

(function()
	local success, err = pcall(function()
		local code, err = load([[
		{{PCALL_WRAPPER_GLOBAL_SCRIPT_CODE}}
		]], nil, "t", _G)		
		if (code) then
			local success2, err2 = pcall(code)
			if not success2 then
				__jass2luaTranspiler__pcall_errors = __jass2luaTranspiler__pcall_errors .. 'code parsing error: ' .. tostring(err2) .. '\n'
			end
		else
			__jass2luaTranspiler__pcall_errors = __jass2luaTranspiler__pcall_errors .. 'exception before config() called' .. tostring(err) .. '\n'
		end
	end)

	if not success then
		__jass2luaTranspiler__pcall_errors = __jass2luaTranspiler__pcall_errors .. 'exception setting up error handler' .. tostring(err) .. '\n'
	end
end)()

local __jass2luaTranspiler__pcall_config = _G.config
_G.config = function(...)
	local success, result = pcall(function(...)
		return __jass2luaTranspiler__pcall_config(...)
	end, ...)
	
	if success then
		return result
	else
		__jass2luaTranspiler__pcall_errors = __jass2luaTranspiler__pcall_errors .. 'config() exception' .. tostring(result) .. '\n'
		
		PreloadGenClear()
		__jass2luaTranspiler__safePreload(__jass2luaTranspiler__pcall_errors)
		PreloadGenStart()
		PreloadGenEnd("JASS2LUATRANPSILER_PCALL_WRAPPER_CONFIG_EXCEPTION.txt")
				
		SetPlayers(1)
		SetTeams(1)
		SetGamePlacement(MAP_PLACEMENT_USE_MAP_SETTINGS)
		DefineStartLocation(0, 1806.2, -1933.4)
		InitCustomPlayerSlots()
		SetPlayerSlotAvailable(Player(0), MAP_CONTROL_USER)
		InitGenericPlayerSlots()
	end
end

local __jass2luaTranspiler__pcall_main = _G.main
_G.main = function(...)
	local success, result = pcall(function(...)
		 return __jass2luaTranspiler__pcall_main(...)
	end, ...)
	
	if success then
		return result
	else
		__jass2luaTranspiler__pcall_errors = __jass2luaTranspiler__pcall_errors .. 'main() exception' .. tostring(result)
		SetCameraBounds(-3328.0 + GetCameraMargin(CAMERA_MARGIN_LEFT), -3584.0 + GetCameraMargin(CAMERA_MARGIN_BOTTOM), 3328.0 - GetCameraMargin(CAMERA_MARGIN_RIGHT), 3072.0 - GetCameraMargin(CAMERA_MARGIN_TOP), -3328.0 + GetCameraMargin(CAMERA_MARGIN_LEFT), 3072.0 - GetCameraMargin(CAMERA_MARGIN_TOP), 3328.0 - GetCameraMargin(CAMERA_MARGIN_RIGHT), -3584.0 + GetCameraMargin(CAMERA_MARGIN_BOTTOM))
		SetDayNightModels("Environment\\DNC\\DNCLordaeron\\DNCLordaeronTerrain\\DNCLordaeronTerrain.mdl", "Environment\\DNC\\DNCLordaeron\\DNCLordaeronUnit\\DNCLordaeronUnit.mdl")
		NewSoundEnvironment("Default")
		SetAmbientDaySound("LordaeronSummerDay")
		SetAmbientNightSound("LordaeronSummerNight")
		SetMapMusic("Music", true, 0)
		InitBlizzard()
		print(__jass2luaTranspiler__pcall_errors)
	end
end
