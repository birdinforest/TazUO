fxc.exe /T fx_2_0 /O3 IsometricWorld.fx
fxc.exe /T fx_2_0 /O3 /Fo IsometricWorld.fxc IsometricWorld.fx
fxc.exe /T fx_2_0 /O3 /Fo xBR.fxc xBR.fx
fxc.exe /T fx_2_0 /O3 /Fo Puddle.fxc Puddle.fx

echo.
echo Shaders compiled. Rebuilding C# so FileEmbed picks up the new .fxc binaries...
echo (FileEmbed embeds shaders at compile time; updating .fxc alone is not enough.)
dotnet build ..\ClassicUO.Renderer\ClassicUO.Renderer.csproj -c Debug --no-incremental
dotnet build ..\ClassicUO.Client\ClassicUO.Client.csproj -c Debug --no-incremental

pause