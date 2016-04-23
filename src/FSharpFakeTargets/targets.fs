﻿namespace datNET

module Targets =
  open datNET.Version
  open datNET.AssemblyInfo
  open Fake
  open Fake.FileSystem
  open Fake.FileSystemHelper
  open Fake.NuGetHelper
  open System
  open System.IO

  let private RootDir = Directory.GetCurrentDirectory()

  // TODO: Unhardcode this ASAP
  let private _AssemblyInfoFilePath = System.IO.Path.Combine(RootDir, "AssemblyInfo.fs");

  type ConfigParams =
    {
      SolutionFile : FileIncludes
      MSBuildArtifacts : FileIncludes
      MSBuildReleaseArtifacts : FileIncludes
      MSBuildOutputDir : string
      NuspecFilePath : Option<string>
      Version : string
      Project : string
      Authors : string list
      Description : string
      OutputPath : string
      WorkingDir : string
      Publish : bool
      AccessKey : string
    }

  let ConfigDefaults() =
    {
      SolutionFile = !! (Path.Combine(RootDir, "*.sln"))
      MSBuildArtifacts = !! "src/**/bin/**/*.*" ++ "src/**/obj/**/*.*"
      MSBuildReleaseArtifacts = !! "**/bin/Release/*"
      MSBuildOutputDir = "bin"
      NuspecFilePath = TryFindFirstMatchingFile "*.nuspec" "."
      Version = String.Empty
      Project = String.Empty
      Authors = List.Empty
      Description = String.Empty
      OutputPath = String.Empty
      WorkingDir = String.Empty
      Publish = false
      AccessKey = String.Empty
    }

  let private _EnsureNuspecFileExists filePath =
    match filePath with
    | Some x -> x
    | None -> raise (FileNotFoundException("Could not find the nuspec file"))

  let private _CreateTarget targetName parameters targetFunc =
    Target targetName targetFunc
    parameters

  let private _CreateNuGetParams parameters =
    (fun (nugetParams : NuGetParams) ->
        { nugetParams with
            Version = parameters.Version
            Project = parameters.Project
            Authors = parameters.Authors
            Description = parameters.Description
            OutputPath = parameters.OutputPath
            WorkingDir = parameters.WorkingDir
            Publish = parameters.Publish
            AccessKey = parameters.AccessKey
        })

  let private _MSBuildTarget parameters =
    _CreateTarget "MSBuild" parameters (fun _ ->
        parameters.SolutionFile
            |> MSBuildRelease null "Build"
            |> ignore

        Copy parameters.MSBuildOutputDir parameters.MSBuildReleaseArtifacts
    )

  let private _CleanTarget parameters =
    _CreateTarget "Clean" parameters (fun _ ->
        DeleteFiles parameters.MSBuildArtifacts
        CleanDir parameters.MSBuildOutputDir
    )

  let private _PackageTarget parameters =
    _CreateTarget "Package" parameters (fun _ ->
        parameters.NuspecFilePath
            |> _EnsureNuspecFileExists
            |> NuGetPack (_CreateNuGetParams parameters)
    )

  let private _PublishTarget parameters =
    _CreateTarget "Publish" parameters (fun _ ->
        NuGetPublish (_CreateNuGetParams parameters)
    )

  let private _IncrementPatchTarget parameters =
    _CreateTarget "IncrementPatch" parameters (fun _ ->
        let currentVersion = datNET.AssemblyInfo.GetAssemblyInformationalVersionString _AssemblyInfoFilePath
        let nextVersion = datNET.Version.IncrPatch currentVersion
        let nextFourVersion = (datNET.Version.CoerceStringToFourVersion nextVersion).ToString()
        let contents = (System.IO.File.ReadAllText(_AssemblyInfoFilePath))

        let ugh setVersion versionStr cntnts =
          setVersion cntnts versionStr
          |> (fun (strSeq: seq<string>) -> String.Join("\n", strSeq))

        let newContents =
            contents
            |> ugh datNET.AssemblyInfo.SetAssemblyVersion nextFourVersion
            |> ugh datNET.AssemblyInfo.SetAssemblyFileVersion nextFourVersion
            |> ugh datNET.AssemblyInfo.SetAssemblyInformationalVersion nextVersion
            //|> (fun c -> datNET.AssemblyInfo.SetAssemblyVersion c nextFourVersion)
            //|> (fun c -> datNET.AssemblyInfo.SetAssemblyFileVersion c nextFourVersion)
            //|> (fun c -> datNET.AssemblyInfo.SetAssemblyFileVersion c nextVersion)

        System.IO.File.WriteAllText(_AssemblyInfoFilePath, newContents)
    )

  let private _IncrementMinorTarget parameters =
    _CreateTarget "IncrementMinor" parameters (fun _ ->
        printfn "%O" "TODO"
    )

  let private _IncrementMajorTarget parameters =
    _CreateTarget "IncrementMajor" parameters (fun _ ->
        printfn "%O" "TODO"
    )

  let Initialize setParams =
    let parameters = ConfigDefaults() |> setParams

    parameters
        |> _MSBuildTarget
        |> _CleanTarget
        |> _PackageTarget
        |> _PublishTarget
        |> _IncrementPatchTarget
        |> _IncrementMinorTarget
        |> _IncrementMajorTarget
