namespace ClientServer2

open WebSharper
open System
open System.IO
open System.Net

type userpass = {
    User:string
    Pass:string
}

type Tweet = {
    Content:string
    Mentions: string[]
    Hashtags: string[]
}

type TweetMSG = {
    Tweet:Tweet
    Sender:string
}

type WantSubscribe = {
    Sender:string
    Subscriber:string
}

type SubscribeRespond = {
    notice:string
    message:string
}

type SearchRequest = {
    from:string
    name:string
    content:string
    hashtag:string
}

type Record = {
    msgtype:string
    user:string
    message:string
}


module Server =
    

    
    let mutable userMap:Map<String, String> = Map.empty //userMap<userName, passWord>
    let mutable tweetMap:Map<String, List<Tweet>> = Map.empty ////tweetMap<userName, List<Tweet>>
    let mutable followMap:Map<String, List<String>> = Map.empty
    let mutable subscribeMap:Map<String, List<String>> = Map.empty


    [<Rpc>]
    let LogoutUser (username) = 
        let ctx = Web.Remoting.GetContext()
        File.AppendAllText("recording.json", {msgtype="receive";user=username; message="log out"}.ToString()+"\n\n")
        ctx.UserSession.Logout()
        


    [<Rpc>]
    let LoginUser userpass =
        let ctx = Web.Remoting.GetContext()
        let username = userpass.User
        let password = userpass.Pass
        File.AppendAllText("recording.json", {msgtype="receive";user=username; message="log in request"}.ToString()+"\n\n")
        if userMap.ContainsKey username then
            if (userMap.[username] = password) then               
                File.AppendAllText("recording.json", {msgtype="send";user=username; message="log in success"}.ToString()+"\n\n")
                ctx.UserSession.LoginUser(userpass.User, persistent=false) |> Async.Ignore
            else
                File.AppendAllText("recording.json", {msgtype="send";user=username; message="log in failed"}.ToString()+"\n\n")
                async.Return ()
        else
            userMap <- userMap.Add (username, password)
            subscribeMap <- subscribeMap.Add(username, [])
            followMap <- followMap.Add(username, [])
            Console.WriteLine("new user registered")
            Console.WriteLine(userMap.ToString())
            File.AppendAllText("recording.json", {msgtype="send";user=username; message="new account created"}.ToString()+"\n\n")
            ctx.UserSession.LoginUser(userpass.User, persistent=false) |> Async.Ignore

    [<Rpc>]
    let ReceiveTweet tweetMSG = 
        let tweet = tweetMSG.Tweet
        let sender = tweetMSG.Sender
        let mutable tlist = [tweet]
        if tweetMap.ContainsKey sender then
            tlist <- tlist @ tweetMap.[sender]
        tweetMap <- tweetMap.Add(sender, tlist)
        Console.WriteLine(tweetMap.ToString())
        File.AppendAllText("recording.json", {msgtype="receive";user=sender; message="receive tweet + \n"+tweet.ToString()}.ToString()+"\n\n")
        async.Return("success")

    [<Rpc>]
    let ReceiveSubscriber (wantSub:WantSubscribe) = 
        async{
            let follow = wantSub.Sender
            let sub = wantSub.Subscriber
            File.AppendAllText("recording.json", {msgtype="receive";user=follow; message="subscribe request: " + sub}.ToString()+"\n\n")
            if follow = sub then
                return {notice="you can't follow yourself!"; message=""}
            else
                if userMap.ContainsKey follow && userMap.ContainsKey sub then
                    let mutable followlist = followMap.[sub]
                    followlist <- followlist@[follow]
                    followMap <- followMap.Add(sub, followlist)
                    let mutable subscribelist = subscribeMap.[follow]
                    subscribelist <- subscribelist@[sub]
                    subscribeMap <- subscribeMap.Add(follow, subscribelist)
                    File.AppendAllText("recording.json", {msgtype="send";user=follow; message="subscribe success"}.ToString()+"\n\n")
                    return {notice="Success!"; message = subscribeMap.[follow].ToString()}
                else
                    File.AppendAllText("recording.json", {msgtype="send";user=follow; message="subscribe fail"}.ToString()+"\n\n")

                    return {notice="Fail!"; message = ""}
        }


    [<Rpc>]
    let GetFollowers user = 
        let mutable res = "None"
        if followMap.ContainsKey user then
            res<-followMap.[user].ToString()
        res


            

    [<Rpc>]
    let GetSubscribers user =
        let mutable res = "None"
        if subscribeMap.ContainsKey user then
            res <- subscribeMap.[user].ToString()
        res

    [<Rpc>]
    let HandleSearch (searchReq:SearchRequest) = 
        async{
            File.AppendAllText("recording.json", {msgtype="receive";user=searchReq.from; message="search request"}.ToString()+"\n\n")

            let mutable res = ""
            for eachuser in tweetMap.Keys do
                for eachtweet in tweetMap.[eachuser] do
                    if eachtweet.Content.Contains(searchReq.content) || eachuser=searchReq.name || Array.contains searchReq.hashtag eachtweet.Hashtags then
                        res <- res + eachtweet.ToString()+"\n"

            Console.WriteLine(res)
            File.AppendAllText("recording.json", {msgtype="send";user=searchReq.from; message="search result: "+res}.ToString()+"\n\n")
            return res
        }


    [<Rpc>]
    let DoSomething input =
        let R (s: string) = System.String(Array.rev(s.ToCharArray()))
        async {
            return R input
        }
