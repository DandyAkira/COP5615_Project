namespace ClientServer2

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Server

type EndPoint =
    | [<EndPoint "/">] Home

module Templating =
    open WebSharper.UI.Html

    type MainTemplate = Templating.Template<"Main.html">

    // Compute a menubar where the menu item for the given endpoint is active
    let MenuBar (ctx: Context<EndPoint>) endpoint : Doc list =
        let ( => ) txt act =
             li [if endpoint = act then yield attr.``class`` "active"] [
                a [attr.href (ctx.Link act)] [text txt]
             ]
        [
            "Home" => EndPoint.Home
        ]

    let Main ctx action (title: string) (body: Doc list) =
        Content.Page(
            MainTemplate()
                .Title(title)
                .MenuBar(MenuBar ctx action)
                .Body(body)
                .Doc()
        )

module Site =
    open WebSharper.UI.Html

    let HomePage (ctx: Context<_>) = 
        async {
        let! loggedIn = ctx.UserSession.GetLoggedInUser()
        let content = 
            match loggedIn with
            | Some username ->
                div [] [
                    h1 [] [text ("Welcome, " + username + "!")]
                    hr [] []
                    div [] [client <@ Client.UserHome (username)@>]
                    hr [] []
                    div [] [client <@ Client.UserSearch (username)@>]
                    hr [] []
                    h2 [] [text "Subscribe!"]
                    div [] [client <@ Client.UserSubscribe (username)@>]
                    hr [] []
                    div [] [client <@ Client.UserLogout (username)@>]
                ]
            | None ->
                div [] [
                    client <@ Client.UserLogin ()@>
                ]
        return! Templating.Main ctx EndPoint.Home "Home" [content]
        }

    [<Website>]
    let Main =
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | EndPoint.Home -> HomePage ctx
        )
