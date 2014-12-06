﻿namespace InfinityRpg
open System
open OpenTK
open Prime
open Nu
open Nu.Constants
open Nu.WorldConstants
open InfinityRpg
open InfinityRpg.Constants

[<AutoOpen>]
module PlayerDispatcherModule =

    type PlayerDispatcher () =
        inherit CharacterDispatcher ()

        static member FieldDefinitions =
            [define? ControlType Player]

        static member IntrinsicFacetNames =
            [typeof<CharacterCameraFacet>.Name]