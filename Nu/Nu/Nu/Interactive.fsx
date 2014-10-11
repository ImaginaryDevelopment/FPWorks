#nowarn "9"
#r "System.Configuration"

(* IMPORTANT NOTE: change these paths to make this script run with your Nu installation! *)
#r "../../../../FPWorks/Prime/xUnit/xunit.dll"
#r "../../../../FPWorks/Prime/FSharpx.Core/FSharpx.Core.dll"
#r "../../../../FPWorks/Prime/Prime/Prime/bin/Debug/Prime.exe"
#r "../../../../FPWorks/Nu/xUnit/xunit.dll"
#r "../../../../FPWorks/Nu/FSharpx.Core/FSharpx.Core.dll"
#r "../../../../FPWorks/Nu/Farseer/FarseerPhysics.dll"
#r "../../../../FPWorks/Nu/SDL2#/Debug/SDL2#.dll"
#r "../../../../FPWorks/Nu/TiledSharp/Debug/TiledSharp.dll"
System.IO.Directory.SetCurrentDirectory "../../../../FPWorks/Nu/Nu/Nu/bin/Debug"
#r "C:/Development/FPWorks/SDL2Addendum/SDL2Addendum/SDL2Addendum/bin/Debug/SDL2Addendum.dll"

#load "RQueue.fs"
#load "Address.fs"
#load "Core.fs"
#load "address.fs"
#load "Constants.fs"

#load "Math.fs"
#load "Xtension.fs"
#load "Overlayer.fs"
#load "Serialization.fs"
#load "Reflection.fs"
#load "Camera.fs"
#load "Assets.fs"
#load "Physics.fs"
#load "Rendering.fs"
#load "Audio.fs"
#load "Metadata.fs"
#load "Input.fs"
#load "Sdl.fs"
#load "Simulation.fs"
#load "Entity.fs"
#load "Group.fs"
#load "Screen.fs"
#load "Game.fs"
#load "Dispatchers.fs"
#load "World.fs"

open System
open SDL2
open OpenTK
open TiledSharp
open Nu
open Nu.Constants