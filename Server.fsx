#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"

open Akka.FSharp
open Akka.Actor
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

let zeros = fsi.CommandLineArgs.[1] |> int
let serverIP = localIP()
//--------------------------------------------------------------------------------------------------------
// create the config for communitaion
let configStr = 
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = " + serverIP + "\n" +
                "port = 8777
            }
        }"
let config =
    Configuration.parse configStr
let system = System.create "mine-system" config
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
// Actor which assign work
let ownerActor (wordLength :int)(zeroLength :int)(mailbox: Actor<_>) = 
    let containsNumber number list = List.exists(fun i -> i=number) list
    let mutable mineStation = [];
    let mutable currentlength = 0;
    let rec loop () = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? string as msg ->
            let minerParam = msg.Split(":")
            let mineStationName = minerParam.[0]
            let mineStationIP = minerParam.[1]
            let minerPort = minerParam.[2]
            let minerNo = minerParam.[3]
            currentlength <- currentlength + 1
            printfn $"No.{minerNo} miner at {mineStationIP} asks for work"
            let minerStr = "akka.tcp://"+mineStationName+"@"+mineStationIP+":"+minerPort.ToString()+"/user/Miner"+ minerNo
            //printfn $"{minerStr}"
            let minerRef = system.ActorSelection(minerStr)
            let name = minerRef.ToString()
            printfn $"{name}"
            let paramMessage = {WordLen = currentlength; ZeroLen=zeroLength}
            minerRef <! paramMessage 
        | :? Result as coin ->
            let miner = coin.Miner
            let result = coin.ResultString
            let hashResult = coin.HashResult
            let cpu_time = coin.CPU_time
            let real_time = coin.Real_time
            let identifier = coin.StationIdentifier
            let minerInfo = identifier + ": " + miner
            printfn $"Get result from {minerInfo}"
            printfn $"{result}"
            printfn $"{hashResult}"
            printfn $"CPU time: {cpu_time}"
            printfn $"Real time: {real_time}"
        // check whether boss is alive
        | _ ->
            printfn "Owner Unknown message"
        return! loop ()
    }
    loop ()
//--------------------------------------------------------------------------------------------------------
// Actor which do the work
let minerActor (name:string)(mailbox: Actor<_>) =
    let mutable wordLength = 0;
    let mutable zeroLength = 0;
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
            let identifier = localIP()+":8777"
            let sendMessage = {StationIdentifier = identifier; Miner=name; ResultString=result; HashResult=hashResult; CPU_time = cpu_time; Real_time = real_time}
            let ownerRef = system.ActorSelection("akka.tcp://mine-system@" + serverIP + ":8777/user/bitcoinOwner")
            ownerRef <! sendMessage
        | _ ->
           printfn "Unknown message"
        return! loop()
    }
    loop ()
//--------------------------------------------------------------------------------------------------------
let bicoinOwner = spawn system "bitcoinOwner" <| ownerActor 10 zeros
for j in 1 .. 30 do
    // Mine:矿场IP地址:8777:工人ID
    let workNo = "mine-system:"+serverIP+":8777:"+ j.ToString()
    let minerName = "Miner" + j.ToString()
    let bitcoinMiner = spawn system minerName <| minerActor minerName
    printfn $"{bitcoinMiner.ToString()}"
    bicoinOwner <! workNo

Console.ReadKey() |> ignore
// let mineStationIp = [|"192.168.0.98"; "192.168.0.180"; "192.168.0.196"|]
// for i in 1 .. mineStationIp.Length do 
//     for j in 1 .. 30 do
//         // Mine:矿场号:矿场IP地址:9001:工人ID
//         let workNo = "Mine:"+i.ToString()+":"+mineStationIp.[i-1]+":9001:"+ j.ToString()
//         bicoinOwner <! workNo