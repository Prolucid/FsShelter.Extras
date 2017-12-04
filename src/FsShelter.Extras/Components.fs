[<AutoOpen>]
module FsShelter.Extras.Components

open System
open FsShelter.DSL
open FsBunny
open Microsoft.Extensions.Caching.Memory

/// Persist the input in the given context.
/// input: incoming tuple.
/// using: provides call context for update.
/// persist: persit the input using specified context.
let createPersistBolt persist (using, input) = 
    using (fun ctx -> async { return! input |> persist ctx } )

/// Transform input into a seq and emit.
/// input: incoming tuple.
/// out: do something with the output.
/// using: provide context for the select.
/// transform: transform the input using a context.
let createTransformBolt transform (using, input, out) = 
    using (fun ctx -> 
            async {
                let! xs = input |> transform ctx 
                return xs |> Seq.iter out
            })

/// Create Ack*Nack pair for a queue spout.
/// consumer: event stream consumer
let ackerOfConsumer (consumer:Consumer<_>) : Acker = 
    (uint64 >> consumer.Ack, uint64 >> consumer.Nack)


/// Create a queue spout.
/// consumer: event stream consumer
let createQueueSpout toTuple (consumer:Consumer<_>) = 
    async {
        return consumer.Get 1<FSharp.Data.UnitSystems.SI.UnitSymbols.s> 
               |> Option.map (fun r -> string r.id, toTuple r.msg)
    }

/// Create a publisher bolt.
/// ofTuple: conversion function
/// publisher: event stream publisher
let createNotifierBolt ofTuple (input, publish:Publisher<_>) = 
    async {
        return input |> ofTuple |> publish
    }

/// Join multiple streams into a single output.
/// toKey: select a key from the input tuple (has to privide meaningful conversion to string!).
/// toOutput: select output tuple.
/// aggregate: join new tuple to available tuples and return the (complete,result).
/// ttl: time to live for cached data
/// numberOfAgents: parallelism for mailbox processor doing the join
/// input: incoming tuple.
let createJoinBoltWith (ttl:TimeSpan, numberOfAgents:int) toKey aggregate toOutput = 
    let mkCacheAgent x = 
        let cache = new MemoryCache(MemoryCacheOptions())
        MailboxProcessor.Start(fun inbox -> 
            async { 
                while true do
                    let! (key,tuple,(rc : AsyncReplyChannel<Choice<_,Exception>>)) = inbox.Receive()
                    try
                        let (complete,newValue) = 
                            cache.Get key |> Option.ofObj |> Option.map unbox |> aggregate tuple
                        if complete then 
                            cache.Remove key |> ignore
                        else 
                            cache.Set(key, box newValue, DateTimeOffset.Now.Add ttl) |> ignore
                        Choice1Of2(complete,newValue) |> rc.Reply
                    with ex -> Choice2Of2 ex |> rc.Reply
            })

    let cacheAgents = [| for i in 1..numberOfAgents -> mkCacheAgent i |]
    fun (input,out) -> 
        async { 
            let key = toKey input
            let cacheAgent = cacheAgents.[abs ((box key).GetHashCode() % numberOfAgents)]
            let! res = cacheAgent.PostAndAsyncReply((fun rc -> ((box >> string) key),input,rc), (int ttl.TotalMilliseconds))
            match res with
            | Choice1Of2 (complete,value) -> if complete then toOutput key input value |> out
            | Choice2Of2 ex -> raise ex
        }


/// Join multiple streams into a single output.
/// toKey: select a key from the input tuple.
/// toOutput: select output tuple.
/// aggregate: join new tuple to available tuples and return the (complete,result).
let createJoinBolt toKey = createJoinBoltWith (TimeSpan.FromSeconds 30., 10) toKey

/// Aggregate inputs and/or emit at any time.
/// input: incoming tuple.
/// out: do something with the output.
/// using: provide context for the select.
/// transform: transform the input using a context.
let createAggregationBolt read (using, input, out) = 
    using (fun ctx -> 
            async {
                do! input |> read ctx out
            })

/// Create a publisher bolt that formats a topic at runtime
/// ofTuple: conversion function for message and topic format parameter
/// input: incoming tuple
/// publish: function taking formatted topic and returning event stream publisher
let createExchangeBolt ofTuple (input, publish:string -> Publisher<_>) = 
    async {
        let msg, topic = input |> ofTuple
        return msg |> publish topic
    }

open FSharp.Quotations
open FSharp.Quotations.Patterns

/// Represent a DU case as a signal
type Signal =
    static member Of([<ReflectedDefinition>]x:Expr<_>) = 
        let rec getCase = function | Lambda(_,ex) -> getCase ex
                                   | Let(_,_,ex) -> getCase ex
                                   | NewUnionCase(case,_) -> case
                                   | _ -> failwithf "Unsupported expression: %A" x

        <@@ %x @@> |> getCase |> fun case -> (bigint 1) <<< case.Tag
                               

open FSharp.Reflection

/// Join aggregator for signals
let signalAggregator<'t> (signals:bigint list) =
    let reader = FSharpValue.PreComputeUnionTagReader typeof<'t>
    fun (input:'t) -> 
        let signal = (bigint 1) <<< (reader input)
        function
        | Some (vs:bigint list) -> signals |> List.zip vs |> List.map (fun (acc,target) -> target &&& (acc ||| signal))
        | _ -> signals |> List.map ((&&&) signal)
        >> fun agg -> (agg |> List.fold (fun all s -> all && (s>bigint.Zero)) true),agg
            