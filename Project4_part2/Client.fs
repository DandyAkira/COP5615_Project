namespace ClientServer2

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html
open WebSharper.Forms
open WebSharper.Sitelets
open System



[<JavaScript>]
module Client =  
    let mutable MyName = "Unknown"
    let UserLogin () = 
        Form.Return (fun user pass -> {User=user; Pass=pass})
        <*> (Form.Yield ""
            |> Validation.IsNotEmpty "Must enter a username")
        <*> (Form.Yield ""
            |> Validation.IsNotEmpty "Must enter a password")
        |> Form.WithSubmit
        |> Form.Run (fun userpass ->
            async{
                do! Server.LoginUser userpass
                return JS.Window.Location.Reload()
            }|>Async.Start
        )
        |> Form.Render (fun user pass submit ->
            div [] [
                div [] [label [] [text "Username: "]; Doc.Input [] user]
                div [] [label [] [text "Password: "]; Doc.PasswordBox [] pass]
                Doc.Button "Log in" [] submit.Trigger
                div [] [
                    Doc.ShowErrors submit.View (fun errors ->
                        errors
                        |> Seq.map (fun m -> p [] [text m.Text])
                        |> Seq.cast
                        |> Doc.Concat)
                ]
            ]
        )

    let UserHome (userName) = 
        if MyName = "Unknown" then
            MyName <- userName
        let CreateTweetMSG (content:string) (mentions:string) (hashtags:string) =
            let tmp_men = mentions.Split('@')|> Array.filter(fun elem -> elem <> "")
            let tmp_hash = hashtags.Split('#')|> Array.filter(fun elem -> elem <> "")
            {Tweet = {Content = content; Mentions=tmp_men; Hashtags=tmp_hash}; Sender = MyName}

        Form.Return (fun content mentions hashtags -> CreateTweetMSG content mentions hashtags)
        <*> (Form.Yield ""
            |> Validation.IsNotEmpty "Must enter the content")
        <*> Form.Yield ""
        <*> Form.Yield ""
        |> Form.WithSubmit
        |> Form.Run (fun tweetMSG ->
            async{
                let! msg = Server.ReceiveTweet tweetMSG
                JS.Alert(msg)
            }|>Async.Start
        )
        |> Form.Render (fun content mentions hashtags submit ->
            div [] [
                div [] [label [] [text "Content: "]; Doc.InputArea [] content]
                div [] [label [] [text "mentions: (split then with '@')"]; Doc.Input [] mentions]
                div [] [label [] [text "hashtags: (split then with '#')"]; Doc.Input [] hashtags]
                Doc.Button "Send" [] submit.Trigger
                div [] [
                    Doc.ShowErrors submit.View (fun errors ->
                        errors
                        |> Seq.map (fun m -> p [] [text m.Text])
                        |> Seq.cast
                        |> Doc.Concat)
                ]
                hr [] []
            ]
        )

    let UserLogout (userName) = 
        if MyName = "Unknown" then
            MyName <- userName
        div [] [
            p [] [text "click to log out"]
            button [
                on.click (fun _ _ ->
                    async {
                        do! Server.LogoutUser(userName)
                        JS.Window.Location.Reload()
                    }
                    |> Async.Start
                )
            ] [text "logout"]
        
        ]

    let UserSearch (username) = 
        let searchUsername = Var.Create ""
        let searchHashtag = Var.Create ""
        let searchContent = Var.Create ""
        let searchRes = Var.Create ""
        if MyName = "Unknown" then
            MyName <- username
        div [] [
            h2 [] [text "Search"]
            div [] [label [] [text "username"]; Doc.Input [] searchUsername]
            div [] [label [] [text "content"]; Doc.Input [] searchContent]
            div [] [label [] [text "hashtag"]; Doc.Input [] searchHashtag]
            button [
                on.click (fun _ _ ->
                    async {
                        let! msg = Server.HandleSearch {from=MyName;name=searchUsername.Get();content=searchContent.Get();hashtag=searchHashtag.Get()}
                        searchRes.Value <- msg
                    }
                    |> Async.Start
                )
            ] [text "search"]
            hr [] []
            p [] [text "Result: See Console"]
            h4 [attr.``class`` "text-muted"] [text "--------------------"]
        ]


    let UserSubscribe (username) = 
        if MyName = "Unknown" then
            MyName <- username

        let followlist = Var.Create (Server.GetFollowers MyName)
        let sublist = Var.Create (Server.GetSubscribers MyName)
        let subscriber = Var.Create ""

        div [] [
            h3 [] [text "Input a user's name"]
            Doc.Input [] subscriber
            button [
                on.click (fun _ _ ->
                    async {
                        let! msg = Server.ReceiveSubscriber {Sender=MyName; Subscriber=subscriber.Get()}
                        sublist.Value <- msg.message
                        JS.Alert(msg.notice)
                        JS.Window.Location.Reload()
                    }
                    |> Async.Start
                )
            ] [text "submit"]
            hr [] []
            p [] [text "Your Followers"]
            div [] [text (followlist.Get())]
            hr [] []
            p [] [text "Your Subscribers"]
            div [] [text (sublist.Get())]
        ]






