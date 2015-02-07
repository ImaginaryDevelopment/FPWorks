module SpaceyModule
open Nu
open SpaceyConstants
open Nu.WorldConstants

type SpaceyDispatcher () =
        inherit GameDispatcher ()
                // this function creates the BlazeVector title screen to the world
        static let createTitleScreen world =

            // this creates a dissolve screen from the specified file with the given parameters
            let world = snd <| World.createDissolveScreenFromGroupFile false DissolveData typeof<ScreenDispatcher>.Name TitleGroupFilePath (Some TitleName) world

            // subscribes to the event that is raised when the Title screen's Play button is
            // clicked, and handles the event by transitioning to the Stage screen
            let world = World.subscribe4 (World.handleAsScreenTransition Stage) (ClickEventAddress ->>- TitlePlay.EntityAddress) Game world

            // subscribes to the event that is raised when the Title screen's Exit button is clicked,
            // and handles the event by exiting the game
            World.subscribe4 World.handleAsExit (ClickEventAddress ->>- TitleExit.EntityAddress) Game world

                // and so on.
        static let createStageScreen world =
            let world = snd <| World.createDissolveScreenFromGroupFile false DissolveData typeof<ScreenDispatcher>.Name StageGuiFilePath (Some StageName) world
            world

        // game registration is where the game's high-level logic is set up!
        override dispatcher.Register _ world = 
            // hint to the renderer that the 'Gui' package should be loaded up front
            let world = World.hintRenderPackageUse GuiPackageName world
            // create our screens
            let world = createTitleScreen world
            let world = createStageScreen world

            // create a splash screen that automatically transitions to the Title screen
            let (splash, world) = World.createSplashScreen false SplashData typeof<ScreenDispatcher>.Name Title (Some SplashName) world
            // play a neat sound effect, select the splash screen, and we're off!
            let world = World.playSound 1.0f NuSplashSound world
            World.selectScreen splash world

