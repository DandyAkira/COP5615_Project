#r "nuget: Akka.Fsharp"
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"

open Akka.FSharp
open Akka.Actor
open System
open System.Diagnostics
open System.Net
open System.Net.NetworkInformation
open System.Net.Sockets
open System.Threading 
// get IP address
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
let clientIP = localIP()
// config for twitter
let configStr = 
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = " + clientIP + "\n" +
                "port = 8400
            }
        }"
let config =
    Configuration.parse configStr    
let system = System.create "users" config
let engineIP = fsi.CommandLineArgs.[1]
let rand = Random()
// Tweet
type Tweet(userName:string, twtCont:string, originalSender:string) = 
    member this.userName = userName
    member this.twtCont = twtCont
    member this.originalSender = originalSender
// --------------------------------------------------------------------------------------------------------
// communication protocals
// GeneralMsg
type GeneralMsg = {
    content:string
}
// AccountMessage
type AccountMsg = {
    UserName:string; 
    Password:string;
    IPAndPort:string;
    ActorName:string;
}
// AccountCreateMsg
type AccountCreateMsg = {
    AccountMessage:AccountMsg
}
// SendTwtMsg
type SendTwtMsg = {
    AccountMessage:AccountMsg
    Tweet:Tweet;
}
// SubscribeMsg
type SubscribeMsg = {
    AccountMessage:AccountMsg
    SubscribeeName:string
}
// HashTagQuery
type HashTagQuery = {
    AccountMessage:AccountMsg
    HashTag:string
}
// SubMentQuery
type SubMentQuery = {
    AccountMessage:AccountMsg
    QueryType:string
}
// QueryResp
type QueryResp = {
    RelatedTwt:Tweet list
}

//SubscribeReq
type SubscribeReq = {
    SubscribeeName:string
}


let messageProcessor = system.ActorSelection("akka.tcp://twitter-engine@"+engineIP+":8777/user/messageProcessor")
let Client (actorName:string) (userName:string) (password:string) (mailbox: Actor<_>) =
    let userName = userName
    let acotrName = actorName
    let accountMessage = {UserName=userName; Password=password; IPAndPort=clientIP+":8400"; ActorName=actorName}
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match box message with
        | :? int as order ->
            //account create
            if order=1 then
                messageProcessor <! {AccountMessage=accountMessage}
            //account login
            else if order=2 then 
                messageProcessor <! accountMessage
            else if order=3 then 
                messageProcessor <! {AccountMessage=accountMessage; HashTag="hello"}
            else if order=4 then 
                messageProcessor <! {AccountMessage=accountMessage; QueryType="mention"}
            else if order=5 then 
                messageProcessor <! {AccountMessage=accountMessage; QueryType="subscribe"}
        | :? string as twtCont ->
            let tweet = new Tweet(userName,twtCont,userName) 
            messageProcessor <! {AccountMessage=accountMessage; Tweet=tweet}
        | :? GeneralMsg as generalMsg ->
            let content = generalMsg.content
            printfn $"{actorName} received: {content}"
        | :? SubscribeReq as subscribeReq ->
            let subscribeeName = subscribeReq.SubscribeeName
            messageProcessor <! {AccountMessage=accountMessage; SubscribeeName=subscribeeName}
        | :? QueryResp as queryResp ->
            let relatedList = queryResp.RelatedTwt
            for idx in 1..relatedList.Length do
                let tweet = relatedList.[idx-1]
                if tweet.originalSender = tweet.userName then
                    printfn $"{userName}"
                    printfn $"receive a origin tweet from {tweet.userName}"
                    printfn $"{tweet.twtCont}"
                else
                    printfn $"{userName}" 
                    printfn $"receive a retweet which origins from {tweet.originalSender} from {tweet.userName}"
                    printfn $"{tweet.twtCont}"
                Console.WriteLine()
            
        | :? Tweet as tweet ->
            let content = tweet.twtCont
            Console.WriteLine()
            printfn $"{userName}"
            if tweet.originalSender = tweet.userName then           
                printfn $"receive a origin tweet from {tweet.userName}"
                printfn $"{tweet.twtCont}"            
                // if rand.Next(1,11)>5 then
                //     Console.WriteLine("Retweeting this tweet")
                //     messageProcessor <! {AccountMessage=accountMessage; Tweet=Tweet(userName, content, tweet.originalSender)}
            else  
                printfn $"receive a retweet which origins from {tweet.originalSender} from {tweet.userName}"
                printfn $"{tweet.twtCont}"
            Console.WriteLine()
        | _ ->
            printfn "Unknown message"
        return! loop()
    }
    loop()

// test 1
// create 3 users
// each user send 5 tweets which contain 3 different hashtags and each tweet mention others
// each user subscribe to other 2 users
// let client1 = spawn system "client1" <| Client "client1" "pipikai" "123456"
// client1 <! 1
// client1 <! 2

// let client2 = spawn system "client2" <| Client "client2" "pipisun" "123456"
// client2 <! 1
// client2 <! 2

// let client3 = spawn system "client3" <| Client "client3" "pipizhong" "123456"
// client3 <! 1
// client3 <! 2

// client1 <! {SubscribeeName="pipisun"}
// client1 <! {SubscribeeName="pipizhong"}

// client2 <! {SubscribeeName="pipikai"}
// client2 <! {SubscribeeName="pipizhong"}

// client3 <! {SubscribeeName="pipisun"}
// client3 <! {SubscribeeName="pipikai"}


// client1 <! "pipizhong hello @pipizhong #hello ";;
// client1 <! "pipisun hello @pipisun #hello ";;    

// client2 <! "pipikai hello @pipikai #hello ";;
// client2 <! "pipizhong hello @pipizhong #hello ";;

// client3 <! "pipikai hello @pipikai #hello ";;    
// client3 <! "pipisun hello @pipisun #hello ";;

// test case 2
// load test
let mutable clientList = []

for i in 1 .. 101 do
    let client = spawn system ("client_"+ (string i)) <| Client ("client_"+(string i)) ("user_" + (string i)) "123123"
    clientList <- List.append clientList [client]
    client <! 1
    client <! 2
    Thread.Sleep(50)

for eachClient in clientList do
    let id = eachClient.Path.Name.Substring 7 |> int
    if id > 1 then
        eachClient <! {SubscribeeName = "user_1"} // user_1 have 100 followers
    if id > 50 then
        eachClient <! {SubscribeeName = "user_2"} // user_2 have 50 followers (1/2)
    if id > 67 then
        eachClient <! {SubscribeeName = "user_3"} // user_3 have 34 followers (1/3)
    if id > 75 then
        eachClient <! {SubscribeeName = "user_4"} // user_4 have 25 followers (1/4)
    if id > 80 then
        eachClient <! {SubscribeeName = "user_5"} // user_5 have 20 followers (1/5)
    if id > 83 then
        eachClient <! {SubscribeeName = "user_6"} // user_6 have 17 followers (1/6)
    if id > 86 then
        eachClient <! {SubscribeeName = "user_7"} // user_7 have 14 followers (1/7)
    if id > 88 then
        eachClient <! {SubscribeeName = "user_8"} // user_8 have 12 followers (1/8)
    if id > 89 then
        eachClient <! {SubscribeeName = "user_9"} // user_9 have 11 followers (1/9)
    if id > 90 then
        eachClient <! {SubscribeeName = "user_10"} // user_4 have 10 followers (1/10)
    Thread.Sleep(50)


for i in 0..9 do
    clientList.[i] <! "user_" + (string (i+1)) + " saying hello to twitter"
    Thread.Sleep(50)
    


Console.WriteLine("simulation done, press any key to exit")
Console.ReadKey()|>ignore
