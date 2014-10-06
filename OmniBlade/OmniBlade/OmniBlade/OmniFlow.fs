﻿namespace OmniBlade
open System
open Prime
open Nu
open Nu.Constants
open OmniBlade
open OmniBlade.OmniConstants
module OmniFlow =

    type OmniComponentFactory () =
        inherit UserComponentFactory ()

        override dispatcher.MakeGroupDispatchers () =
            Map.ofList
                [typeof<BattleGroupDispatcher>.Name, BattleGroupDispatcher () :> GroupDispatcher
                 typeof<FieldGroupDispatcher>.Name, FieldGroupDispatcher () :> GroupDispatcher]

        override dispatcher.MakeGameDispatchers () =
            Map.ofList
                [typeof<OmniBladeDispatcher>.Name, OmniBladeDispatcher () :> GameDispatcher]

    let addTitleScreen world =
        let world = snd <| World.addDissolveScreenFromFile typeof<ScreenDispatcher>.Name TitleGroupFileName IncomingTime OutgoingTime TitleAddress world
        let world = World.subscribe4 ClickTitleNewGameEvent Address.empty (World.handleAsScreenTransition FieldAddress) world
        let world = World.subscribe4 ClickTitleLoadGameEvent Address.empty (World.handleAsScreenTransition LoadGameAddress) world
        let world = World.subscribe4 ClickTitleCreditsEvent Address.empty (World.handleAsScreenTransition CreditsAddress) world
        World.subscribe4 ClickTitleExitEvent Address.empty World.handleAsExit world

    let addLoadGameScreen world =
        let world = snd <| World.addDissolveScreenFromFile typeof<ScreenDispatcher>.Name LoadGameGroupFileName IncomingTime OutgoingTime LoadGameAddress world
        World.subscribe4 ClickLoadGameBackEvent Address.empty (World.handleAsScreenTransition TitleAddress) world

    let addCreditsScreen world =
        let world = snd <| World.addDissolveScreenFromFile typeof<ScreenDispatcher>.Name CreditsGroupFileName IncomingTime OutgoingTime CreditsAddress world
        World.subscribe4 ClickCreditsBackEvent Address.empty (World.handleAsScreenTransition TitleAddress) world

    let addFieldScreen world =
        let world = snd <| World.addDissolveScreenFromFile typeof<ScreenDispatcher>.Name FieldGroupFileName IncomingTime OutgoingTime FieldAddress world
        World.subscribe4 ClickFieldBackEvent Address.empty (World.handleAsScreenTransition TitleAddress) world

    let tryMakeOmniBladeWorld sdlDeps userState =
        let omniComponentFactory = OmniComponentFactory ()
        let optWorld = World.tryMake sdlDeps omniComponentFactory UIAndPhysics false userState
        match optWorld with
        | Right world ->
            let world = World.hintRenderingPackageUse UIPackageName world
            // let world = World.playSong GameSong 1.0f DefaultTimeToFadeOutSongMs world
            let splashScreenImage = { ImageAssetName = "Image5"; PackageName = DefaultPackageName }
            let (splashScreen, world) = World.addSplashScreenFromData TitleAddress SplashAddress typeof<ScreenDispatcher>.Name IncomingTimeSplash IdlingTime OutgoingTimeSplash splashScreenImage world
            let world = addTitleScreen world
            let world = addLoadGameScreen world
            let world = addCreditsScreen world
            let world = addFieldScreen world
            let world = snd <| World.selectScreen SplashAddress splashScreen world
            Right world
        | Left _ as left -> left