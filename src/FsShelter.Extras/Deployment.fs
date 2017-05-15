module FsShelter.Extras.Deployment

open FsShelter
open System.IO

/// Include specified path as content output
let contentIncludes depPath =
    let ofSource n =
        n,Path.Combine("resources", (Path.GetDirectoryName >> Path.GetFileName) n,Path.GetFileName n)

    [ fun (_:string) -> depPath |> Directory.GetFiles |> Seq.map ofSource ]

/// Include specified path as build output 
/// and Protoshell as Storm-side serializer (presumed installed via Paket)
let defaultIncludes depPath =
    [ Includes.buildOutput Includes.defaultExtensions
      Includes.jarContents <| Path.Combine (depPath, "github.com/protoshell-1.0.1-SNAPSHOT-jar-with-dependencies.jar")] 
            
/// Submit topology with specified includes, process host, arguments and configuration
/// to a Nimbus service running at the specified address and port
let submit (topology:Topology.Topology<'t>) includes exePath mkCmdArgs conf address port = 
    let nimbusTopology = topology |> ThriftModel.ofTopology (mkCmdArgs exePath)
    Nimbus.withClient address port
        (fun client ->
            let binDir = Path.GetDirectoryName exePath
            let uploadedFile = 
                Package.makeJar (Includes.aggregate includes) binDir (Path.Combine(binDir, topology.Name) + ".jar")
                |> Nimbus.uploadJar client
            nimbusTopology
            |> Nimbus.submit client (Some conf) uploadedFile)

