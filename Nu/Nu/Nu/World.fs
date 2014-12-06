﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2014.

namespace Nu
open System
open System.IO
open System.Collections.Generic
open System.ComponentModel
open System.Reflection
open System.Xml
open System.Xml.Serialization
open SDL2
open OpenTK
open TiledSharp
open Prime
open Nu
open Nu.Constants
open Nu.WorldConstants

[<AutoOpen>]
module WorldModule =

    let private ScreenTransitionMouseLeftKey = World.makeSubscriptionKey ()
    let private ScreenTransitionMouseCenterKey = World.makeSubscriptionKey ()
    let private ScreenTransitionMouseRightKey = World.makeSubscriptionKey ()
    let private ScreenTransitionMouseX1Key = World.makeSubscriptionKey ()
    let private ScreenTransitionMouseX2Key = World.makeSubscriptionKey ()
    let private ScreenTransitionKeyboardKeyKey = World.makeSubscriptionKey ()
    let private SplashScreenTickKey = World.makeSubscriptionKey ()
    let private LoadedAssemblies = Dictionary<string, Assembly> ()

    type World with

        static member getSimulantDefinition (address : Simulant Address) world =
            match address.Names with
            | [] -> Game <| world.Game
            | [_] -> Screen <| World.getScreen (atosa address) world
            | [_; _] -> Group <| World.getGroup (atoga address) world
            | [_; _; _] -> Entity <| World.getEntity (atoea address) world
            | _ -> failwith <| "Invalid simulant address '" + acstring address + "'."

        static member getOptSimulantDefinition address world =
            match address.Names with
            | [] -> Some <| Game world.Game
            | [_] -> Option.map Screen <| World.getOptScreen (atosa address) world
            | [_; _] -> Option.map Group <| World.getOptGroup (atoga address) world
            | [_; _; _] -> Option.map Entity <| World.getOptEntity (atoea address) world
            | _ -> failwith <| "Invalid simulant address '" + acstring address + "'."

        static member tryGetIsSelectedScreenIdling world =
            match World.getOptSelectedScreen world with
            | Some selectedScreen -> Some <| Screen.isIdling selectedScreen
            | None -> None

        static member isSelectedScreenIdling world =
            match World.tryGetIsSelectedScreenIdling world with
            | Some answer -> answer
            | None -> failwith <| "Cannot query state of non-existent selected screen."

        static member tryGetIsSelectedScreenTransitioning world =
            Option.map not <| World.tryGetIsSelectedScreenIdling world

        static member isSelectedScreenTransitioning world =
            not <| World.isSelectedScreenIdling world

        static member private setScreenState state address screen world =
            let screen = Screen.setScreenState state screen
            let world =
                match state with
                | IdlingState ->
                    world |>
                        World.unsubscribe ScreenTransitionMouseLeftKey |>
                        World.unsubscribe ScreenTransitionMouseCenterKey |>
                        World.unsubscribe ScreenTransitionMouseRightKey |>
                        World.unsubscribe ScreenTransitionMouseX1Key |>
                        World.unsubscribe ScreenTransitionMouseX2Key |>
                        World.unsubscribe ScreenTransitionKeyboardKeyKey
                | IncomingState | OutgoingState ->
                    world |>
                        World.subscribe ScreenTransitionMouseLeftKey World.handleAsSwallow (MouseLeftEventAddress ->- AnyEventAddress) GameAddress |>
                        World.subscribe ScreenTransitionMouseCenterKey World.handleAsSwallow (MouseCenterEventAddress ->- AnyEventAddress) GameAddress |>
                        World.subscribe ScreenTransitionMouseRightKey World.handleAsSwallow (MouseRightEventAddress ->- AnyEventAddress) GameAddress |>
                        World.subscribe ScreenTransitionMouseX1Key World.handleAsSwallow (MouseX1EventAddress ->- AnyEventAddress) GameAddress |>
                        World.subscribe ScreenTransitionMouseX2Key World.handleAsSwallow (MouseX2EventAddress ->- AnyEventAddress) GameAddress |>
                        World.subscribe ScreenTransitionKeyboardKeyKey World.handleAsSwallow (KeyboardKeyEventAddress ->- AnyEventAddress) GameAddress 
            let world = World.setScreen address screen world
            (screen, world)

        static member selectScreen screenAddress screen world =
            let world =
                match World.getOptSelectedScreenAddress world with
                | Some selectedScreenAddress ->  World.publish4 () (DeselectEventAddress ->>- selectedScreenAddress) selectedScreenAddress world
                | None -> world
            let (screen, world) = World.setScreenState IncomingState screenAddress screen world
            let world = World.setOptSelectedScreenAddress (Some screenAddress) world
            let world = World.publish4 () (SelectEventAddress ->>- screenAddress) screenAddress world
            (screen, world)

        static member tryTransitionScreen destinationAddress destinationScreen world =
            match World.getOptSelectedScreenAddress world with
            | Some selectedScreenAddress ->
                match World.getOptScreen selectedScreenAddress world with
                | Some selectedScreen ->
                    let subscriptionKey = World.makeSubscriptionKey ()
                    let subscription = fun (_ : unit Event) world ->
                        match world.State.OptScreenTransitionDestinationAddress with
                        | Some address ->
                            let world = World.unsubscribe subscriptionKey world
                            let world = World.setOptScreenTransitionDestinationAddress None world
                            let world = snd <| World.selectScreen address destinationScreen world
                            (Cascade, world)
                        | None -> failwith "No valid OptScreenTransitionDestinationAddress during screen transition!"
                    let world = World.setOptScreenTransitionDestinationAddress (Some destinationAddress) world
                    let world = snd <| World.setScreenState OutgoingState selectedScreenAddress selectedScreen world
                    let world = World.subscribe<unit, Screen> subscriptionKey subscription (OutgoingFinishEventAddress ->>- selectedScreenAddress) selectedScreenAddress world
                    Some world
                | None -> None
            | None -> None

        // TODO: replace this with more sophisticated use of handleAsScreenTransition4, and so on for its brethren.
        static member private handleAsScreenTransitionFromSplash4<'d> eventHandling destinationAddress (_ : 'd Event) world =
            let destinationScreen = World.getScreen destinationAddress world
            let world = snd <| World.selectScreen destinationAddress destinationScreen world
            (eventHandling, world)

        static member handleAsScreenTransitionFromSplash<'d> destinationAddress event world =
            World.handleAsScreenTransitionFromSplash4<'d> Cascade destinationAddress event world

        static member handleAsScreenTransitionFromSplashBy<'d> by destinationAddress event  (world : World) =
            let (eventHandling, world) = by event world
            World.handleAsScreenTransitionFromSplash4<'d> eventHandling destinationAddress event world

        static member private handleAsScreenTransition4<'d> eventHandling destinationAddress (_ : 'd Event) world =
            let destinationScreen = World.getScreen destinationAddress world
            match World.tryTransitionScreen destinationAddress destinationScreen world with
            | Some world -> (eventHandling, world)
            | None ->
                trace <| "Program Error: Invalid screen transition for destination address '" + acstring destinationAddress + "'."
                (eventHandling, world)

        static member handleAsScreenTransition<'d> destinationAddress event world =
            World.handleAsScreenTransition4<'d> Cascade destinationAddress event world

        static member handleAsScreenTransitionBy<'d> by destinationAddress event (world : World) =
            let (eventHandling, world) = by event world
            World.handleAsScreenTransition4<'d> eventHandling destinationAddress event world

        static member private updateScreenTransition1 screen transition =
            if screen.TransitionTicksNp = transition.TransitionLifetime then (true, { screen with TransitionTicksNp = 0L })
            else (false, { screen with TransitionTicksNp = screen.TransitionTicksNp + 1L })

        // TODO: split this function up...
        static member private updateScreenTransition world =
            match World.getOptSelectedScreenAddress world with
            | Some selectedScreenAddress ->
                let selectedScreen = World.getScreen selectedScreenAddress world
                match selectedScreen.ScreenStateNp with
                | IncomingState ->
                    match world.State.Liveness with
                    | Running ->
                        let world =
                            if selectedScreen.TransitionTicksNp = 0L
                            then World.publish4 () (IncomingStartEventAddress ->>- selectedScreenAddress) selectedScreenAddress world
                            else world
                        match world.State.Liveness with
                        | Running ->
                            let (finished, selectedScreen) = World.updateScreenTransition1 selectedScreen selectedScreen.Incoming
                            let world = World.setScreen selectedScreenAddress selectedScreen world
                            if finished then
                                let world = snd <| World.setScreenState IdlingState selectedScreenAddress selectedScreen world
                                World.publish4 () (IncomingFinishEventAddress ->>- selectedScreenAddress) selectedScreenAddress world
                            else world
                        | Exiting -> world
                    | Exiting -> world
                | OutgoingState ->
                    let world =
                        if selectedScreen.TransitionTicksNp <> 0L then world
                        else World.publish4 () (OutgoingStartEventAddress ->>- selectedScreenAddress) selectedScreenAddress world
                    match world.State.Liveness with
                    | Running ->
                        let (finished, selectedScreen) = World.updateScreenTransition1 selectedScreen selectedScreen.Outgoing
                        let world = World.setScreen selectedScreenAddress selectedScreen world
                        if finished then
                            let world = snd <| World.setScreenState IdlingState selectedScreenAddress selectedScreen world
                            match world.State.Liveness with
                            | Running -> World.publish4 () (OutgoingFinishEventAddress ->>- selectedScreenAddress) selectedScreenAddress world
                            | Exiting -> world
                        else world
                    | Exiting -> world
                | IdlingState -> world
            | None -> world

        static member private handleSplashScreenIdleTick idlingTime ticks event world =
            let world = World.unsubscribe SplashScreenTickKey world
            if ticks < idlingTime then
                let subscription = World.handleSplashScreenIdleTick idlingTime (inc ticks)
                let world = World.subscribe SplashScreenTickKey subscription event.EventAddress event.SubscriberAddress world
                (Cascade, world)
            else
                match World.getOptSelectedScreenAddress world with
                | Some selectedScreenAddress ->
                    match World.getOptScreen selectedScreenAddress world with
                    | Some selectedScreen ->
                        let world = snd <| World.setScreenState OutgoingState selectedScreenAddress selectedScreen world
                        (Cascade, world)
                    | None ->
                        trace "Program Error: Could not handle splash screen tick due to no selected screen."
                        (Resolve, World.exit world)
                | None ->
                    trace "Program Error: Could not handle splash screen tick due to no selected screen."
                    (Resolve, World.exit world)

        static member internal handleSplashScreenIdle idlingTime event world =
            let world = World.subscribe SplashScreenTickKey (World.handleSplashScreenIdleTick idlingTime 0L) TickEventAddress event.SubscriberAddress world
            (Resolve, world)

        static member addSplashScreen persistent splashData dispatcherName address destination world =
            let splashScreen = { World.makeDissolveScreen splashData.DissolveData dispatcherName (Some <| Address.head address) world with Persistent = persistent }
            let splashGroup = { World.makeGroup typeof<GroupDispatcher>.Name (Some "SplashGroup") world with Persistent = persistent }
            let splashLabel = { World.makeEntity typeof<LabelDispatcher>.Name (Some "SplashLabel") world with Persistent = persistent }
            let splashLabel = Entity.setSize world.Camera.EyeSize splashLabel
            let splashLabel = Entity.setPosition (-world.Camera.EyeSize * 0.5f) splashLabel
            let splashLabel = Entity.setLabelImage splashData.SplashImage splashLabel
            let splashGroupHierarchies = Map.singleton splashGroup.Name (splashGroup, Map.singleton splashLabel.Name splashLabel)
            let splashScreenHierarchy = (splashScreen, splashGroupHierarchies)
            let world = snd <| World.addScreen address splashScreenHierarchy world
            let world = World.monitor (World.handleSplashScreenIdle splashData.IdlingTime) (IncomingFinishEventAddress ->>- address) address world
            let world = World.monitor (World.handleAsScreenTransitionFromSplash destination) (OutgoingFinishEventAddress ->>- address) address world
            (splashScreen, world)

        static member addDissolveScreen persistent dissolveData dispatcherName address world =
            let dissolveScreen = { World.makeDissolveScreen dissolveData dispatcherName (Some <| Address.head address) world with Persistent = persistent }
            let dissolveScreenHierarchy = (dissolveScreen, Map.empty)
            World.addScreen address dissolveScreenHierarchy world

        static member addDissolveScreenFromGroupFile persistent dissolveData dispatcherName address groupFilePath world =
            let dissolveScreen = { World.makeDissolveScreen dissolveData dispatcherName (Some <| Address.head address) world with Persistent = persistent }
            let (group, entities) = World.readGroupHierarchyFromFile groupFilePath world
            let dissolveGroupHierarchies = Map.singleton group.Name (group, entities)
            let dissolveScreenHierarchy = (dissolveScreen, dissolveGroupHierarchies)
            World.addScreen address dissolveScreenHierarchy world

        static member private createIntrinsicOverlays entityDispatchers facets =
            let hasFacetNamesField = fun sourceType -> sourceType = typeof<EntityDispatcher>
            let entityDispatchers = Map.toValueListBy objectify entityDispatchers
            let facets = Map.toValueListBy (fun facet -> facet :> obj) facets
            let sources = facets @ entityDispatchers
            let sourceTypes = List.map (fun source -> source.GetType ()) sources
            Reflection.createIntrinsicOverlays hasFacetNamesField sourceTypes

        static member tryReloadOverlays inputDirectory outputDirectory world =
            
            // try to reload overlay file
            let inputOverlayFilePath = Path.Combine (inputDirectory, world.State.OverlayFilePath)
            let outputOverlayFilePath = Path.Combine (outputDirectory, world.State.OverlayFilePath)
            try File.Copy (inputOverlayFilePath, outputOverlayFilePath, true)

                // cache old overlayer and make new one
                let oldOverlayer = world.Subsystems.Overlayer
                let intrinsicOverlays = World.createIntrinsicOverlays world.Components.EntityDispatchers world.Components.Facets
                let overlayer = Overlayer.make outputOverlayFilePath intrinsicOverlays
                let world = World.setOverlayer overlayer world

                // get all the entities in the world
                let entities =
                    [for screenKvp in world.Entities do
                        for groupKvp in screenKvp.Value do
                            for entityKvp in groupKvp.Value do
                                let address = Address<Entity>.make [screenKvp.Key; groupKvp.Key; entityKvp.Key]
                                yield (address, entityKvp.Value)]

                // apply overlays to all entities
                let world =
                    Seq.fold
                        (fun world (address, entity : Entity) ->
                            let entity = { entity with Id = entity.Id } // hacky copy
                            match entity.OptOverlayName with
                            | Some overlayName ->
                                let oldFacetNames = entity.FacetNames
                                Overlayer.applyOverlayToFacetNames overlayName overlayName entity oldOverlayer world.Subsystems.Overlayer
                                match World.trySynchronizeFacets oldFacetNames (Some address) entity world with
                                | Right (entity, world) ->
                                    let facetNames = Entity.getFacetNames entity
                                    Overlayer.applyOverlay6 overlayName overlayName facetNames entity oldOverlayer world.Subsystems.Overlayer
                                    World.setEntity address entity world
                                | Left error -> note <| "There was an issue in applying a reloaded overlay: " + error; world
                            | None -> world)
                        world
                        entities

                // right!
                Right world

            // propagate error
            with exn -> Left <| acstring exn

        static member tryReloadAssets inputDirectory outputDirectory refinementDirectory world =
            
            // try to reload asset graph file
            try File.Copy (
                    Path.Combine (inputDirectory, world.State.AssetGraphFilePath),
                    Path.Combine (outputDirectory, world.State.AssetGraphFilePath), true)

                // reload asset graph
                match Assets.tryBuildAssetGraph inputDirectory outputDirectory refinementDirectory false world.State.AssetGraphFilePath with
                | Right () ->

                    // reload asset metadata
                    match Metadata.tryGenerateAssetMetadataMap world.State.AssetGraphFilePath with
                    | Right assetMetadataMap ->
                    
                        // reload assets
                        let world = World.setAssetMetadataMap assetMetadataMap world
                        let world = World.reloadRenderAssets world
                        let world = World.reloadAudioAssets world
                        Right world
            
                    // propagate errors
                    | Left errorMsg -> Left errorMsg
                | Left error -> Left error
            with exn -> Left <| acstring exn

        static member continueHack groupAddress world =
            // NOTE: since messages may be invalid upon continuing a world (especially physics
            // messages), all messages are eliminated. If this poses an issue, the editor will have
            // to instead store past / future worlds only once their current frame has been
            // processed (integrated, advanced, rendered, played, et al).
            let world = World.clearRenderMessages world
            let world = World.clearAudioMessages world
            let world = World.clearPhysicsMessages world
            let world = World.addPhysicsMessage RebuildPhysicsHackMessage world
            let entityMap = World.getEntityMap groupAddress world
            Map.fold
                (fun world _ (entity : Entity) ->
                    let entityAddress = gatoea groupAddress entity.Name
                    Entity.propagatePhysics entityAddress entity world)
                world
                entityMap

        static member private play world =
            let audioMessages = world.MessageQueues.AudioMessages
            let world = World.clearAudioMessages world
            let audioPlayer = world.Subsystems.AudioPlayer.Play audioMessages 
            World.setAudioPlayer audioPlayer world

        static member private getGroupRenderDescriptors world entities =
            Map.toValueListBy
                (fun entity -> Entity.getRenderDescriptors entity world)
                entities

        static member private getScreenTransitionRenderDescriptors camera screen transition =
            match transition.OptDissolveImage with
            | Some dissolveImage ->
                let progress = single screen.TransitionTicksNp / single transition.TransitionLifetime
                let alpha = match transition.TransitionType with Incoming -> 1.0f - progress | Outgoing -> progress
                let color = Vector4 (Vector3.One, alpha)
                [LayerableDescriptor
                    { Depth = Single.MaxValue
                      LayeredDescriptor =
                        SpriteDescriptor
                            { Position = -camera.EyeSize * 0.5f // negation for right-handedness
                              Size = camera.EyeSize
                              Rotation = 0.0f
                              ViewType = Absolute
                              OptInset = None
                              Image = dissolveImage
                              Color = color }}]
            | None -> []

        static member private getRenderDescriptors world =
            match World.getOptSelectedScreenAddress world with
            | Some selectedScreenAddress ->
                let optGroupMap = Map.tryFind (Address.head selectedScreenAddress) world.Entities
                match optGroupMap with
                | Some groupMap ->
                    let groupValues = Map.toValueList groupMap
                    let entityMaps = List.fold List.flipCons [] groupValues
                    let descriptors = List.map (World.getGroupRenderDescriptors world) entityMaps
                    let descriptors = List.concat <| List.concat descriptors
                    let selectedScreen = World.getScreen selectedScreenAddress world
                    match selectedScreen.ScreenStateNp with
                    | IncomingState -> descriptors @ World.getScreenTransitionRenderDescriptors world.Camera selectedScreen selectedScreen.Incoming
                    | OutgoingState -> descriptors @ World.getScreenTransitionRenderDescriptors world.Camera selectedScreen selectedScreen.Outgoing
                    | IdlingState -> descriptors
                | None -> []
            | None -> []

        static member private render world =
            let renderDescriptors = World.getRenderDescriptors world
            let renderingMessages = world.MessageQueues.RenderMessages
            let world = World.clearRenderMessages world
            let renderer = world.Subsystems.Renderer.Render (world.Camera, renderingMessages, renderDescriptors)
            World.setRenderer renderer world

        static member private handleIntegrationMessage world integrationMessage =
            match world.State.Liveness with
            | Running ->
                match integrationMessage with
                | BodyTransformMessage bodyTransformMessage ->
                    match World.getOptEntity (atoea bodyTransformMessage.SourceAddress) world with
                    | Some entity -> snd <| World.handleBodyTransformMessage bodyTransformMessage (atoea bodyTransformMessage.SourceAddress) entity world
                    | None -> world
                | BodyCollisionMessage bodyCollisionMessage ->
                    match World.getOptEntity (atoea bodyCollisionMessage.SourceAddress) world with
                    | Some _ ->
                        let collisionAddress = CollisionEventAddress ->- bodyCollisionMessage.SourceAddress
                        let collisionData =
                            { Normal = bodyCollisionMessage.Normal
                              Speed = bodyCollisionMessage.Speed
                              Collidee = (atoea bodyCollisionMessage.Source2Address) }
                        World.publish4 collisionData collisionAddress GameAddress world
                    | None -> world
            | Exiting -> world

        static member private handleIntegrationMessages integrationMessages world =
            List.fold World.handleIntegrationMessage world integrationMessages

        static member private integrate world =
            if World.isPhysicsRunning world then
                let physicsMessages = world.MessageQueues.PhysicsMessages
                let world = World.clearPhysicsMessages world
                let integrationMessages = world.Subsystems.Integrator.Integrate physicsMessages
                World.handleIntegrationMessages integrationMessages world
            else world

        static member private processTask (tasksNotRun, world) task =
            if task.ScheduledTime < world.State.TickTime then
                debug <| "Task leak found for time '" + acstring world.State.TickTime + "'."
                (tasksNotRun, world)
            elif task.ScheduledTime = world.State.TickTime then
                let world = task.Operation world
                (tasksNotRun, world)
            else (task :: tasksNotRun, world)

        static member private processTasks world =
            let tasks = List.rev world.Callbacks.Tasks
            let world = World.clearTasks world
            let (tasksNotRun, world) = List.fold World.processTask ([], world) tasks
            let tasksNotRun = List.rev tasksNotRun
            World.restoreTasks tasksNotRun world

        static member processInput (event : SDL.SDL_Event) world =
            let world =
                match event.``type`` with
                | SDL.SDL_EventType.SDL_QUIT ->
                    World.exit world
                | SDL.SDL_EventType.SDL_MOUSEMOTION ->
                    let mousePosition = Vector2 (single event.button.x, single event.button.y)
                    let world =
                        if World.isMouseButtonDown MouseLeft world
                        then World.publish World.sortSubscriptionsByPickingPriority { MouseMoveData.Position = mousePosition } MouseDragEventAddress GameAddress world
                        else world
                    World.publish World.sortSubscriptionsByPickingPriority { MouseMoveData.Position = mousePosition } MouseMoveEventAddress GameAddress world
                | SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN ->
                    let mousePosition = World.getMousePositionF world
                    let mouseButton = World.toNuMouseButton <| uint32 event.button.button
                    let mouseButtonEventAddress = ltoa [MouseButton.toEventName mouseButton]
                    let mouseButtonDownEventAddress = MouseEventAddress -<- mouseButtonEventAddress -<- ltoa<MouseButtonData> ["Down"]
                    let mouseButtonChangeEventAddress = MouseEventAddress -<- mouseButtonEventAddress -<- ltoa<MouseButtonData> ["Change"]
                    let eventData = { Position = mousePosition; Button = mouseButton; Down = true }
                    let world = World.publish World.sortSubscriptionsByPickingPriority eventData mouseButtonDownEventAddress GameAddress world
                    World.publish World.sortSubscriptionsByPickingPriority eventData mouseButtonChangeEventAddress GameAddress world
                | SDL.SDL_EventType.SDL_MOUSEBUTTONUP ->
                    let mousePosition = World.getMousePositionF world
                    let mouseButton = World.toNuMouseButton <| uint32 event.button.button
                    let mouseButtonEventAddress = ltoa [MouseButton.toEventName mouseButton]
                    let mouseButtonUpEventAddress = MouseEventAddress -<- mouseButtonEventAddress -<- ltoa<MouseButtonData> ["Up"]
                    let mouseButtonChangeEventAddress = MouseEventAddress -<- mouseButtonEventAddress -<- ltoa<MouseButtonData> ["Change"]
                    let eventData = { Position = mousePosition; Button = mouseButton; Down = false }
                    let world = World.publish World.sortSubscriptionsByPickingPriority eventData mouseButtonUpEventAddress GameAddress world
                    World.publish World.sortSubscriptionsByPickingPriority eventData mouseButtonChangeEventAddress GameAddress world
                | SDL.SDL_EventType.SDL_KEYDOWN ->
                    let keyboard = event.key
                    let key = keyboard.keysym
                    let eventData = { ScanCode = int key.scancode; Repeated = keyboard.repeat <> byte 0; Down = true }
                    let world = World.publish World.sortSubscriptionsByHierarchy eventData KeyboardKeyDownEventAddress GameAddress world
                    World.publish World.sortSubscriptionsByHierarchy eventData KeyboardKeyChangeEventAddress GameAddress world
                | SDL.SDL_EventType.SDL_KEYUP ->
                    let keyboard = event.key
                    let key = keyboard.keysym
                    let eventData = { ScanCode = int key.scancode; Repeated = keyboard.repeat <> byte 0; Down = false }
                    let world = World.publish World.sortSubscriptionsByHierarchy eventData KeyboardKeyUpEventAddress GameAddress world
                    World.publish World.sortSubscriptionsByHierarchy eventData KeyboardKeyChangeEventAddress GameAddress world
                | _ -> world
            (world.State.Liveness, world)

        static member processUpdate handleUpdate world =
            let world = handleUpdate world
            match world.State.Liveness with
            | Running ->
                let world = World.updateScreenTransition world
                match world.State.Liveness with
                | Running ->
                    let world = World.integrate world
                    match world.State.Liveness with
                    | Running ->
                        let world = World.publish4 () TickEventAddress GameAddress world
                        match world.State.Liveness with
                        | Running ->
                            let world = World.processTasks world
                            (world.State.Liveness, world)
                        | Exiting -> (Exiting, world)
                    | Exiting -> (Exiting, world)
                | Exiting -> (Exiting, world)
            | Exiting -> (Exiting, world)

        static member processRender handleRender world =
            let world = World.render world
            handleRender world

        static member processPlay world =
            let world = World.play world
            World.incrementTickTime world

        static member exitRender world =
            let renderer = world.Subsystems.Renderer.HandleRenderExit () 
            World.setRenderer renderer world

        static member run4 tryMakeWorld handleUpdate handleRender sdlConfig =
            Sdl.run
                tryMakeWorld
                World.processInput
                (World.processUpdate handleUpdate)
                (World.processRender handleRender)
                World.processPlay
                World.exitRender
                sdlConfig

        static member run tryMakeWorld handleUpdate sdlConfig =
            World.run4 tryMakeWorld handleUpdate id sdlConfig

        static member private pairWithName source =
            (Reflection.getTypeName source, source)

        static member private pairWithNames sources =
            Map.ofListBy World.pairWithName sources

        static member tryMake
            farseerCautionMode
            useLoadedGameDispatcher
            interactivity
            userState
            (nuPlugin : NuPlugin)
            sdlDeps =

            // attempt to generate asset metadata so the rest of the world can be created
            match Metadata.tryGenerateAssetMetadataMap AssetGraphFilePath with
            | Right assetMetadataMap ->

                // make user-defined values
                let userFacets = World.pairWithNames <| nuPlugin.MakeFacets ()
                let userEntityDispatchers = World.pairWithNames <| nuPlugin.MakeEntityDispatchers ()
                let userGroupDispatchers = World.pairWithNames <| nuPlugin.MakeGroupDispatchers ()
                let userScreenDispatchers = World.pairWithNames <| nuPlugin.MakeScreenDispatchers ()
                let userOptGameDispatcher = nuPlugin.MakeOptGameDispatcher ()
                let userOverlayRoutes = nuPlugin.MakeOverlayRoutes ()

                // infer the active game dispatcher
                let defaultGameDispatcher = GameDispatcher ()
                let activeGameDispatcher =
                    if useLoadedGameDispatcher then
                        match userOptGameDispatcher with
                        | Some gameDispatcher -> gameDispatcher
                        | None -> defaultGameDispatcher
                    else defaultGameDispatcher

                // make facets
                let defaultFacets =
                    Map.ofList
                        [typeof<RigidBodyFacet>.Name, RigidBodyFacet () :> Facet
                         typeof<SpriteFacet>.Name, SpriteFacet () :> Facet
                         typeof<AnimatedSpriteFacet>.Name, AnimatedSpriteFacet () :> Facet]
                let facets = Map.addMany (Map.toSeq userFacets) defaultFacets

                // make entity dispatchers
                // TODO: see if we can reflectively generate these
                let defaultEntityDispatcherList =
                    [EntityDispatcher ()
                     GuiDispatcher () :> EntityDispatcher
                     ButtonDispatcher () :> EntityDispatcher
                     LabelDispatcher () :> EntityDispatcher
                     TextDispatcher () :> EntityDispatcher
                     ToggleDispatcher () :> EntityDispatcher
                     FeelerDispatcher () :> EntityDispatcher
                     FillBarDispatcher () :> EntityDispatcher
                     BlockDispatcher () :> EntityDispatcher
                     BoxDispatcher () :> EntityDispatcher
                     TopViewCharacterDispatcher () :> EntityDispatcher
                     SideViewCharacterDispatcher () :> EntityDispatcher
                     TileMapDispatcher () :> EntityDispatcher]
                let defaultEntityDispatchers = World.pairWithNames defaultEntityDispatcherList
                let entityDispatchers = Map.addMany (Map.toSeq userEntityDispatchers) defaultEntityDispatchers

                // make group dispatchers
                let defaultGroupDispatchers = Map.ofList [World.pairWithName <| GroupDispatcher ()]
                let groupDispatchers = Map.addMany (Map.toSeq userGroupDispatchers) defaultGroupDispatchers

                // make screen dispatchers
                let defaultScreenDispatchers = Map.ofList [World.pairWithName <| ScreenDispatcher ()]
                let screenDispatchers = Map.addMany (Map.toSeq userScreenDispatchers) defaultScreenDispatchers

                // make game dispatchers
                let defaultGameDispatchers = Map.ofList [World.pairWithName <| defaultGameDispatcher]
                let gameDispatchers = 
                    match userOptGameDispatcher with
                    | Some gameDispatcher ->
                        let (gameDispatcherName, gameDispatcher) = World.pairWithName gameDispatcher
                        Map.add gameDispatcherName gameDispatcher defaultGameDispatchers
                    | None -> defaultGameDispatchers

                // make intrinsic overlays
                let intrinsicOverlays = World.createIntrinsicOverlays entityDispatchers facets

                // make the world's components
                let components =
                    { EntityDispatchers = entityDispatchers
                      GroupDispatchers = groupDispatchers
                      ScreenDispatchers = screenDispatchers
                      GameDispatchers = gameDispatchers
                      Facets = facets }

                // make the world's subsystems
                let subsystems =
                    { AudioPlayer = AudioPlayer.make AssetGraphFilePath
                      Renderer = Renderer.make sdlDeps.RenderContext AssetGraphFilePath
                      Integrator = Integrator.make farseerCautionMode Gravity 
                      Overlayer = Overlayer.make OverlayFilePath intrinsicOverlays }

                // make the world's message queues
                let messageQueues =
                    { AudioMessages = [HintAudioPackageUseMessage { PackageName = DefaultPackageName }]
                      RenderMessages = [HintRenderPackageUseMessage { PackageName = DefaultPackageName }]
                      PhysicsMessages = [] }

                // make the world's callbacks
                let callbacks =
                    { Tasks = []
                      Subscriptions = Map.empty
                      Unsubscriptions = Map.empty
                      CallbackStates = Map.empty }

                // make the world's state
                let state =
                    { TickTime = 0L
                      Liveness = Running
                      Interactivity = interactivity
                      OptScreenTransitionDestinationAddress = None
                      AssetMetadataMap = assetMetadataMap
                      AssetGraphFilePath = AssetGraphFilePath
                      OverlayRouter = OverlayRouter.make entityDispatchers userOverlayRoutes
                      OverlayFilePath = OverlayFilePath
                      UserState = userState }

                // make the game
                let game = World.makeGame activeGameDispatcher

                // make the world itself
                let world =
                    { Game = game
                      Screens = Map.empty
                      Groups = Map.empty
                      Entities = Map.empty
                      Camera = let eyeSize = Vector2 (single sdlDeps.Config.ViewW, single sdlDeps.Config.ViewH) in { EyeCenter = Vector2.Zero; EyeSize = eyeSize }
                      Components = components
                      Subsystems = subsystems
                      MessageQueues = messageQueues
                      Callbacks = callbacks
                      State = state }

                // and finally, register the game
                let world = snd <| Game.register world.Game world
                Right world
            | Left errorMsg -> Left errorMsg

        static member makeEmpty (userState : 'u) =

            // the default dispatchers
            let entityDispatcher = EntityDispatcher ()
            let groupDispatcher = GroupDispatcher ()
            let screenDispatcher = ScreenDispatcher ()
            let gameDispatcher = GameDispatcher ()

            // make the world's components
            let components =
                { EntityDispatchers = Map.singleton (Reflection.getTypeName entityDispatcher) entityDispatcher
                  GroupDispatchers = Map.singleton (Reflection.getTypeName groupDispatcher) groupDispatcher
                  ScreenDispatchers = Map.singleton (Reflection.getTypeName screenDispatcher) screenDispatcher
                  GameDispatchers = Map.singleton (Reflection.getTypeName gameDispatcher) gameDispatcher
                  Facets = Map.empty }

            // make the world's subsystems
            let subsystems =
                { AudioPlayer = { MockAudioPlayer  = () }
                  Renderer = { MockRenderer = () }
                  Integrator = { MockIntegrator = () }
                  Overlayer = { Overlays = XmlDocument () }}

            // make the world's message queues
            let messageQueues =
                { AudioMessages = []
                  RenderMessages = []
                  PhysicsMessages = [] }

            // make the world's callbacks
            let callbacks =
                { Tasks = []
                  Subscriptions = Map.empty
                  Unsubscriptions = Map.empty
                  CallbackStates = Map.empty }

            // make the world's state
            let state =
                { TickTime = 0L
                  Liveness = Running
                  Interactivity = GuiOnly
                  OptScreenTransitionDestinationAddress = None
                  AssetMetadataMap = Map.empty
                  AssetGraphFilePath = String.Empty
                  OverlayRouter = OverlayRouter.make (Map.ofList [World.pairWithName entityDispatcher]) []
                  OverlayFilePath = String.Empty
                  UserState = userState }

            // make the game
            let game = World.makeGame gameDispatcher

            // make the world itself
            let world =
                { Game = game
                  Screens = Map.empty
                  Groups = Map.empty
                  Entities = Map.empty
                  Camera = { EyeCenter = Vector2.Zero; EyeSize = Vector2 (single ResolutionXDefault, single ResolutionYDefault) }
                  Components = components
                  Subsystems = subsystems
                  MessageQueues = messageQueues
                  Callbacks = callbacks
                  State = state }

            // and finally, register the game
            snd <| Game.register world.Game world

        static member init () =

            // make types load reflectively from pathed (non-static) assemblies
            AppDomain.CurrentDomain.AssemblyLoad.Add
                (fun args -> LoadedAssemblies.[args.LoadedAssembly.FullName] <- args.LoadedAssembly)
            AppDomain.CurrentDomain.add_AssemblyResolve <| ResolveEventHandler
                (fun _ args -> snd <| LoadedAssemblies.TryGetValue args.Name)

            // ensure the current culture is invariate
            System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo.InvariantCulture

            // init type converters
            Math.initTypeConverters ()

            // assign functions to the pub / sub vars
            World.getSimulant <- World.getSimulantDefinition
            World.getOptSimulant <- World.getOptSimulantDefinition