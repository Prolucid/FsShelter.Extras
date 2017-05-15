namespace FsShelter.Extras.Assemblers

/// Binary FsPickler assembly

[<AutoOpen>]
module PicklerAssembly =
    open MBrace.FsPickler
    open System.IO
    open FsBunny

    let private serializer = FsPickler.CreateBinarySerializer()

    /// disassemble a message into RMQ primitives
    /// exchange: destination exchange
    /// item: message
    let disassembler exchange item =
        use ms = new MemoryStream()
        serializer.Serialize(ms, item)
        exchange, None, ms.GetBuffer()

    /// assemble a message from RMQ primitives
    let assembler (topic,properties,bytes:byte[]) =
        use ms = new MemoryStream(bytes)
        serializer.Deserialize(ms)

    type FsBunny.EventStreams with 
        /// Construct a consumer, using specified message type, queue, the exchange to bind to and Pickler assember.
        member this.GetPicklerConsumer<'T> (queue: Queue) (exchange:Exchange) : Consumer<'T> =
            this.GetConsumer<'T> queue exchange assembler

        /// Construct a publisher for the specified message type using Pickler disassembler.
        member this.GetPicklerPublisher<'T> (exchange: Exchange) : Publisher<'T> = 
            this.GetPublisher<'T> (disassembler exchange)

        /// Use a publisher with a continuation for the specified message type using Pickler disassembler.
        member this.UsingPicklerPublisher<'T> (exchange: Exchange) (cont:Publisher<'T> -> unit) : unit =
            this.UsingPublisher<'T> (disassembler exchange) cont