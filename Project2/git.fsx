#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let mutable flag = true
let sw = System.Diagnostics.Stopwatch()

type Message =
    | Rumor of string
    | Converge of string
    | Gossip of string
    | Update of int
    | PushSum of float * float
    | SumConverge of float

let roundNodes n s =
    match s with
    | "2d"
    | "imp2d" -> Math.Pow(Math.Round(sqrt (float n)), 2.0) |> int
    | _ -> n

let pickRandom (l: List<_>) =
    let r = System.Random()
    l.[r.Next(l.Length)]

let gridNeighbors x n =
    let r = sqrt (float n) |> int
    [ 1 .. n ]
    |> List.filter (fun y ->
        if (x % r = 0)
        then (y = x + r || y = x - 1 || y = x - r)
        elif (x % r = 1)
        then (y = x + r || y = x + 1 || y = x - r)
        else (y = x + r || y = x - 1 || y = x + 1 || y = x - r))

let buildTopology n s a =
    let mutable map = Map.empty
    match s with
    | "full" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist = List.filter (fun y -> x <> y) [ 1 .. n ]
            map <- map.Add(x, nlist))
        |> ignore
        map
    | "line" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist =
                List.filter (fun y -> (y = x + 1 || y = x - 1)) [ 1 .. n ]
            if a <> "gossip" then
                if x = 1 then
                    let mlist = n::nlist
                    map <- map.Add(x, mlist)
                elif x = n then
                    let mlist = 1::nlist
                    map <- map.Add(x, mlist)
                else    
                    map <- map.Add(x, nlist)
            else      
                map <- map.Add(x, nlist))
        |> ignore
        map
    | "2d" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist = gridNeighbors x n
            map <- map.Add(x, nlist))
        |> ignore
        map
    | "imp2d" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist = gridNeighbors x n

            let random =
                [ 1 .. n ]
                |> List.filter (fun m -> m <> x && not (nlist |> List.contains m))
                |> pickRandom

            let randomNList = random :: nlist
            map <- map.Add(x, randomNList))
        |> ignore
        map
    | _ -> map

match fsi.CommandLineArgs.Length with
| 4 -> ignore
| _ -> failwith "Requires number of nodes, topology and algorithm as input"

let args = fsi.CommandLineArgs |> Array.tail
let topology = args.[1]
let algorithm = args.[2]
let nodes = roundNodes (args.[0] |> int) topology
let topologyMap = buildTopology nodes topology algorithm
let gossipcount = 60

let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/worker" + string s
    select actorPath system

let toList s = Set.fold (fun l m -> m :: l) [] s

let getRandomNeighbor x l =
    let nlist = (topologyMap.TryFind x).Value
    let fin = Set.ofList l

    let rem =
        nlist
        |> List.filter (fun a -> not (fin |> Set.contains a))

    if rem.IsEmpty then
        let alive =
            toList ((Set.ofSeq { 1 .. nodes }) - fin)

        getWorkerRef (pickRandom alive)
    else
        getWorkerRef (pickRandom rem)

let broadcastConvergence x =
    [ 1 .. nodes ]
    |> List.map (getWorkerRef)
    |> List.iter (fun ref -> ref <! Update x)

let observerBehavior count (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()

            match msg with
            | Converge (s) ->
                if (count + 1 = nodes) then
                    sw.Stop()
                    printfn "Gossip algorithm has converged in %A" sw.ElapsedMilliseconds
                    flag <- false
            | SumConverge (s) ->
                if count = 0 then
                    sw.Stop()
                    printfn "Push Sum has converged in %A" sw.ElapsedMilliseconds
                    flag <- false
            | _ -> failwith "Observer received unsupported message"

            return! loop (count + 1)
        }

    loop count

let observerRef =
    spawn system "observer" (observerBehavior 0)

let spreadRumor ref s dlist =
    let neighRef = getRandomNeighbor ref dlist
    neighRef <! Rumor(s)

let processGossip msg ref count dlist =
    let self = getWorkerRef ref
    if count < gossipcount then
        match msg with
        | Rumor (s) ->
            spreadRumor ref s dlist
            if count = 0 then self <! Gossip(s)
            if count + 1 = gossipcount then
                let conmsg =
                    "Worker " + string ref + " has converged"

                observerRef <! Converge conmsg
                broadcastConvergence ref
            count + 1, dlist
        | Gossip (s) ->
            spreadRumor ref s dlist
            self <! Gossip(s)
            count, dlist
        | Update (s) -> count, s :: dlist
        | _ -> failwith "Worker received unsupported message"
    else
        count + 1, dlist

let gossipBehavior ref (inbox: Actor<Message>) =
    let rec loop count dlist =
        actor {
            let! msg = inbox.Receive()
            let newCount, newList = processGossip msg ref count dlist
            return! loop newCount newList
        }

    loop 0 List.Empty

let processPushsum ref msg c s w =
    if c < 3 then
        let l = List.Empty
        let self = getWorkerRef ref
        match msg with
        | Rumor (_) ->
            let neighRef = getRandomNeighbor ref l
            neighRef <! PushSum(s / 2.0, w / 2.0)
            self <! Gossip
            c, s / 2.0, w / 2.0
        | PushSum (a, b) ->
            let ss = s + a
            let ww = w + b

            let cc =
                if abs ((s / w) - (ss / ww)) < 1.0e-10 then c + 1 else 0

            if cc = 3 then
                observerRef <! SumConverge(ss / ww)
            else
                let neighRef = getRandomNeighbor ref l
                neighRef <! PushSum(ss / 2.0, ww / 2.0)
            if (s = float ref) then self <! Gossip
            cc, ss / 2.0, ww / 2.0
        | Gossip (m) ->
            let neighRef = getRandomNeighbor ref l
            neighRef <! PushSum(s / 2.0, w / 2.0)
            self <! Gossip
            c, s / 2.0, w / 2.0
        | _ -> failwith "Worker received unsupported message"
    else
        c, s, w

let pushsumBehavior ref (inbox: Actor<Message>) =
    let rec loop count s w =
        actor {
            let! msg = inbox.Receive()
            let cc, ss, ww = processPushsum ref msg count s w
            return! loop cc ss ww
        }

    loop 0 (ref |> double) 1.0

let workerBehavior x =
    if algorithm = "gossip" then gossipBehavior x else pushsumBehavior x

let workerRef =
    [ 1 .. nodes ]
    |> List.map (fun x ->
        let name = "worker" + string x
        spawn system name (workerBehavior x))
    |> pickRandom

workerRef <! Rumor "starting a random rumor"
sw.Start()

while flag do
    ignore ()