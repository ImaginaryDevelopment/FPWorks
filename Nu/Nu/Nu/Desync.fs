﻿namespace Nu
open Prime
open Nu

module Desync =

    type [<NoComparison; NoEquality>] Desync<'e, 's, 'a> =
        Desync of ('s -> 's * Either<'e -> Desync<'e, 's, 'a>, 'a>)

    let private step (m : Desync<'e, 's, 'a>) (s : 's) : 's * Either<'e -> Desync<'e, 's, 'a>, 'a> =
        match m with Desync f -> f s

    let private returnM (a : 'a) : Desync<'e, 's, 'a> =
        Desync (fun s -> (s, Right a))
        
    let rec private bind (m : Desync<'e, 's, 'a>) (cont : 'a -> Desync<'e, 's, 'b>) : Desync<'e, 's, 'b> =
        Desync (fun s ->
            match step m s with
            | (s', Left m') -> (s', Left (fun e -> bind (m' e) cont))
            | (s', Right v) -> step (cont v) s')

    type DesyncBuilder () =

        member this.Return op = returnM op
        member this.Bind (m, cont) = bind m cont

    let desync =
        DesyncBuilder ()

    let get : Desync<'e, 's, 's> =
        Desync (fun s -> (s, Right s))

    let set (s : 's) : Desync<'e, 's, unit> =
        Desync (fun _ -> (s, Right ()))

    let advance (m : 'e -> Desync<'e, 's, 'a>) (e : 'e) (s : 's) : 's * Either<'e -> Desync<'e, 's, 'a>, 'a> =
        step (m e) s

    let rec run (m : Desync<'e, 's, 'a>) (e : 'e) (s : 's) : ('s * 'a) =
        match step m s with
        | (s', Left m') -> run (m' e) e s'
        | (s', Right v) -> (s', v)

    let waitE (m : 'e -> Desync<'e, 's, 'a>) : Desync<'e, 's, 'a> =
        Desync (fun s -> (s, Left m))

    let wait (m : Desync<'e, 's, 'a>) =
        waitE (fun _ -> m)

    let passE () : Desync<'e, 's, unit> =
        waitE <| (fun _ -> returnM ())

    let pass () : Desync<'e, 's, unit> =
        passE ()

    let callE expr : 'e -> Desync<'e, 's, unit> =
        fun e ->
            desync {
                let! s = get
                let s = expr e s
                do! set s }

    let call expr : Desync<'e, 's, unit> =
        callE (fun _ -> expr) Unchecked.defaultof<'e>

    let reactE expr : Desync<'e, 's, unit> =
        waitE <| (fun e -> callE expr e)

    let react expr : Desync<'e, 's, unit> =
        reactE (fun _ -> expr)

    let rec loopE (state : 't) (advance : 't -> 't) (pred : 't -> bool) (m : 'e -> 't -> Desync<'e, 's, unit>) =
        fun e ->
            if pred state then
                let state = advance state
                desync {
                    do! m e state
                    do! loopE state advance pred m e }
            else returnM ()

    let loop (state : 't) (advance : 't -> 't) (pred : 't -> bool) (m : 't -> Desync<'e, 's, unit>) =
        loopE state advance pred (fun _ -> m) Unchecked.defaultof<'e>

    let private runDesync4 makeSubscription (observable : Observable<'a, 'o>) (desync : Desync<'a Event, World, unit>) world =
        let callbackKey = World.makeCallbackKey ()
        let world = World.addCallbackState callbackKey (fun (_ : 'a Event) -> desync) world
        let subscriptionKey = World.makeSubscriptionKey ()
        let (eventAddress, unsubscribe, world) = observable.Subscribe world
        let unsubscribe = fun world ->
            let world = World.removeCallbackState callbackKey world
            let world = unsubscribe world
            World.unsubscribe subscriptionKey world
        let subscription = makeSubscription unsubscribe callbackKey
        let world = World.subscribe<'a, 'o> subscriptionKey subscription eventAddress observable.ObserverAddress world
        (unsubscribe, world)

    let runDesync4' eventHandling (observable : Observable<'a, 'o>) (desync : Desync<'a Event, World, unit>) world =
        let makeSubscription unsubscribe callbackKey =
            fun event world ->
                let desync = World.getCallbackState callbackKey world : 'a Event -> Desync<'a Event, World, unit>
                let (world, advanceResult) = advance desync event world
                match advanceResult with
                | Left desyncNext ->
                    let world = World.addCallbackState callbackKey desyncNext world
                    (eventHandling, world)
                | Right () ->
                    let world = unsubscribe world
                    (eventHandling, world)
        runDesync4 makeSubscription observable desync world

    /// Run the given desynchronized process on top of Nu's event system.
    /// Allows each desynchronized operation to run without referencing its source event, and
    /// without specifying its event handling approach by assuming Cascade.
    let runDesyncAssumingCascade (observable : Observable<'a, 'o>) desync world =
        runDesync4' Cascade observable desync world

    /// Run the given desynchronized process on top of Nu's event system.
    /// Allows each desynchronized operation to run without referencing its source event, and
    /// without specifying its event handling approach by assuming Resolve.
    let runDesyncAssumingResolve (observable : Observable<'a, 'o>) desync world =
        runDesync4' Resolve observable desync world