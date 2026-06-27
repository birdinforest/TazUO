#!/bin/bash
#winetricks dxsdk_jun2010
wine fxc.exe /T fx_2_0 /O3 /Fo IsometricWorld.fxc IsometricWorld.fx
wine fxc.exe /T fx_2_0 /O3 /Fo xBR.fxc xBR.fx
wine fxc.exe /T fx_2_0 /O3 /Fo Puddle.fxc Puddle.fx

echo "Recompiled: IsometricWorld.fxc, xBR.fxc, Puddle.fxc (surface shimmer + edge ripple)."
echo "FileEmbed embeds at C# compile time — run: dotnet build --no-incremental"
