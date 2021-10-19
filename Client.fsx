#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"

open Akka.FSharp
open Akka.Remote
open System
open System.Diagnostics
open System.Net
open System.Net.NetworkInformation
open System.Net.Sockets

type Param = {
    WordLen:int;
    ZeroLen:int
    }

type Result = {
    StationIdentifier:string;
    Miner:string; 
    ResultString:string
    HashResult:string
    CPU_time:int
    Real_time:int
    }
   
//--------------------------------------------------------------------------------------------------------
let localIP () =
    let networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                            |> Array.filter(fun iface -> iface.OperationalStatus.Equals(OperationalStatus.Up))

    let addresses = seq {
        for iface in networkInterfaces do
            for unicastAddr in iface.GetIPProperties().UnicastAddresses do
                yield unicastAddr.Address}

    let ipv4 = 
        addresses
        |> Seq.filter(fun addr -> addr.AddressFamily.Equals(AddressFamily.InterNetwork))
        |> Seq.filter(IPAddress.IsLoopback >> not)
        |> Seq.last    
    ipv4.ToString()
//let inputParam = fsi.CommandLineArgs
let myIP = localIP()
let serverIP = fsi.CommandLineArgs.[1]
//--------------------------------------------------------------------------------------------------------
// hashcoin compute methods
let GetHash (str: string) =
    let content = System.Text.Encoding.ASCII.GetBytes str
    let bytes = Security.Cryptography.HashAlgorithm.Create("SHA256").ComputeHash(content)
    let result = BitConverter.ToString(bytes).Replace("-", "").ToLower()
    result
// 1.generate a random string
let ranStr n = 
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in
    String(Array.init n (fun _ -> chars.[r.Next sz]))
// 2.check string matches the pattern or not
let CheckZeros (str : string) (zeros : int) (compare : string) = 
    (GetHash str).[0..zeros - 1] = compare
// 3.main function and main job for the actor
let Check (len : int) (zeros : int) = 
    let prefix = "yandi.ma"
    let mutable check = false
    let mutable str = ""
    let mutable comparezeros = ""
    for i = 1 to zeros do
        comparezeros <- comparezeros + "0"
    //printfn $"compare: {comparezeros}"
    while(not check) do
        str  <- prefix + (ranStr len)
        check <- CheckZeros str zeros comparezeros
    str
//--------------------------------------------------------------------------------------------------------
let configStr = 
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = " + myIP + "\n" +
                "port = 6666
            }
        }"
let config =
    Configuration.parse configStr
let system  = System.create "MineStation" config
//--------------------------------------------------------------------------------------------------------
// Actor which do the work
let minerActor (name:string)(No:int)(mailbox: Actor<_>) =
    let mutable wordLength = 0;
    let mutable zeroLength = 0;
    let sendInfo () = actor {
        let ownerRef = system.ActorSelection("akka.tcp://mine-system@"+serverIP+":8777/user/bitcoinOwner")
        ownerRef <! "MineStation:"+myIP+":6666:" + No.ToString()
    }
    sendInfo() |> ignore
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match box message with
        | :? Param as parameter ->
            let timer = new Stopwatch()
            wordLength <- parameter.WordLen
            zeroLength <- parameter.ZeroLen
            printfn $"{name} has received the task"
            let proc = Process.GetCurrentProcess()
            let cpu_time_stamp = proc.TotalProcessorTime
            timer.Start()
            let mutable result = Check wordLength zeroLength
            let cpu_time = (proc.UserProcessorTime - cpu_time_stamp).Milliseconds
            timer.Stop();
            let real_time = timer.Elapsed.Milliseconds
            let hashResult = GetHash result
            let identifier = myIP + ":6666"
            let sendMessage = {StationIdentifier = identifier ; Miner=name; ResultString=result; HashResult=hashResult; CPU_time = cpu_time; Real_time = real_time}
            let ownerRef = system.ActorSelection("akka.tcp://mine-system@"+serverIP+":8777/user/bitcoinOwner")
            ownerRef <! sendMessage
        | _ ->
           printfn "Unknown message"
        return! loop()
    }
    loop ()


for i in 1 .. 30 do
    let minerName = "Miner" + i.ToString()
    let bitcoinMiner1 = spawn system minerName <| minerActor minerName i
    let message = "Miner " + i.ToString() + " is ready"
    printfn $"{message}"
printfn $"Mine Station is ready to work"

Console.ReadKey() |> ignore