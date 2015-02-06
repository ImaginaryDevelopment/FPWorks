﻿// NuEdit - The Nu Game Engine editor.
// Copyright (C) Bryan Edds, 2013-2015.

namespace NuEdit
open System
open Prime
open Nu
open Nu.Constants
open Nu.WorldConstants
module Constants =

    // TODO: make InfinityRpg a build dependency when it's working again

    let EditorScreenName = "EditorScreen"
    let EditorScreen = Screen.proxy <| ntoa EditorScreenName
    let EditorGroupName = "Group"
    let EditorGroup = Group.proxy <| satoga EditorScreen.ScreenAddress EditorGroupName
    let AddEntityKey = World.makeSubscriptionKey ()
    let RemovingEntityKey = World.makeSubscriptionKey ()