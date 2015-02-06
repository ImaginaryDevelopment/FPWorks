﻿namespace Nu
open System
open System.IO
open System.Xml
open System.Reflection
open Prime
open Nu
open Nu.Constants
open Nu.WorldConstants

[<AutoOpen>]
module GroupModule =

    type Group with

        static member setPersistent persistent (group : Group) = { group with Persistent = persistent }

        static member register address (group : Group) (world : World) : Group * World =
            group.DispatcherNp.Register (address, group, world)
        
        static member unregister address (group : Group) (world : World) : Group * World =
            group.DispatcherNp.Unregister (address, group, world)

        static member dispatchesAs (dispatcherTargetType : Type) (group : Group) =
            Reflection.dispatchesAs dispatcherTargetType group.DispatcherNp

        static member make dispatcher optName =
            let id = Core.makeId ()
            { Group.Id = id
              Name = match optName with None -> acstring id | Some name -> name
              Persistent = true
              CreationTimeNp = DateTime.UtcNow
              DispatcherNp = dispatcher
              Xtension = { XFields = Map.empty; CanDefault = false; Sealed = true } }

[<AutoOpen>]
module WorldGroupModule =

    type World with

        static member private optGroupFinder (address : Group Address) world =
            match address.Names with
            | [screenName; groupName] ->
                let optGroupMap = Map.tryFind screenName world.Groups
                match optGroupMap with
                | Some groupMap -> Map.tryFind groupName groupMap
                | None -> None
            | _ -> failwith <| "Invalid group address '" + acstring address + "'."

        static member private groupAdder (address : Group Address) world child =
            match address.Names with
            | [screenName; groupName] ->
                match Map.tryFind screenName world.Groups with
                | Some groupMap ->
                    let groupMap = Map.add groupName child groupMap
                    { world with Groups = Map.add screenName groupMap world.Groups }
                | None ->
                    let groupMap = Map.add screenName (Map.singleton groupName child) world.Groups
                    { world with Groups = groupMap }
            | _ -> failwith <| "Invalid group address '" + acstring address + "'."

        static member private groupRemover (address : Group Address) world =
            match address.Names with
            | [screenName; groupName] ->
                match Map.tryFind screenName world.Groups with
                | Some groupMap ->
                    let groupMap = Map.remove groupName groupMap
                    { world with Groups = Map.add screenName groupMap world.Groups }
                | None -> world
            | _ -> failwith <| "Invalid group address '" + acstring address + "'."

        static member getGroup address world = Option.get <| World.optGroupFinder address world
        static member setGroup address group world = World.groupAdder address world group
        static member getOptGroup address world = World.optGroupFinder address world
        static member containsGroup address world = Option.isSome <| World.getOptGroup address world
        static member private setOptGroup address optGroup world =
            match optGroup with
            | Some group -> World.setGroup address group world
            | None -> World.groupRemover address world

        static member getOptGroupHierarchy address world =
            match World.getOptGroup address world with
            | Some group ->
                let entityMap = World.getEntityMap address world
                Some (group, entityMap)
            | None -> None
        
        static member getGroupHierarchy address world =
            Option.get <| World.getOptGroupHierarchy address world

        static member getGroupMap (screenAddress : Screen Address) world =
            match screenAddress.Names with
            | [screenName] ->
                match Map.tryFind screenName world.Groups with
                | Some groupMap -> groupMap
                | None -> Map.empty
            | _ -> failwith <| "Invalid screen address '" + acstring screenAddress + "'."

        static member getGroups screenAddress world =
            let groupMap = World.getGroupMap screenAddress world
            Map.toValueSeq groupMap

        static member getGroupMap3 groupNames (screenAddress : Screen Address) world =
            let groupNames = Set.ofSeq groupNames
            let groupMap = World.getGroupMap screenAddress world
            Map.filter (fun groupName _ -> Set.contains groupName groupNames) groupMap

        static member getGroups3 groupNames screenAddress world =
            let groups = World.getGroupMap3 screenAddress groupNames world
            Map.toValueSeq groups

        static member getGroupHierarchies screenAddress world =
            let groupMap = World.getGroupMap screenAddress world
            Map.map
                (fun groupName group ->
                    let groupAddress = satoga screenAddress groupName
                    let entityMap = World.getEntityMap groupAddress world
                    (group, entityMap))
                groupMap

        static member private registerGroup address group world =
            Group.register address group world

        static member private unregisterGroup address group world =
            Group.unregister address group world

        static member removeGroupImmediate address group world =
            let world = World.publish4 () (RemovingEventAddress ->>- address) address world
            let (group, world) = World.unregisterGroup address group world
            let entityMap = World.getEntityMap address world
            let world = snd <| World.removeEntitiesImmediate address entityMap world
            let world = World.setOptGroup address None world
            (group, world)

        static member removeGroup address (group : Group) world =
            let task =
                { ScheduledTime = world.State.TickTime
                  Operation = fun world ->
                    match World.getOptGroup address world with
                    | Some group -> snd <| World.removeGroupImmediate address group world
                    | None -> world }
            let world = World.addTask task world
            (group, world)

        static member removeGroupsImmediate (screenAddress : Screen Address) groups world =
            World.transformSimulants World.removeGroupImmediate satoga screenAddress groups world

        static member removeGroups (screenAddress : Screen Address) groups world =
            World.transformSimulants World.removeGroup satoga screenAddress groups world

        static member addGroup address groupHierarchy world =
            let (group, entities) = groupHierarchy
            if not <| World.containsGroup address world then
                let (group, world) =
                    match World.getOptGroup address world with
                    | Some _ -> World.removeGroupImmediate address group world
                    | None -> (group, world)
                let world = World.setGroup address group world
                let world = snd <| World.addEntities address entities world
                let (group, world) = World.registerGroup address group world
                let world = World.publish4 () (AddEventAddress ->>- address) address world
                (group, world)
            else failwith <| "Adding a group that the world already contains at address '" + acstring address + "'."

        static member addGroups screenAddress groupHierarchies world =
            Map.fold
                (fun (groups, world) groupName groupHierarchy ->
                    let (group, world) = World.addGroup (satoga screenAddress groupName) groupHierarchy world
                    (group :: groups, world))
                ([], world)
                groupHierarchies

        static member makeGroup dispatcherName optName world =
            let dispatcher = Map.find dispatcherName world.Components.GroupDispatchers
            let group = Group.make dispatcher optName
            Reflection.attachFields dispatcher group
            group

        static member writeGroupHierarchy (writer : XmlWriter) groupHierarchy world =
            let (group : Group, entities) = groupHierarchy
            writer.WriteAttributeString (DispatcherNameAttributeName, (group.DispatcherNp.GetType ()).Name)
            Serialization.writePropertiesFromTarget tautology3 writer group
            writer.WriteStartElement EntitiesNodeName
            World.writeEntities writer entities world
            writer.WriteEndElement ()

        static member writeGroupHierarchyToFile (filePath : string) groupHierarchy world =
            let filePathTmp = filePath + ".tmp"
            let writerSettings = XmlWriterSettings ()
            writerSettings.Indent <- true
            // NOTE: XmlWriter can also write to an XmlDocument / XmlNode instance by using
            // XmlWriter.Create <| (document.CreateNavigator ()).AppendChild ()
            use writer = XmlWriter.Create (filePathTmp, writerSettings)
            writer.WriteStartDocument ()
            writer.WriteStartElement RootNodeName
            writer.WriteStartElement GroupNodeName
            World.writeGroupHierarchy writer groupHierarchy world
            writer.WriteEndElement ()
            writer.WriteEndElement ()
            writer.WriteEndDocument ()
            writer.Dispose ()
            File.Delete filePath
            File.Move (filePathTmp, filePath)

        static member writeGroupHierarchies (writer : XmlWriter) groupHierarchies world =
            let groupHierarchies =
                List.sortBy
                    (fun (group : Group, _) -> group.CreationTimeNp)
                    (Map.toValueList groupHierarchies)
            let groupHierarchies = List.filter (fun (group : Group, _) -> group.Persistent) groupHierarchies
            for groupHierarchy in groupHierarchies do
                writer.WriteStartElement GroupNodeName
                World.writeGroupHierarchy writer groupHierarchy world
                writer.WriteEndElement ()

        static member readGroupHierarchy (groupNode : XmlNode) defaultDispatcherName defaultEntityDispatcherName world =

            // read in the dispatcher name and create the dispatcher
            let dispatcherName = Serialization.readDispatcherName defaultDispatcherName groupNode
            let dispatcher =
                match Map.tryFind dispatcherName world.Components.GroupDispatchers with
                | Some dispatcher -> dispatcher
                | None ->
                    note <| "Could not locate dispatcher '" + dispatcherName + "'."
                    let dispatcherName = typeof<GroupDispatcher>.Name
                    Map.find dispatcherName world.Components.GroupDispatchers
            
            // make the bare group with name as id
            let group = Group.make dispatcher None
            
            // attach the group's instrinsic fields from its dispatcher if any
            Reflection.attachFields group.DispatcherNp group

            // read the groups's properties
            Serialization.readPropertiesToTarget groupNode group
            
            // read the group's entities
            let entities = World.readEntities (groupNode : XmlNode) defaultEntityDispatcherName world

            // return the initialized group and entities
            (group, entities)

        static member readGroupHierarchies (parentNode : XmlNode) defaultDispatcherName defaultEntityDispatcherName world =
            match parentNode.SelectSingleNode GroupsNodeName with
            | null -> Map.empty
            | groupsNode ->
                let groupNodes = groupsNode.SelectNodes GroupNodeName
                Seq.fold
                    (fun groupHierarchies groupNode ->
                        let groupHierarchy = World.readGroupHierarchy groupNode defaultDispatcherName defaultEntityDispatcherName world
                        let groupName = (fst groupHierarchy).Name
                        Map.add groupName groupHierarchy groupHierarchies)
                    Map.empty
                    (enumerable groupNodes)

        static member readGroupHierarchyFromFile (filePath : string) world =
            let separator = Path.DirectorySeparatorChar |> fun c-> c.ToString()
            let targetFilePath = filePath.Replace("/",separator);
            let combinedPath = Path.Combine(Environment.CurrentDirectory,targetFilePath)
            let fullerPath = Uri(combinedPath).AbsolutePath.Replace("/",separator).Replace("%20"," ")
            if System.IO.File.Exists(combinedPath) = false then failwithf "Could not find path for file %s which could resolve to %s" combinedPath fullerPath
            use reader = XmlReader.Create combinedPath
            let document = let emptyDoc = XmlDocument () in (emptyDoc.Load reader; emptyDoc)
            let rootNode = document.[RootNodeName]
            let groupNode = rootNode.[GroupNodeName]
            World.readGroupHierarchy groupNode typeof<GroupDispatcher>.Name typeof<EntityDispatcher>.Name world