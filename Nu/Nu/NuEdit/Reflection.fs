﻿// NuEdit - The Nu Game Engine editor.
// Copyright (C) Bryan Edds, 2013-2014.

namespace NuEdit
open System
open System.Reflection
open Prime
open Nu
open NuEdit

[<AutoOpen>]
module ReflectionModule =

    type EntityProperty =
        | EntityXFieldDescriptor of XFieldDescriptor
        | EntityPropertyInfo of PropertyInfo

module Reflection =

    let containsProperty<'t> (property : PropertyInfo) =
        let properties = typeof<'t>.GetProperties property.Name
        Seq.exists (fun item -> item = property) properties

    let getEntityPropertyValue property (entity : Entity) =
        match property with
        | EntityXFieldDescriptor x ->
            let xtension = entity.Xtension
            (Map.find x.FieldName xtension.XFields).FieldValue
        | EntityPropertyInfo p ->
            if containsProperty<Entity> p then p.GetValue entity
            else p.GetValue entity

    let setEntityPropertyValue property value (entity : Entity) =
        match property with
        | EntityXFieldDescriptor x ->
            let xField = { FieldValue = value; FieldType = x.FieldType }
            let xFields = Map.add x.FieldName xField entity.Xtension.XFields
            { entity with Xtension = { entity.Xtension with XFields = xFields }}
        | EntityPropertyInfo p ->
            let entity = { entity with Id = entity.Id } // NOTE: hacky copy
            p.SetValue (entity, value)
            entity