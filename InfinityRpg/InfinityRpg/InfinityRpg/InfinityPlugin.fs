﻿namespace InfinityRpg
open System
open Prime
open Nu
open Nu.Constants
open InfinityRpg
open InfinityRpg.Constants

[<AutoOpen>]
module PluginModule =

    type InfinityRpgPlugin () =
        inherit NuPlugin ()

        override this.MakeOptGameDispatcher () =
            Some (InfinityDispatcher () :> GameDispatcher)

        override this.MakeScreenDispatchers () =
            [GameplayDispatcher () :> ScreenDispatcher]

        override this.MakeEntityDispatchers () =
            [FieldDispatcher () :> EntityDispatcher
             EnemyDispatcher () :> EntityDispatcher
             CharacterDispatcher () :> EntityDispatcher
             PlayerDispatcher () :> EntityDispatcher]

        override this.MakeFacets () =
            [CharacterStateFacet () :> Facet
             CharacterAnimationFacet () :> Facet
             CharacterCameraFacet () :> Facet]

        override this.MakeOverlayRoutes () =
            [typeof<ButtonDispatcher>.Name, Some "InfinityButtonDispatcher"]