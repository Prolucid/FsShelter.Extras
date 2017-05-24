(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../build_output"
#r "FsShelter.dll"
#r "FsShelter.Extras.dll"
#r "RabbitMq.Client.dll"
#r "FsBunny.dll"
#r "FsCassy.dll"
#r "Cassandra.dll"

(**
Bootstrapping the components
========================

The component constructors are defined in a way that delays instantiation (and consequently environmental requirements) till the moment it's actually needed.
What it means is that when implemented correctly, configuration, runtime dependencies, database availability, etc are not demanded when we are just inspecting the topology. For example: to document the topology or submit it for execution.
Only when Storm brings it up for execution do we need to have everything in place and available, hence the bootstrap parametrization.

Exmaple
========================

### Messaging
Let's boostrap EventStreams consumers and publishers with Json assembly:

*)
open FsBunny
open FsShelter.Extras.Assemblers

module RabbitMq =
    open System
    open RabbitMQ.Client
    let mkEventStreams (connectionString:string) =
        let uri = Uri(connectionString)
        let cf = ConnectionFactory(Uri = uri)
        RabbitMqEventStreams(cf, "amq.topic", 3us, 1000us)

let rabbit = "amqp://localhost"

/// Create a consumer based on the arguments.
let withQueueConsumer topic queue log cfg = 
    let eventStreams = RabbitMq.mkEventStreams rabbit :> EventStreams
    eventStreams.GetJsonConsumer (Persistent queue) (eventStreams.Routed topic)

/// Create a routed publisher based on the arguments.
let withPublisher publish log cfg = 
    let eventStreams = RabbitMq.mkEventStreams rabbit :> EventStreams
    fun tuple emit -> tuple, publish eventStreams

/// Publish Json
let publishJson exchange (eventStreams:EventStreams) (topic,msg) = 
    msg |-> eventStreams.UsingJsonPublisher (Routed (exchange,topic))

(**
### Persistence
Now let's bootstrap some Cassandra reader and writer components:

*)
module Cassandra =
  open FsCassy
  open Cassandra.Mapping

  let mkInterpreter mkSession (name:string) = 
    let session = mkSession name
    { new Interpreter with 
        member x.Interpret<'t,'r> (statement:Statement<'t,'r>) : 'r= 
          Cassandra.Interpreter.execute (fun _ -> Cassandra.Api.mkTable (MappingConfiguration()) session) statement }

  let mkSession (connectionString:string) =
    Cassandra.Api.mkCluster connectionString
    |> Cassandra.Api.mkSession id

let cassandra = "contact points=localhost;default keyspace=sample"

let withPersisterArgs log cfg =
    let persister = Cassandra.mkInterpreter Cassandra.mkSession cassandra
    fun input emit ->
        (fun f -> f persister), input

let withReaderArgs log cfg =
    let persister = Cassandra.mkInterpreter Cassandra.mkSession cassandra
    fun input emit ->
        (fun f -> f persister), input, emit



(**
### Implementing component logic

FsShelter provides the runtime, the Extras components outline the role of the component, and the bootstrap parameters wire the configured providers, but the user provides the logic.
Let's implement some of the components. We'll model some sensors, take the readings as messages coming in and raise an alarm by sending a message if the temperature readings exceed a threshold.
*)

open System
open FsCassy

[<AutoOpen>]
module Messaging =
  type SensorReading = 
    { Id : Guid; Temp : int}

  type SensorAlert = 
    { Id : Guid; Text : string }

[<AutoOpen>]
module Persistence =
  type Sensor = 
    { Id : Guid; Value : int}

type Stream =
    | Update of sensorId:Guid * temp:int
    | Alarm of sensorId:Guid * temp:int
    | NotFound of sensorId:Guid

let sensorWriter (persister:Interpreter) = 
    function 
    | Update(id, temp) -> 
            table<Sensor> 
        >>= where (Quote.X(fun s -> s.Id = id))
        >>= select (Quote.X(fun x -> { x with Value = temp }))
        >>= update >>= execute
            |> persister.Interpret

let thresholdAnalyser (persister:Interpreter) = 
    // realistically we'd use the ^ persister to read the threshold value from the database, but let's keep it simple
    function 
    | Update(id, temp) when temp > 100 -> [Alarm(id,temp)]
    | _ -> []
    >> async.Return


(**
### Topology

Finally, we put things together in a topology:

*)

open FsShelter.DSL
open FsShelter.Extras

let sampleTopology = topology "sample" {
    let sensorEvents = 
        createQueueSpout (fun (m:SensorReading) -> Update (m.Id, m.Temp))
        |> runReliableSpout (withQueueConsumer "Topics.Sensor.Started" "sensor_started") ackerOfConsumer

    let sensorWriter = 
        createPersistBolt sensorWriter
        |> runBolt withPersisterArgs 

    let thresholdAnalyser = 
        createTransformBolt thresholdAnalyser
        |> runBolt withReaderArgs 

    let alertNotifier = 
        createNotifierBolt (function Alarm (sensorId,temp) -> "Topics.Alarm", {SensorAlert.Id = sensorId; Text = sprintf "At %A the reactor went critical" DateTime.Now})
        |> runBolt (withPublisher <| publishJson "amq.topic")

    yield sensorEvents ==> sensorWriter |> shuffle.on Update
    yield sensorEvents ==> thresholdAnalyser |> shuffle.on Update

    yield thresholdAnalyser --> alertNotifier |> shuffle.on Alarm
}

(**
### Summary

This example outlined wiring of the persistence and messaging dependencies into the core logic of the application using FsShelter.Extra's components. 
The topology is available for inspection and instantiation w/o imposing the runtime requirements, such as configuration and connectivity. 
At the same time, core logic is concise, focused on the problem and testable in isolation.

*)
