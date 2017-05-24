(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../build_output"

(**
FsShelter.Extras
======================
The opinionated library of common components for building event-processing topologies with [FsShelter](https://prolucid.github.io/FsShelter) and [FsBunny](https://prolucid.github.io/FsBunny).
Together with Apache Storm, RabbitMQ and Cassandra these components provide a fault-tolerant and highly scalable platform for programming distributed systems with F#.


Installing
======================

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The FsShelter.Extras library can be <a href="https://nuget.org/packages/FsShelter.Extras">installed from NuGet</a>:
      <pre>paket add nuget FsShelter.Extras</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>


Motivation and capabilities
-------
The library is for developers who want to use F# to write applications that can scale linearly and have predictable uptime and performance characteristics, while running under minimal supervision.
When we say "scale" what we really we mean:

- the system as a whole is at least partially available,
- performs the required function, 
- has the predictable timeframe for handling the incoming messages,
- can handle a larger load if needed given more resources.

#### Apache Storm
Think of Apache Storm as a distributed operating system designed for running programs that continiously process discrete events using a cluster of cheap machines.
The core value of Apache Storm comes from the granular processing guarantees that are provided without touching the storage, therefore avoiding very high latency that usually comes with it.
Storm will take care of distributing the program throughout the cluster, will monitor its performance and will redistribute the load if required.

#### Apache Cassandra
The persistence components implemented here don't have a dependency on [FsCassy](https://prolucid.github.io/FsCassy) and can be used with any database technology, but use of Cassandra is highly complementary.
It features a statically-typed API with consistently low latency in combination with tunable guarantees required for always-on systems. 
At the same time, the "materialized views" Cassandra encourages are easily maintained with FsShelter components.

#### RabbitMQ
RabbitMQ is a swiss knife of distributed messaging:

- it talks in variety of protocols right out of the box,
- it enables a variety of messaging patterns, 
- it runs on top of the time-tested Erlang runtime,
- and it supports clustering, mirroring and failover,
- it runs on a laptop, an in-house datacenter or any cloud vendor your customers might prefer.

[FsBunny](https://prolucid.github.io/FsBunny) implements the publisher and consumer API we use for our messaging components.

 
Contributing and copyright
--------------------------
The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. 

The library is available under Apache license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/Prolucid/FsShelter.Extras/tree/master/docs/content
  [gh]: https://github.com/Prolucid/FsShelter.Extras
  [issues]: https://github.com/Prolucid/FsShelter.Extras/issues
  [readme]: https://github.com/Prolucid/FsShelter.Extras/blob/master/README.md
  [license]: https://github.com/Prolucid/FsShelter.Extras/blob/master/LICENSE.md


Copyright 2017 Prolucid Technologies Inc
*)
