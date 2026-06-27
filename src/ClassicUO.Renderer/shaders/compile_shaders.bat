fxc.exe /T fx_2_0 /O3 /Fo IsometricWorld.fxc IsometricWorld.fx
fxc.exe /T fx_2_0 /O3 /Fo xBR.fxc xBR.fx
fxc.exe /T fx_2_0 /O3 /Fo Puddle.fxc Puddle.fx

echo.
echo Recompiled: IsometricWorld.fxc, xBR.fxc, Puddle.fxc (surface shimmer + edge ripple).
echo FileEmbed embeds at C# compile time — run dotnet build --no-incremental after this.
dotnet build ..\ClassicUO.Renderer\ClassicUO.Renderer.csproj -c Debug --no-incremental
dotnet build ..\ClassicUO.Client\ClassicUO.Client.csproj -c Debug --no-incremental

pause