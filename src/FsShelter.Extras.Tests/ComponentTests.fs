module FsShelter.Extras.ComponentTests

open System
open NUnit.Framework
open Swensen.Unquote

[<Test>]
let ``Joins``() = 
    let mutable res = 0
    let toKey _ = 1
    let agg input =
        function 
        | Some v -> true,(input ||| v) 
        | _ -> false,input
    let toOutput _ _ v = v

    let join = createJoinBolt toKey agg toOutput 

    join (0x1,(fun r -> res <- r)) |> Async.RunSynchronously
    join (0x2,(fun r -> res <- r)) |> Async.RunSynchronously

    res =! 3