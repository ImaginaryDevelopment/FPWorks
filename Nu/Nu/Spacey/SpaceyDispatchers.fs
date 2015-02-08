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

        static let [<Literal>] BulletLifetime = 40L

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
            [
                define? Size <| Vector2 (24.0f, 24.0f)
                define? Depth 6
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
    let trace s = System.Diagnostics.Trace.WriteLine(s)
    type PlayerDispatcher () =
        inherit TopViewCharacterDispatcher()
//        static let screenToScene (p:Vector2) maxSourceX maxSourceY maxXTarget maxYTarget = 
//            p.X * maxXTarget / maxSourceX, p.Y * maxYTarget / maxSourceY
        static let bulletStart (sourcePosition:Vector2) (targetPosition:Vector2) n = 

            let x1,y1,x2,y2 = sourcePosition.X,sourcePosition.Y, targetPosition.X, targetPosition.Y
            let slope = (y2 - y1) / (x2-x1)
            let pointSlope x = slope * (x - x1) + y1
            let x3,y3 = 
                n+10.0f
                |> fun x -> x,pointSlope x
            
            let result = Vector2(x3,y3)
            sprintf "source %A target  %A n %A slope %A  point %A" sourcePosition targetPosition n slope result |> trace
            result

        static let createBullet (playerTransform : Transform) (group : Group) targetPosition world =
            let (bullet, world) = World.createEntity typeof<BulletDispatcher>.Name None group world
            //let bulletPosition =   playerTransform.Position + Vector2 (playerTransform.Size.X * 1.0f, playerTransform.Size.Y * 0.5f)
            let bulletPosition = bulletStart playerTransform.Position targetPosition <| playerTransform.Size.X / 2.0f // player is a circle
            sprintf "creating bullet at %A" bulletPosition |> trace
            let world = bullet.SetPosition bulletPosition world
            let world = bullet.SetDepth playerTransform.Depth world
            let world = World.propagateEntityPhysics bullet world
            (bullet, world)

        static let propelBullet (bullet : Entity) direction world =
            let world = World.applyBodyLinearImpulse direction (bullet.GetPhysicsId world) world
            World.playSound 1.0f NuSplashSound world

        static let shootBullet (player : Entity) (targetPosition:Vector2) world =
            let playerTransform = player.GetTransform world
            let playerGroup = Group.proxy <| eatoga player.EntityAddress
            let velocity = 5.5f
            let direction =
                match playerTransform.Position.X > targetPosition.X, playerTransform.Position.Y> targetPosition.Y with
                | true,false -> Vector2(velocity,-velocity)
                | true,true -> Vector2(velocity,velocity)
                | false,true -> Vector2(-velocity,velocity)
                | false,false -> Vector2(-velocity,-velocity)
                
            let (bullet, world) = createBullet playerTransform playerGroup targetPosition world
            propelBullet bullet direction world 
        static let handleSpawnBullet event world =
            let player = event.Subscriber : Entity
            let mouseButtonData = event.Data : MouseButtonData
            if World.isGamePlaying world then
                if World.getTickTime world % 3L = 0L then
                    let world = shootBullet player mouseButtonData.Position world
                    (Cascade, world)
                else (Cascade, world)
            else (Cascade, world)
        override dispatcher.Register player world =
            world
            |> World.monitor handleSpawnBullet MouseRightChangeEventAddress player
            
 
