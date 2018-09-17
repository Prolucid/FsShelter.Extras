[<AutoOpen>]
module FsShelter.Extras.Components

open System
open FsShelter.DSL
open FsBunny
open Microsoft.Extensions.Caching.Memory
open FSharp.Quotations
open FSharp.Quotations.Patterns

/// Represent a DU case as a signal
/// Signals are more about the fact that something happened than the data they carry.
/// They can be used to implement logic gates (for an E to happen, A,B,C and D have to happen first).
type Signal =
    static member Of([<ReflectedDefinition>]x:Expr<_>) = 
        let rec getCase = function | Lambda(_,ex) -> getCase ex
                                   | Let(_,_,ex) -> getCase ex
                                   | NewUnionCase(case,_) -> case
                                   | _ -> failwithf "Unsupported expression: %A" x

        <@@ %x @@> |> getCase |> fun case -> (bigint 1) <<< case.Tag
                               

module Bolt =
    /// Persist the input in the given context.
    /// input: incoming tuple.
    /// using: provides call context for update.
    /// persist: persit the input using specified context.
    let mkPersister persist (using, input) = 
        using (fun ctx -> async { return! input |> persist ctx } )

    /// Transform input into a seq and emit.
    /// input: incoming tuple.
    /// out: do something with the output.
    /// using: provide context for the select.
    /// transform: transform the input using a context.
    let mkTransformer transform (using, input, out) = 
        using (fun ctx -> 
                async {
                    let! xs = input |> transform ctx 
                    return xs |> Seq.iter out
                })

    /// Create a simple publisher bolt that always publishes to the same topic.
    /// ofTuple: conversion function
    /// publisher: event stream publisher
    let mkNotifier ofTuple (input, publish:Publisher<_>) = 
        input |> ofTuple |> publish

    /// Create a publisher bolt that formats a topic at runtime
    /// ofTuple: conversion function for message and topic format parameter
    /// input: incoming tuple
    /// mkPublish: function taking formatted topic and returning event stream publisher
    let mkTopicNotifier ofTuple (input, mkPublish:string -> Publisher<_>) = 
        let msg, topic = input |> ofTuple
        msg |> mkPublish topic

    /// Join multiple streams into a single output.
    /// toKey: select a key from the input tuple (has to privide meaningful conversion to string!).
    /// toOutput: select output tuple.
    /// aggregate: join new tuple to available tuples and return the (complete,result).
    /// ttl: time to live for cached data
    /// numberOfAgents: parallelism for mailbox processor doing the join
    /// input: incoming tuple.
    let mkJoinerWith (ttl:TimeSpan, numberOfAgents:int) toKey aggregate toOutput = 
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
            let key = toKey input
            let cacheAgent = cacheAgents.[abs ((box key).GetHashCode() % numberOfAgents)]
            let res = cacheAgent.PostAndReply((fun rc -> ((box >> string) key),input,rc), (int ttl.TotalMilliseconds))
            match res with
            | Choice1Of2 (complete,value) -> if complete then toOutput key input value |> out
            | Choice2Of2 ex -> raise ex


    /// Join multiple streams into a single output.
    /// toKey: select a key from the input tuple.
    /// toOutput: select output tuple.
    /// aggregate: join new tuple to available tuples and return the (complete,result).
    let mkJoiner toKey = mkJoinerWith (TimeSpan.FromSeconds 30., 10) toKey

    /// Aggregate inputs and/or emit at any time.
    /// input: incoming tuple.
    /// out: do something with the output.
    /// using: provide context for the select.
    /// transform: transform the input using a context.
    let mkAggregator read (using, input, out) = 
        using (fun ctx -> 
                input |> read ctx out
                |> Async.RunSynchronously)

    open FSharp.Reflection

    /// Join aggregator for signals
    let mkSignalAggregator<'t> (signals:bigint list) =
        let reader = FSharpValue.PreComputeUnionTagReader typeof<'t>
        fun (input:'t) -> 
            let signal = (bigint 1) <<< (reader input)
            function
            | Some (vs:bigint list) -> signals |> List.zip vs |> List.map (fun (acc,target) -> target &&& (acc ||| signal))
            | _ -> signals |> List.map ((&&&) signal)
            >> fun agg -> (agg |> List.fold (fun all s -> all && (s>bigint.Zero)) true),agg
                
module Consumer =
    /// Create Ack*Nack pair for a queue spout.
    /// consumer: event stream consumer
    let toAcker (consumer:Consumer<_>) : Acker = 
        (uint64 >> consumer.Ack, uint64 >> consumer.Nack)

    /// Dispose of the consumer on Deactivation
    /// consumer: event stream consumer
    let deactivate (consumer:Consumer<_>) : unit = 
        (consumer :> IDisposable).Dispose()

module Spout =
    /// Create a queue spout.
    /// consumer: event stream consumer
    let ofConsumer toTuple (consumer:Consumer<_>) = 
        consumer.Get 1<FSharp.Data.UnitSystems.SI.UnitSymbols.s> 
        |> Option.map (fun r -> string r.id, toTuple r.msg)


[<Obsolete("Use `Bolt.mkPersister` instead.")>]
let createPersistBolt = Bolt.mkPersister

[<Obsolete("Use `Bolt.mkTransformer` instead.")>]
let createTransformBolt = Bolt.mkTransformer

[<Obsolete("Use `Consumer.toAcker` instead.")>]
let ackerOfConsumer = Consumer.toAcker


[<Obsolete("Use `Spout.ofConsumer` instead.")>]
let createQueueSpout = Spout.ofConsumer

[<Obsolete("Use `Bolt.mkNotifier` instead.")>]
let createNotifierBolt = Bolt.mkNotifier

[<Obsolete("Use `Bolt.mkJoinerWith` instead.")>]
let createJoinBoltWith = Bolt.mkJoinerWith


[<Obsolete("Use `Bolt.mkJoiner` instead.")>]
let createJoinBolt = Bolt.mkJoiner

[<Obsolete("Use `Bolt.mkAggregator` instead.")>]
let createAggregationBolt = Bolt.mkAggregator

[<Obsolete("Use `Bolt.mkTopicNotifier` instead.")>]
let createExchangeBolt = Bolt.mkTopicNotifier

[<Obsolete("Use `Bolt.mkSignalAggregator` instead.")>]
let signalAggregator<'t> = Bolt.mkSignalAggregator<'t>
            