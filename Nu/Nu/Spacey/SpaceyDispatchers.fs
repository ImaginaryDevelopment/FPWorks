namespace Spacey
open Nu
open Nu.Constants
open Nu.WorldConstants
open SpaceyConstants
open OpenTK

[<AutoOpen>]
module BulletModule =
    type Entity with
    
        member this.GetAge world : int64 = (this.GetXtension world)?Age
        member this.SetAge (value : int64) world = this.UpdateXtension (fun xtension -> xtension?Age <- value) world

    type BulletDispatcher () =
        inherit EntityDispatcher ()

        static let [<Literal>] BulletLifetime = 27L

        static let handleTick event world =
            let bullet = event.Subscriber : Entity
            if World.isGamePlaying world then
                let world = bullet.SetAge (bullet.GetAge world + 1L) world
                let world =
                    if bullet.GetAge world > BulletLifetime
                    then World.destroyEntity bullet world
                    else world
                (Cascade, world)
            else (Cascade, world)

        static let handleCollision event world =
            let bullet = event.Subscriber : Entity
            if World.isGamePlaying world then
                let world = World.destroyEntity bullet world
                (Cascade, world)
            else (Cascade, world)

        static member FieldDefinitions =
            [define? Size <| Vector2 (24.0f, 24.0f)
             define? Density 0.25f
             define? Restitution 0.5f
             define? LinearDamping 0.0f
             define? GravityScale 0.0f
             define? IsBullet true
             define? CollisionExpr "Circle"
             define? SpriteImage PlayerBulletImage
             define? Age 0L]

        static member IntrinsicFacetNames =
            [typeof<RigidBodyFacet>.Name
             typeof<SpriteFacet>.Name]

        override dispatcher.Register bullet world =
            world |>
                World.monitor handleTick TickEventAddress bullet |>
                World.monitor handleCollision (CollisionEventAddress ->>- bullet.EntityAddress) bullet
[<AutoOpen>]
module PlayerModule =

    type PlayerDispatcher () =
        inherit TopViewCharacterDispatcher()

        static let createBullet (playerTransform : Transform) (group : Group) targetPosition world =
            let (bullet, world) = World.createEntity typeof<BulletDispatcher>.Name None group world
            targetPosition |> ignore
            let bulletPosition =   playerTransform.Position + Vector2 (playerTransform.Size.X * 1.0f, playerTransform.Size.Y * 0.5f)
            let world = bullet.SetPosition bulletPosition world
            let world = bullet.SetDepth playerTransform.Depth world
            let world = World.propagateEntityPhysics bullet world
            (bullet, world)

        static let propelBullet (bullet : Entity) world =
            let world = World.applyBodyLinearImpulse (Vector2 (50.0f, 0.0f)) (bullet.GetPhysicsId world) world
            World.playSound 1.0f NuSplashSound world

        static let shootBullet (player : Entity) targetPosition world =
            let playerTransform = player.GetTransform world
            let playerGroup = Group.proxy <| eatoga player.EntityAddress
            let (bullet, world) = createBullet playerTransform playerGroup targetPosition world
            propelBullet bullet world
        static let handleSpawnBullet event world =
            let player = event.Subscriber : Entity
            let mouseButtonData = event.Data : MouseButtonData
            if World.isGamePlaying world then
                if World.getTickTime world % 6L = 0L then
                    let world = shootBullet player mouseButtonData.Position world
                    (Cascade, world)
                else (Cascade, world)
            else (Cascade, world)
        override dispatcher.Register player world =
            world
            |> World.monitor handleSpawnBullet MouseRightChangeEventAddress player
            
 
