module SpaceyConstants
open Nu
open Nu.WorldConstants
open Nu.Constants

// these constants specify the packages as named in the project's 'AssetGraph.xml' file

let GuiPackageName = "Gui"
let StagePackageName = "Stage"

let PlayerBulletImage = { PackageName = DefaultPackageName; AssetName = "Image" }
    // this constant describes the 'dissolving' transition behavior of game's screens
let DissolveData =
    {
        IncomingTime = 20L
        OutgoingTime = 30L
        DissolveImage = DefaultDissolveImage 
    }
let BulletForce = 0.5f
// these are like the pair of constants for splash screen above, but for the title screen
let TitleName = "Title"
let Title = Screen.proxy <| ntoa TitleName
// these are also like the pair of constants above, but for the group that is loaded into the
// title that contains all of its gui entities. You'll notice that the group is proxied from a
// combination of the address of its containing screen as well as its own personal name as
// found in its originating document, 'Assets/Gui/Title.nugroup'.
//
// You'll need to familiarize yourself with the 'satoga' operator and its relatives by reading
// their respective documentation comments.
let TitleGroupName = "Group"
let TitleGroup = Group.proxy <| satoga Title.ScreenAddress TitleGroupName


// these constants specify the file paths from which various simulants are loaded
let TitleGroupFilePath = "Assets/Gui/Title.nugroup"
let StageGuiFilePath = "Assets/Stage/Gui.nugroup"
let StageGameplayFilePath = "Assets/Stage/Gameplay.nugroup"
// these are like the above, but for the play button found in the above group
let TitlePlayName = "Play"
let TitlePlay = Entity.proxy <| gatoea TitleGroup.GroupAddress TitlePlayName

// these pair of constants are -
//  a) a string used to give a name to the splash screen
//  b) a proxy used to locate and operate upon the splash screen
// A screen proxy is created by first converting a name to an address with the ntoa function, and
// then passing the address into the Screen.proxy function.
let SplashName = "Splash"
let Splash = Screen.proxy <| ntoa SplashName

let NuSplashSound = { PackageName = DefaultPackageName; AssetName = "Sound" }

// this constant describes the 'splashing' behavior of game's splash screen
let SplashData =
    { 
        DissolveData = DissolveData
        IdlingTime = 60L
        SplashImage = { PackageName = DefaultPackageName; AssetName = "Image5" }
    }

// and so on.
let TitleExitName = "Exit"
let TitleExit = Entity.proxy <| gatoea TitleGroup.GroupAddress TitleExitName
// these constants specify names and proxies for various simulants of the state screen
let StageName = "Stage"
let Stage = Screen.proxy <| ntoa StageName
let StageGroupName = "Group"
let StageGroup = Group.proxy <| satoga Stage.ScreenAddress StageGroupName