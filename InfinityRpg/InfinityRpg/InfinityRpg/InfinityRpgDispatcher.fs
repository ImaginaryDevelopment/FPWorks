﻿namespace InfinityRpg
open System
open System.IO
open OpenTK
open Prime
open Nu
open Nu.Constants
open Nu.WorldConstants
open Nu.Observer
open InfinityRpg
open InfinityRpg.Constants

[<AutoOpen>]
module InfinityRpgModule =

    type InfinityRpgDispatcher () =
        inherit GameDispatcher ()

        static let handleAsScreenTransitionToGameplay shallLoadGame event world =
            let gameplay = World.getScreen GameplayAddress world
            let gameplay = Screen.setShallLoadGame shallLoadGame gameplay
            let world = World.setScreen GameplayAddress gameplay world
            World.handleAsScreenTransition GameplayAddress event world

        static let addTitle world =
            let world = snd <| World.addDissolveScreenFromGroupFile false DissolveData typeof<ScreenDispatcher>.Name TitleAddress TitleGroupFilePath world
            let world = World.subscribe4 (World.handleAsScreenTransition CreditsAddress) ClickTitleCreditsEventAddress GameAddress world
            let world = World.subscribe4 (handleAsScreenTransitionToGameplay false) ClickTitleNewGameEventAddress GameAddress world
            let world = World.subscribe4 (handleAsScreenTransitionToGameplay true) ClickTitleLoadGameEventAddress GameAddress world
            World.subscribe4 World.handleAsExit ClickTitleExitEventAddress GameAddress world

        static let addCredits world =
            let world = snd <| World.addDissolveScreenFromGroupFile false DissolveData typeof<ScreenDispatcher>.Name CreditsAddress CreditsGroupFilePath world
            World.subscribe4 (World.handleAsScreenTransition TitleAddress) ClickCreditsBackEventAddress GameAddress world

        static let addGameplay world =
            let world = snd <| World.addDissolveScreenFromGroupFile true DissolveData typeof<GameplayDispatcher>.Name GameplayAddress HudFilePath world
            let world = World.setGroup HudAddress (Group.setPersistent false <| World.getGroup HudAddress world) world
            World.subscribe4 (World.handleAsScreenTransition TitleAddress) (ClickEventAddress ->>- HudBackAddress) GameAddress world

        override dispatcher.Register (game, world) =
            let world = World.hintRenderPackageUse GuiPackageName world
            let world = World.hintRenderPackageUse GameplayPackageName world
            let world = addTitle world
            let world = addCredits world
            let world = addGameplay world
            let (splashScreen, world) = World.addSplashScreen false NuSplashData typeof<ScreenDispatcher>.Name NuSplashAddress TitleAddress world
            let world = snd <| World.selectScreen NuSplashAddress splashScreen world
            (game, world)