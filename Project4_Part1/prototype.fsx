#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp
open System.Threading 

let system = ActorSystem.Create("Twitter")

type PWD = {
    clientID : int
    status : string
    pwd : string
}

type Notice = {
    note : string
}

type Tweet = {
    clientID : int
    tags : List<string>
    mentions : List<string>
    content : string
    time: DateTime
}

type wanttofollow = {
    clientID:int
    followID:int
}

type Message = 
    | RequestLoginPage of clientID:int
    | LoginPage
    | Login of clientID:int
    | Register of clientID:int
    | InputPwdToLogin of Notice
    | InputPwdToRegister of Notice
    | Password of PWD
    | Notice of Notice
    | RequestHomePage of clientID:int
    | HomePage
    | SendTweet of clientID:int
    | RequestTweet
    | NewTweet of Tweet
    | ReadTweets of clientID:int
    | MySubscriptions
    | MyFollowers
    | DisplayTweet of List<Tweet>
    | WantToFollow of wanttofollow
    | FollowSuccess of followID:int
    | AddFollower of clientID:int



//-----------------------------------------Engine Actor------------------------------------

let rec remove i l =
    match i, l with
    | 0, x::xs -> xs
    | i, x::xs -> x::remove (i - 1) xs
    | i, [] -> failwith "index out of range"


let Engine (mailbox:Actor<Message>) = 
    let mutable clientList = []
    let mutable pwdList = []
    let mutable tweetMap = Map.empty
    let mutable displayList = []
    printfn $"Twitter engine running"
    let rec loop () = actor{
        let! msg = mailbox.Receive()
        match msg with
        //---------------------登录与注册模块------------------------------------
        //用户请求登录页
        | RequestLoginPage clientID ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string clientID))
            client <! LoginPage
        
        | RequestHomePage clientID ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string clientID))
            client <! HomePage
        
        //用户请求登录
        | Login clientID ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string clientID))
            if List.contains clientID clientList then
                client <! InputPwdToLogin {note = "Please Enter Password"}
            else
                client <! InputPwdToRegister {note = "You haven't Register, Register now, please enter your Password"}
        
        //用户请求注册
        | Register clientID ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string clientID))
            if List.contains clientID clientList then
                client <! InputPwdToLogin {note = "You already have registered, please enter the password to login"}
            else
                client <! InputPwdToRegister {note = "Please enter the password to register"}
        
        //用户输入密码
        | Password tmp ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string tmp.clientID))

            //密码用于登录
            if tmp.status = "to login" then 
                let index = List.findIndex (fun elem -> elem = tmp.clientID) clientList
                if (List.item index pwdList) = tmp.pwd then
                    client <! Notice {note = "Password Correct"}
                    client <! HomePage
                else
                    client <! InputPwdToLogin {note = "Password Wrong, please try again"}
            //密码用于注册
            else if tmp.status = "to register" then
                clientList <- clientList @ [tmp.clientID]
                pwdList <- pwdList @ [tmp.pwd]
                //client <! Notice {note = "Register Successfully"}
                client <! Notice {note = "Register Successfully"}
                client <! LoginPage
            
                else
                    printfn $"engine received unknown meaning of password info"

        //用户想要发推
        | SendTweet clientID -> 
            let client = system.ActorSelection("akka://Twitter/user/client" + (string clientID))
            client <! Notice {note = "Please write tweet\n
                                    !! Important !! start a new line and type ''!send'' to send the tweet!!\n
                                    Note: use '#' to mark tag; use '@' to mark mention"}
            client <! RequestTweet

        //收到用户的推文
        | NewTweet temp ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string temp.clientID))
            tweetMap <- tweetMap.Add(temp.clientID, temp)
            if List.contains temp.clientID displayList then
                let index = List.findIndex (fun e-> e = temp.clientID) displayList
                displayList <- remove index displayList
            displayList <- [temp.clientID] @ displayList
            client <! Notice {note = "New Tweet Posted Successfully!"}
            client <! HomePage
        
        //用户想要看推文
        | ReadTweets clientID ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string clientID))
            let mutable tweetList = []
            for each in displayList do
                tweetList <- tweetList @ [Map.find each tweetMap]
            client <! DisplayTweet tweetList
        
        | WantToFollow temp ->
            let client = system.ActorSelection("akka://Twitter/user/client" + (string temp.clientID))
            if List.contains temp.followID clientList then
                system.ActorSelection("akka://Twitter/user/client" + (string temp.followID)) <! AddFollower temp.clientID
                client <! FollowSuccess temp.followID
            else
                client <! Notice {note="Follow Failed, No such person"}
                Thread.Sleep(300)
                client <! HomePage
                

        | _ -> printfn $"Engine got unknown message"

        return! loop()
    }
    loop()



//-----------------------------------------Client Actor------------------------------------
let Login_Page = "(1) Login\n(2) Register\n(3) Quit"
let Home_Page = "(1) Send Tweet\n(2) Read Tweets\n(3) My Subscriptions\n(4) My Followers\n(5) Quit"

let FindTags (content:string) = 
    let mutable tags = []
    for s = 0 to content.Length-1 do
        if content.Chars(s) = '#' then
            let mutable e = s+1
            let mutable tag = ""
            while e<content.Length && content.Chars(e)<>' ' && content.Chars(e)<>'\n' do
                tag <- tag + content.Chars(e).ToString()
                e <- e+1
            tags <- tags @ [tag]
    tags

let FindMentions (content:string) = 
    let mutable mentions = []
    for s = 0 to content.Length-1 do
        if content.Chars(s) = '@' then
            let mutable e = s+1
            let mutable mention = ""
            while e<content.Length && content.Chars(e)<>' ' && content.Chars(e)<>'\n' do
                mention <- mention + content.Chars(e).ToString()
                e <- e+1
            mentions <- mentions @ [mention]
    mentions

let Client (id:int) (mailbox:Actor<Message>) = 
    printfn $"Twitter client running"
    let mutable subscribeList = []
    let mutable followList = []
    let engine = system.ActorSelection("akka://Twitter/user/engine")
    engine <! RequestLoginPage id
    let rec loop () = actor{
        let! message = mailbox.Receive()
        match message with
        //---------------------登录与注册模块------------------------------------
        //服务器提示
        | Notice notice -> 
            printfn $"{notice.note}"
        
        //收到登录页
        | LoginPage -> 
            printfn $"{Login_Page}" 
            let mutable input = Console.ReadLine()
            while input <> "1" && input <> "2" do
                printfn $"Did not catch proper input, please input again"
                input <- Console.ReadLine()
            if input.Equals (string 1) then
                engine <! Login id
            if input.Equals (string 2) then
                engine <! Register id
            //还没写退出

        //需要输入密码
        | InputPwdToLogin notice ->
            printfn $"{notice.note}"
            let input = Console.ReadLine()
            engine <! Password {clientID = id; status = "to login"; pwd = input}
        
        //输入密码以注册
        | InputPwdToRegister notice ->
            printfn $"{notice.note}"
            let input = Console.ReadLine()
            engine <! Password {clientID = id; status = "to register"; pwd = input}

        //收到用户主页
        | HomePage ->
            printfn $"{Home_Page}"
            let mutable input = Console.ReadLine()
            while input.Length <> 1 || not(List.contains (int input) [1;2;3;4;5]) do
                printfn $"Did not catch proper input, please input again"
                input <- Console.ReadLine()
            match input with
            | "1" -> engine <! SendTweet id
            | "2" -> engine <! ReadTweets id
            | "3" -> engine <! MySubscriptions
            | "4" -> engine <! MyFollowers
            | "5" -> ignore() //还没写退出
            | _ -> failwith "unknown input"
        
        | RequestTweet ->
            let mutable contents = ""
            let mutable input = Console.ReadLine()
            while input <> "!send" do
                contents <- contents + (input + "\n")
                input <- Console.ReadLine()
            let tagList = FindTags contents
            let mentionList = FindMentions contents
            engine <! NewTweet {clientID = id; content = contents; tags = tagList; mentions = mentionList; time = DateTime.Now}
            

        | DisplayTweet tempList ->
            for each in tempList do
                Console.WriteLine("ID: " + (string each.clientID))
                Console.WriteLine(each.time)
                Console.WriteLine(each.content)
                Console.WriteLine()
            
            Console.WriteLine("enter b to back")
            Console.WriteLine("If you are intersted in someone's tweet, you can type ''follow someone'' and enter to follow")
            let mutable key = Console.ReadLine()
            while (key <> "b") && not (key.StartsWith "follow ") do
                Console.WriteLine("did not catch proper input")
                key <- Console.ReadLine()

            if key = "b" then
                engine <! RequestHomePage id
            else
                let tmp = (key.Split " ").[1] |> int
                engine <! WantToFollow {clientID=id; followID=tmp}
        
        | FollowSuccess temp->
            subscribeList <- subscribeList @ [temp]
            Console.WriteLine("Follow Success")
            engine <! RequestHomePage

        | _ -> printfn $"Client got unknown message"

        return! loop()
    }
    loop()


//-----------------------------------------Entry Point------------------------------------

let engine = spawn system "engine" <| Engine
let client = spawn system ("client" + (string 1)) <| Client 1


//-------------------------------Keep Program Alive------------------------------------------
while true do
    ignore()