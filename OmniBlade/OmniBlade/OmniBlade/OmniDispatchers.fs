﻿namespace OmniBlade
open OpenTK
open SDL2
open Prime
open Nu
open Nu.Constants
open Nu.WorldConstants
open OmniBlade
open OmniBlade.OmniConstants

[<AutoOpen>]
module OmniDispatchersModule =

    let [<Literal>] KeyboardMovementForce = 400.0f

    type FieldGroupDispatcher () =
        inherit GroupDispatcher ()

        let adjustFieldCamera groupAddress world =
            let characterAddress = gatoea groupAddress FieldCharacterName
            let character = World.getEntity characterAddress world
            let camera = { world.Camera with EyeCenter = character.Position + character.Size * 0.5f }
            World.setCamera camera world

        let handleAdjustFieldCamera event world =
            let address = World.unwrapA event world
            (Cascade, adjustFieldCamera address world)

        let handleMoveFieldCharacter event world =
            let address = World.unwrapA event world
            let characterAddress = gatoea address FieldCharacterName
            let feelerAddress = gatoea address FieldFeelerName
            let character = World.getEntity characterAddress world
            let feeler = World.getEntity feelerAddress world
            if feeler.Touched then
                let mousePosition = World.getMousePositionF world
                let mousePositionWorld = Camera.mouseToWorld character.ViewType mousePosition world.Camera
                let characterCenter = character.Position + character.Size * 0.5f
                let impulseVector = (mousePositionWorld - characterCenter) * 5.0f
                let world = World.applyBodyLinearImpulse impulseVector character.PhysicsId world 
                (Cascade, world)
            else
                let impulses =
                    [(if World.isKeyboardKeyDown (int SDL.SDL_Scancode.SDL_SCANCODE_LEFT) world then Vector2 (-KeyboardMovementForce, 0.0f) else Vector2.Zero)
                     (if World.isKeyboardKeyDown (int SDL.SDL_Scancode.SDL_SCANCODE_RIGHT) world then Vector2 (KeyboardMovementForce, 0.0f) else Vector2.Zero)
                     (if World.isKeyboardKeyDown (int SDL.SDL_Scancode.SDL_SCANCODE_UP) world then Vector2 (0.0f, KeyboardMovementForce) else Vector2.Zero)
                     (if World.isKeyboardKeyDown (int SDL.SDL_Scancode.SDL_SCANCODE_DOWN) world then Vector2 (0.0f, -KeyboardMovementForce) else Vector2.Zero)]
                let impulse = List.reduce add impulses
                let world = World.applyBodyLinearImpulse impulse character.PhysicsId world 
                (Cascade, world)

        override dispatcher.Register (address, character, world) =
            let world = World.monitor handleMoveFieldCharacter TickEventAddress address world
            let world = World.monitor handleAdjustFieldCamera TickEventAddress address world
            let world = World.addPhysicsMessage (SetGravityMessage Vector2.Zero) world
            let world = adjustFieldCamera address world
            (character, world)

    type BattleGroupDispatcher () =
        inherit GroupDispatcher ()

        override dispatcher.Register (_, group, world) =
            let world = World.addPhysicsMessage (SetGravityMessage Vector2.Zero) world
            (group, world)

    type OmniBladeDispatcher () =
        inherit GameDispatcher ()