namespace FsShelter.Extras.Assemblers

/// Protobuf assembly
[<AutoOpen>]
module ProtoAssembly =
    open Google.Protobuf
    open FsBunny

    /// disassemble the message into RMQ primitives
    /// exchange: destination exchange
    /// message: message to disassemble
    let disassembler exchange (message:#IMessage<'T>) =
        exchange, None, message.ToByteArray()

    /// assemble a message from RMQ primitives
    /// parser: Protobuf message parser
    let assembler<'T when 'T :> IMessage<'T> and 'T:(new:'T)> 
            (parser : MessageParser<'T>) (_,_,bytes:byte[]) =
        parser.ParseFrom(bytes)

    type FsBunny.EventStreams with 
        /// Construct a consumer, using specified message type, queue, the exchange to bind to and Proto assember.
        member this.GetProtoConsumer<'T when 'T :> IMessage<'T> and 'T:(new:'T)> (queue: Queue) (exchange:Exchange) : Consumer<'T> =
            let parser = MessageParser<'T>(fun () -> new 'T())
            this.GetConsumer<'T> queue exchange (assembler parser)

        /// Construct a publisher for the specified message type using Proto disassembler.
        member this.GetProtoPublisher<'T when 'T :> IMessage<'T>> (exchange: Exchange) : Publisher<'T> = 
            this.GetPublisher<'T> (disassembler exchange)

        /// Use a publisher with a continuation for the specified message type using Proto disassembler.
        member this.UsingProtoPublisher<'T when 'T :> IMessage<'T>> (exchange: Exchange) (cont:Publisher<'T> -> unit) : unit =
            this.UsingPublisher<'T> (disassembler exchange) cont