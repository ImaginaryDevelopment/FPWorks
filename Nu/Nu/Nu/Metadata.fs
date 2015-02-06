﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2015.

namespace Nu
open System
open System.ComponentModel
open System.Collections.Generic
open System.Drawing
open System.IO
open System.Linq
open System.Xml
open OpenTK
open TiledSharp
open Prime
open Nu
open Nu.Constants

[<AutoOpen>]
module MetadataModule =

    /// Metadata for an asset. Useful to describe various attributes of an asset without having the
    /// full asset loaded into memory.
    type [<StructuralEquality; NoComparison>] AssetMetadata =
        | TextureMetadata of Vector2i
        | TileMapMetadata of string * AssetTag list * TmxMap
        | SoundMetadata
        | SongMetadata
        | OtherMetadata of obj
        | InvalidMetadata of string

    /// A map of asset names to asset metadata.
    type AssetMetadataMap = Map<string, Map<string, AssetMetadata>>

    /// Thrown when a tile set property is not found.
    exception TileSetPropertyNotFoundException of string

[<RequireQualifiedAccess>]
module Metadata =

    let private getTileSetProperties (tileSet : TmxTileset) =
        let properties = tileSet.Properties
        try { PackageName = properties.["PackageName"]
              AssetName = properties.["ImageAssetName"] }
        with :? KeyNotFoundException ->
            let errorMessage = "TileSet '" + tileSet.Name + "' missing one or more properties (PackageName or AssetName)."
            raise <| TileSetPropertyNotFoundException errorMessage

    let private generateTextureMetadata asset =
        let separator = Path.DirectorySeparatorChar |> fun c-> c.ToString()
        let targetFilePath = asset.FilePath.Replace("/",separator);
        if not <| File.Exists targetFilePath then
            let errorMessage = "Failed to load Bitmap due to missing file '" + targetFilePath + "'."
            trace errorMessage
            InvalidMetadata errorMessage
        else
            // TODO: find an efficient way to pull metadata from a bitmap file without loading its
            // actual image buffer.
            try use bitmap = new Bitmap (targetFilePath)
                if bitmap.PixelFormat = Imaging.PixelFormat.Format32bppArgb
                then TextureMetadata <| Vector2i (bitmap.Width, bitmap.Height)
                else
                    let errorMessage = "Bitmap with invalid format (expecting 32-bit ARGB)."
                    trace errorMessage
                    InvalidMetadata errorMessage
            with _ as exn ->
                let errorMessage = "Failed to load Bitmap '" + targetFilePath + "' due to '" + acstring exn + "'."
                trace errorMessage
                InvalidMetadata errorMessage

    let private generateTileMapMetadata asset =
        let separator = Path.DirectorySeparatorChar |> fun c-> c.ToString()
        let targetFilePath = asset.FilePath.Replace("/",separator)
        if not <| File.Exists targetFilePath then
            let errorMessage = "Failed to load TmxMap due to missing file '" + targetFilePath + "'."
            trace errorMessage
            InvalidMetadata errorMessage
        else
            try let tmxMap = TmxMap targetFilePath
                let tileSets = List.ofSeq tmxMap.Tilesets
                let tileSetImages = List.map getTileSetProperties tileSets
                TileMapMetadata (targetFilePath, tileSetImages, tmxMap)
            with _ as exn ->
                let errorMessage = "Failed to load TmxMap '" + asset.FilePath + "' due to '" + acstring exn + "'."
                trace errorMessage
                InvalidMetadata errorMessage

    let private generateAssetMetadata asset =
        let extension = Path.GetExtension asset.FilePath
        let metadata =
            match extension with
            | ".bmp"
            | ".png" -> generateTextureMetadata asset
            | ".tmx" -> generateTileMapMetadata asset
            | ".wav" -> SoundMetadata
            | ".ogg" -> SongMetadata
            | _ -> InvalidMetadata <| "Could not load asset metadata '" + acstring asset + "' due to unknown extension '" + extension + "'."
        (asset.AssetTag.AssetName, metadata)

    let private generateAssetMetadataSubmap (packageNode : XmlNode) =
        let packageName = (packageNode.Attributes.GetNamedItem NameAttributeName).InnerText
        let assets =
            Seq.fold (fun assetsRev (node : XmlNode) ->
                match node.Name with
                | AssetNodeName -> match Assets.tryLoadAssetFromAssetNode node with Some asset -> asset :: assetsRev | None -> assetsRev
                | AssetsNodeName -> match Assets.tryLoadAssetsFromAssetsNode true node with Some assets' -> List.rev assets' @ assetsRev | None -> assetsRev
                | CommentNodeName -> assetsRev
                | _ -> [])
                []
                (packageNode.OfType<XmlNode> ()) |>
            List.rev
        let submap = Map.ofSeqBy (fun asset -> generateAssetMetadata asset) assets
        (packageName, submap)

    let private generateAssetMetadataMap packageNodes =
        List.fold
            (fun assetMetadataMap (packageNode : XmlNode) ->
                let (packageName, submap) = generateAssetMetadataSubmap packageNode
                Map.add packageName submap assetMetadataMap)
            Map.empty
            packageNodes

    /// Attempt to generate an asset metadata map from the given asset graph.
    let tryGenerateAssetMetadataMap assetGraphFilePath =
        try let document = XmlDocument ()
            document.Load (assetGraphFilePath : string) 
            let optRootNode = document.[RootNodeName]
            match optRootNode with
            | null -> Left <| "Root node is missing from asset graph file '" + assetGraphFilePath + "'."
            | rootNode ->
                let possiblePackageNodes = List.ofSeq <| rootNode.OfType<XmlNode> ()
                let packageNodes = List.filter (fun (node : XmlNode) -> node.Name = PackageNodeName) possiblePackageNodes
                let assetMetadataMap = generateAssetMetadataMap packageNodes
                Right assetMetadataMap
        with exn -> Left <| acstring exn

    /// Try to get the metadata of the given asset.
    let tryGetMetadata (assetTag : AssetTag) assetMetadataMap =
        let optPackage = Map.tryFind assetTag.PackageName assetMetadataMap
        match optPackage with
        | Some package ->
            let optAsset = Map.tryFind assetTag.AssetName package
            match optAsset with
            | Some _ as asset -> asset
            | None -> None
        | None -> None

    /// Try to get the texture metadata of the given asset.
    let tryGetTextureSize assetTag assetMetadataMap =
        let optAsset = tryGetMetadata assetTag assetMetadataMap
        match optAsset with
        | Some (TextureMetadata size) -> Some size
        | None -> None
        | _ -> None

    /// Forcibly get the texture size metadata of the given asset (throwing on failure).
    let getTextureSize assetTag assetMetadataMap =
        Option.get <| tryGetTextureSize assetTag assetMetadataMap

    /// Try to get the texture size metadata of the given asset.
    let tryGetTextureSizeAsVector2 assetTag assetMetadataMap =
        match tryGetTextureSize assetTag assetMetadataMap with
        | Some size -> Some <| Vector2 (single size.X, single size.Y)
        | None -> None

    /// Forcibly get the texture size metadata of the given asset (throwing on failure).
    let getTextureSizeAsVector2 assetTag assetMetadataMap =
        Option.get <| tryGetTextureSizeAsVector2 assetTag assetMetadataMap

    /// Try to get the tile map metadata of the given asset.
    let tryGetTileMapMetadata assetTag assetMetadataMap =
        let optAsset = tryGetMetadata assetTag assetMetadataMap
        match optAsset with
        | Some (TileMapMetadata (filePath, images, tmxMap)) -> Some (filePath, images, tmxMap)
        | None -> None
        | _ -> None

    /// Forcibly get the tile map metadata of the given asset (throwing on failure).
    let getTileMapMetadata assetTag assetMetadataMap =
        Option.get <| tryGetTileMapMetadata assetTag assetMetadataMap