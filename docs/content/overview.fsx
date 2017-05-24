(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../build_output"
open System.Collections.Generic

(**
Namespaces
========================

FsShelter.Extras is organized into 3 parts designed to work together:

- Assembers - modules for packing and unpacking EventStream messages
- Components - functions for creating the most common topology components
- Deployment API - helps with packaging of the topology for deployment 


Assemblers
========================
FsShelter.Extras comes with several FsBunny assemblers to pack and unpack messages:

- Google Protobuf (v3) 
- FsPickler 
- JSON with Fable converter

And they are all availabe as extension methods on the EventStreams API.
*)

(**
FsShelter components
========================
Fundamentally FsShelter components are just mapping functions, taking something as input and potentially producing some output.
These are the common types of components we have identified and implemented in the library in a fairly general form:

* **Messaging spouts** <br />
Create and subscribe queues, convert messages into topology streams and handle processing guarantees.

* **Messaging bolts** <br />
Convert topology streams into messages and send them to destination exchanges.

* **Persistence bolts** <br />
Convert incoming tuples into persistence instructions.

* **Transformation bolts** <br />
Read something from the input, do something and potentially produce some output. Most bolts are transformation bolts.

* **Aggregation bolts** <br />
These are transformation bolts that may emit at any time, they typically perform some sort of aggregation over inputs before emitting.

* **Joiner bolts** <br />
Joiner is an aggregation bolt that is specifically tailored for joining multiple streams on some kind of key.

Additionally, one common type of a joiner bolt that has been identified is the Signal joiner. The term Signal is used in a sense than a certain event has occurred.
It can be useful in construction of logic Gates - bolts that ensure multiple conditions have been met before the processing can continue.

One thing to keep in mind about any delayed emits (i.e. aggregation and joiner bolts) is the topology-wide timeout: the maximum time a tuple emitted at the spout can spend unacknowledged while being processed in the topology.
Decide carefully if delayed emits need to fit into the processing guarantees or have to be set as unachored.

It's up to the user to parametrize these components with how specifically these actions are performed or if a particular component is suitable at all.
For details see the API reference.

*)


(** 
Deployment module
========================
Deployment module helps with packaging of common .NET build artifacts and execution of the application as a Storm topology.
*)
