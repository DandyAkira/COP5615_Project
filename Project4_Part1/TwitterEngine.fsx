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


// --------------------------------------------------------------------------------------------------------
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
let serverIP = localIP()
Console.WriteLine("-------------------------------------------------------------------------")
printfn $"Engine IP: {serverIP}"
Console.WriteLine("Please enter it as the parameter of client simulator")
Console.WriteLine("-------------------------------------------------------------------------")
// config for twitter
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
let system = System.create "twitter-engine" config
let rand = Random()
// --------------------------------------------------------------------------------------------------------
// Tweet
type Tweet(userName:string, twtCont:string, originalSender:string) = 
    member this.userName = userName
    member this.twtCont = twtCont
    member this.originalSender = originalSender
// User
type User(userName:string, password:string, actorName:string, IPAndPort:string) =
    // 订阅的人
    let mutable subscribeeList = List.empty
    // 被哪些人订阅
    let mutable subscriberList = List.empty
    // 发送过的推特
    let mutable tweetList = List.empty
    let mutable tweetNum = 0
    // 被哪些推特提及
    let mutable mentionList = List.empty
    member this.userName = userName
    member this.password = password
    member this.actorName = actorName
    member this.IPAndPort = IPAndPort
    member this.addSubscribee(userName:string)=
        if not (List.contains userName subscribeeList) then
            subscribeeList <- List.append subscribeeList [userName]
    member this.getSubscribees() =
        subscribeeList
    member this.addSubscriber(userName:string)=
        if not (List.contains userName subscriberList) then
            subscriberList <- List.append subscriberList [userName]
    member this.getSubscribers() =
        subscriberList
    member this.addTweet(tweetId:string) =
        tweetList <- List.append tweetList [tweetId]
        tweetNum <- tweetNum+1
    member this.getTweetNum() = 
        tweetNum
    member this.getTweets() =
        tweetList
    member this.addMention(tweetId:string) =
        mentionList <- List.append mentionList [tweetId]
    member this.getMentions() =
        mentionList
// twitter engine
type Twitter() = 
    // 所有的用户
    let mutable users = new Map<string,User>([])
    // 所有的tweet
    let mutable tweets = new Map<string, Tweet>([])
    // hashtag分门别类
    let mutable hashTags = new Map<string, string list>([])
    // 在线用户列表
    let mutable aliveUsers = new System.Collections.Generic.List<string>()
    
    member this.addLiveUser (userName:string) = 
        aliveUsers.Add(userName)
    member this.removeLiveUser (userName:string) = 
        aliveUsers.Remove(userName)

    // 打印twitter所有状况（已废弃）
    // member this.printAll () = 
    //     printfn "1the following are the user"
    //     for KeyValue(userName,user) in users do
    //         let password = user.password
    //         printfn $"{userName}, {password}"
    //         printfn "1.1.subscribe to"
    //         let subscribeList = user.getSubscribees()
    //         for idx in 1..subscribeList.Length do
    //             let subscribeeName = subscribeList.[idx-1]
    //             printfn $"{subscribeeName}"
    //         printfn "1.2.tweet"
    //         let tweetist = user.getTweets()
    //         for idx in 1..tweetist.Length do
    //             let twtCont = tweetist.[idx-1]
    //             printfn $"{twtCont}"
    //         printfn "1.3.tweet that mentions"
    //         let mentionList = user.getMentions()
    //         for idx in 1..mentionList.Length do
    //             let twtCont = mentionList.[idx-1]
    //             printfn $"{twtCont}"
    //     // printfn "2.the following are the tweets"
    //     // for KeyValue(tweetId,tweet) in tweets do
    //     //     printfn $"{tweetId}"
    //     //     printfn $"{tweet}"
    //     printfn "3.the following are the hashTags"
    //     for KeyValue(hashTag,tweets) in hashTags do
    //         printfn $"{hashTag}"
    //         for idx in 1..tweets.Length do
    //             let twtCont = tweets.[idx-1]
    //             printfn $"{twtCont}"

    // 新用户注册
    member this.addUser (userName:string, password:string, actorName:string, IPAndPort:string) = 
        if users.ContainsKey(userName) then
            false
        else
            let user = new User(userName, password, actorName, IPAndPort)
            users <- users.Add(user.userName, user)
            true
    
    // 用户发送推特
    member this.addTweet (userName:string,tweet:Tweet) =
        let user = users.[userName]
        let tweetNum = user.getTweetNum()
        let tweetId = userName + (string (tweetNum+1))
        let twtCont = tweet.twtCont
        tweets <- tweets.Add(tweetId, tweet)
        user.addTweet(tweetId)

        // 对推特内容解析
        let mutable informList = new System.Collections.Generic.List<User>()
        let mentstartPtr = twtCont.IndexOf("@")
        if not (mentstartPtr = -1) then
            let mentendPtr = twtCont.IndexOf(" ",mentstartPtr)
            let mention = twtCont.[mentstartPtr+1..mentendPtr-1]
            if users.ContainsKey(mention) then
                let user = users.[mention]
                user.addMention(tweetId)
                if aliveUsers.Contains(mention) then
                    let mentionUser = users.[mention]
                    informList.Add(mentionUser)
        
        let hashstartPtr = twtCont.IndexOf("#")
        if not (hashstartPtr = -1) then
            let hashendPtr = twtCont.IndexOf(" ",hashstartPtr)
            let hashtag = twtCont.[hashstartPtr+1..hashendPtr-1]
            let mutable map = hashTags
            if map.ContainsKey(hashtag)=false then
                let l = List.empty: string list
                map <- map.Add(hashtag, l)
            let tweetIds = map.[hashtag]
            map <- map.Add(hashtag, List.append tweetIds [tweetId])
            hashTags <- map
        
        // 获取需要通知的用户
        let subscribersName = user.getSubscribers()
        for subscriberName in subscribersName do 
            if aliveUsers.Contains(subscriberName) then
                let subscriber = users.[subscriberName]
                if not (informList.Contains(subscriber)) then
                    informList.Add(subscriber)
        informList

    // 用户订阅
    member this.addSubscribe (subscriberName:string, subscribeeName:string) =
        if users.ContainsKey(subscribeeName) then 
            let subscriber = users.[subscriberName]
            subscriber.addSubscribee(subscribeeName)
            let subscribee = users.[subscribeeName]
            subscribee.addSubscriber(subscriberName)
            true
        else
            false

    // 身份识别
    member this.authenticate (userName:string, password:string) =
        if users.ContainsKey(userName) then
            let user = users.[userName]
            if password=user.password then
                true
            else
                false
        else
            false
    // tag查询
    member this.queryHashTag (hashTag:string) = 
        if hashTags.ContainsKey(hashTag) then
            let tweetIds = hashTags.[hashTag]
            this.idMapTweet(tweetIds)
        else 
            List.empty: Tweet list

    // mention查询
    member this.queryMention (userName:string) = 
        let user = users.[userName]
        let tweetIds=user.getMentions()
        this.idMapTweet(tweetIds)
    
    // 订阅查询
    member this.querySubscribe (userName:string) = 
        let user = users.[userName]
        let subscribeeNames = user.getSubscribees()
        let mutable resultTweets = List.empty
        for idx in 1..subscribeeNames.Length do
            let subscribeeName = subscribeeNames.[idx-1]
            let subscribee = users.[subscribeeName]
            let subscribeeTweets = subscribee.getTweets()
            resultTweets <- List.append resultTweets subscribeeTweets
        this.idMapTweet(resultTweets)
    
    // 工具方法（tweetId to tweet）
    member this.idMapTweet (tweetIds:list<string>) = 
        let mutable res = List.empty: Tweet list
        for tweetId in tweetIds do
            let tweet = tweets.[tweetId]
            res <- List.append res [tweet]
        res

let twitter = new Twitter()
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
// AccountLogOutMsg
type AccountLogOutMsg = {
    userName:string
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
// --------------------------------------------------------------------------------------------------------
// Message Processor
let MessageProcessor = 
    spawn system "messageProcessor"
            (fun mailbox ->
                let rec loop () = actor {
                    let! message = mailbox.Receive()
                    match box message with
                    // 登陆请求
                    | :? AccountMsg as accountMsg ->
                        let userName = accountMsg.UserName
                        let password = accountMsg.Password
                        let actorName = accountMsg.ActorName
                        let IPAndPort = accountMsg.IPAndPort
                        let requesterRef = system.ActorSelection("akka.tcp://users@" + IPAndPort + "/user/"+actorName)
                        
                        let result = twitter.authenticate(userName, password)
                        if result then
                            twitter.addLiveUser(userName)
                            printfn $"{userName} Successfully log in"
                            requesterRef<! {content="Successfully log in"}
                        else 
                            requesterRef<! {content="Wrong username or password"}
                    // 注册请求
                    | :? AccountCreateMsg as accountCreateMsg ->
                        let userName = accountCreateMsg.AccountMessage.UserName
                        let password = accountCreateMsg.AccountMessage.Password
                        let actorName = accountCreateMsg.AccountMessage.ActorName
                        let IPAndPort = accountCreateMsg.AccountMessage.IPAndPort
                        let requesterRef = system.ActorSelection("akka.tcp://users@" + IPAndPort + "/user/"+actorName)

                        let result = twitter.addUser(userName, password,actorName,IPAndPort)
                        if result then
                            printfn $"{userName} has Successfully created"
                            requesterRef<! {content="Successfully create"}
                        else
                            printfn $"{userName} has FAILED created" 
                            requesterRef<! {content="The username has been used"}
                    //离线通知
                    | :? AccountLogOutMsg as accountLogOutMsg ->
                        twitter.removeLiveUser(accountLogOutMsg.userName) |> ignore
                        Console.WriteLine(accountLogOutMsg.userName + " has quited")
                    //Tag查询请求
                    | :? HashTagQuery as hashTagQuery -> 
                        let accountMsg = hashTagQuery.AccountMessage
                        let userName = accountMsg.UserName
                        let password = accountMsg.Password
                        let authenticateResult = twitter.authenticate(userName, password)
                        if authenticateResult then
                            let result = twitter.queryHashTag(hashTagQuery.HashTag)
                            let IPAndPort = hashTagQuery.AccountMessage.IPAndPort
                            let actorName = hashTagQuery.AccountMessage.ActorName
                            let requesterRef = system.ActorSelection("akka.tcp://users@" + IPAndPort + "/user/"+actorName)
                            requesterRef <! {RelatedTwt=result}
                    //订阅以及mention请求
                    | :? SubMentQuery as subMentQuery ->
                        let accountMsg = subMentQuery.AccountMessage
                        let userName = accountMsg.UserName
                        let password = accountMsg.Password
                        let authenticateResult = twitter.authenticate(userName, password)
                        if authenticateResult then
                            let queryType = subMentQuery.QueryType
                            let IPAndPort = accountMsg.IPAndPort
                            let actorName = accountMsg.ActorName
                            let requesterRef = system.ActorSelection("akka.tcp://users@" + IPAndPort + "/user/"+actorName)
                            if queryType="subscribe" then
                                let result1 = twitter.querySubscribe(userName)
                                requesterRef <! {RelatedTwt=result1}
                            else if queryType="mention" then
                                let result2 = twitter.queryMention(userName)
                                requesterRef <! {RelatedTwt=result2}
                    //发送推特请求
                    | :? SendTwtMsg as sendTwtMsg ->
                        let accountMsg = sendTwtMsg.AccountMessage
                        let tweet = sendTwtMsg.Tweet
                        let userName = accountMsg.UserName
                        let password = accountMsg.Password
                        let result = twitter.authenticate(userName, password)
                        if result then
                            let informList = twitter.addTweet(userName,tweet)
                            Console.WriteLine()
                            if tweet.originalSender=userName then
                                printfn $"receive a origin tweet from {userName}"
                                printfn $"{tweet.twtCont}"                          
                            else 
                                printfn $"{userName} has retweeted a tweet of {tweet.originalSender}"
                                printfn $"{tweet.twtCont}"
                            Console.WriteLine()
                            if informList.Capacity > 0 then
                                Console.Write("Informing: ")
                                for user in informList do
                                    Console.Write(user.userName+" ")
                                    let IPAndPort = user.IPAndPort
                                    let actorName = user.actorName
                                    let informerRef = system.ActorSelection("akka.tcp://users@" + IPAndPort + "/user/"+actorName)
                                    informerRef <! tweet
                    // 订阅请求
                    | :? SubscribeMsg as subscribeMsg ->
                        let accountMsg = subscribeMsg.AccountMessage
                        let userName = accountMsg.UserName
                        let password = accountMsg.Password
                        let subscribeeName = subscribeMsg.SubscribeeName
                        let result = twitter.authenticate(userName, password)
                        if result then
                            let actorName = accountMsg.ActorName
                            let IPAndPort = accountMsg.IPAndPort
                            let result = twitter.addSubscribe(userName,subscribeeName)
                            let requesterRef = system.ActorSelection("akka.tcp://users@" + IPAndPort + "/user/"+actorName)
                            if result then
                                let msg = $"Successfully subscribe to {subscribeeName}!"
                                requesterRef<! {content=msg}
                            else 
                                requesterRef<! {content="The target user does not exist!"}        
                    | _ ->
                        printfn "Unknown message"
                    return! loop()
                }
                loop())

while true do
    ignore()