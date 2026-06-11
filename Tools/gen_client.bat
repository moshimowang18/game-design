set WORKSPACE=..
set GEN_CLIENT=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=%WORKSPACE%\DataTables

dotnet %GEN_CLIENT% ^
    -t client ^
    -c cs-simple-json ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%WORKSPACE%\Assets\Res\Scripts\LubanCode ^
    -x outputDataDir=%WORKSPACE%\Assets\Res\LubanData
pause