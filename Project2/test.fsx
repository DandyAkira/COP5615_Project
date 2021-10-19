
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

type sw = {
    value : double
    weight : double
}

type Message = 
    |Rumor of string
    |Send of string
    |Finish of int
    |Converge of int
    |PushSum of sw




let inputs = fsi.CommandLineArgs |> Array.tail
let inputNum = inputs.[0] |> float
let topology = inputs.[1]
let algorithm = inputs.[2]
let sw = System.Diagnostics.Stopwatch()


let AdjustNodeNumber (inputNum:float) = 
    if topology = "3D" || topology = "imp3D" then
        let tmp = Math.Ceiling (inputNum ** (1.0/3.0))
        let res = (tmp ** 3.0) |> int
        res, int tmp
    else
        int inputNum, 0

let nodeNum, width = AdjustNodeNumber inputNum

let system = ActorSystem.Create("Project2")
//let stopwatch = System.Diagnostics.Stopwatch()
let mutable nodeList:IActorRef list = List.Empty
let mutable neighbourMap = Map.empty.Add(0, nodeList)


        

printfn $"{nodeNum}; {topology}"

let containsNumber number list = List.exists (fun elem -> elem = number) list

let removeAt list index =
  list
  // Associate each element with a boolean flag specifying whether 
  // we want to keep the element in the resulting list
  |> List.mapi (fun i el -> (i <> index, el)) 
  // Remove elements for which the flag is 'false' and drop the flags
  |> List.filter fst |> List.map snd

let GetNodeID (node:IActorRef) = 
    node.Path.Name.ToString().[4..] |> int

let topologyMap (topology:string) = 
    match topology with
    | "line" ->
        for i in 0..nodeNum-1 do
            let mutable nb = []
            if i-1 >=0 then nb <- nb@[nodeList.[i-1]]
            if i+1 < nodeNum then nb <- nb@[nodeList.[i+1]]
            neighbourMap <- neighbourMap.Add(i+1, nb)
    
    | "3D" ->
        let mutable cube:int[,,] = Array3D.zeroCreate width width width
        let mutable x = 0

        for i in 0..width-1 do
            for j in 0..width-1 do
                for k in 0..width-1 do
                    x <- x+1
                    Array3D.set cube i j k x                   
        
        if x<>nodeNum then 
            printfn $"Cube Generate Error, nodeNum {nodeNum}, x {x}"

        for i in 0..width-1 do
            for j in 0..width-1 do
                for k in 0..width-1 do
                    let self = Array3D.get cube i j k
                    let mutable nb = []
                    if i-1 >= 0 then 
                        let down = Array3D.get cube (i-1) j k
                        nb <- nb @ [nodeList.[down-1]]
                    if i+1 < width then
                        let up = Array3D.get cube (i+1) j k
                        nb <- nb @ [nodeList.[up-1]]
                    if j-1 >= 0 then
                        let back = Array3D.get cube i (j-1) k
                        nb <- nb @ [nodeList.[back-1]]
                    if j+1 < width then
                        let forward = Array3D.get cube i (j+1) k
                        nb <- nb @ [nodeList.[forward-1]]
                    if k-1 >= 0 then
                        let left = Array3D.get cube i j (k-1)
                        nb <- nb @ [nodeList.[left-1]]
                    if k+1 < width then
                        let right = Array3D.get cube i j (k+1)
                        nb <- nb @ [nodeList.[right-1]]
                    neighbourMap <- neighbourMap.Add(self, nb)

    | "imp3D" ->
        let mutable cube:int[,,] = Array3D.zeroCreate width width width
        let mutable x = 0

        for i in 0..width-1 do
            for j in 0..width-1 do
                for k in 0..width-1 do
                    x <- x+1
                    Array3D.set cube i j k x                   
        
        if x<>nodeNum then 
            printfn $"Cube Generate Error, nodeNum {nodeNum}, x {x}"

        for i in 0..width-1 do
            for j in 0..width-1 do
                for k in 0..width-1 do
                    let self = Array3D.get cube i j k
                    let mutable nb = []
                    if i-1 >= 0 then 
                        let down = Array3D.get cube (i-1) j k
                        nb <- nb @ [nodeList.[down-1]]
                    if i+1 < width then
                        let up = Array3D.get cube (i+1) j k
                        nb <- nb @ [nodeList.[up-1]]
                    if j-1 >= 0 then
                        let back = Array3D.get cube i (j-1) k
                        nb <- nb @ [nodeList.[back-1]]
                    if j+1 < width then
                        let forward = Array3D.get cube i (j+1) k
                        nb <- nb @ [nodeList.[forward-1]]
                    if k-1 >= 0 then
                        let left = Array3D.get cube i j (k-1)
                        nb <- nb @ [nodeList.[left-1]]
                    if k+1 < width then
                        let right = Array3D.get cube i j (k+1)
                        nb <- nb @ [nodeList.[right-1]]
                    neighbourMap <- neighbourMap.Add(self, nb)

    | _ ->
        ignore()    

        
        


            
            

let GetRandomNeighbour id  = 
    let random = System.Random()

    if topology = "full" then      
        let mutable r = random.Next(nodeList.Length)
        while (r+1) = id do
            r <- random.Next(nodeList.Length)
        nodeList.[r], r+1
    
    else if topology = "imp3D" then
        let mutable r = random.Next(nodeList.Length)
        while (r+1) = id || List.contains nodeList.[r] neighbourMap.[id] do
            r <- random.Next(nodeList.Length)
        let neighbourList = neighbourMap.[id] @ [nodeList.[r]]
        let mutable x = random.Next(neighbourList.Length)
        neighbourList.[x], GetNodeID(neighbourList.[x])

        else
            let neighbourList = neighbourMap.[id]
            let mutable x = random.Next(neighbourList.Length)
            neighbourList.[x], GetNodeID(neighbourList.[x])



//-------Actors----------------------------------------------------------------------------------------------------------------

let Observer (nodeNum:int) (mailbox: Actor<Message>) = 
    let mutable round = 1
    let mutable num = 0
    let innerSW = System.Diagnostics.Stopwatch()
    innerSW.Start()
    let mutable converge_list = []
    let rec loop () = actor{

        if num = nodeNum then
            //printfn $"round {round} is finished"
            //Console.ReadKey() |> ignore
            for each in nodeList do
                each <! Send "send"
            round <- round + 1
            num <- 0


        if converge_list.Length>0 && (int innerSW.ElapsedMilliseconds)>10000 then
            sw.Stop()
            innerSW.Stop()
            printfn $"Done, {converge_list.Length} nodes converges"
            printfn $"takes {round} rounds"
            printfn $"takes {(int sw.ElapsedMilliseconds) - 10000} ms"
            Environment.Exit 1
        
        let! msg = mailbox.Receive()
        match msg with
        | Finish (id) ->
            //printfn $"round{round}, node{id}"
            num <- num+1
        | Converge id ->
            if not(containsNumber id converge_list) then
                converge_list <- converge_list @ [id]
                innerSW.Restart()
                printfn $"node {id} has converged, now {converge_list.Length} nodes have converged"
            
            if converge_list.Length = (nodeNum - 1) then
                sw.Stop()
                printfn $"all nodes converges"
                printfn $"takes {round} rounds"
                printfn $"takes {sw.ElapsedMilliseconds} ms"
            
                //printfn $"{converge_list.ToString()}"

                Environment.Exit 1
            //printfn $"Ob: node {id} converges"
        | Send (_) ->
            for each in nodeList do
                if not(List.contains (GetNodeID each) converge_list) then
                    each <! Send "send"
        | _ -> 
            printfn $"oberver receives unknown msg"

        return! loop()
    }
    loop()

let RumorNode (id:int) (mailbox: Actor<Message>) = 
    let mutable count = 0
    let mutable send_rumor = false
    let rec loop () = actor{
        if count >= 10 then
            send_rumor <- false
            system.ActorSelection(@"akka://Project2/user/observer") <! Converge id
            //printfn $"Node {id} has converged"
        else 
            if count>0 && count<10 then
                send_rumor <- true
        
        let! msg = mailbox.Receive()
        match msg with
        | Rumor (_) ->
            count <- count + 1
            if count < 10 then send_rumor <- true
            else ignore()
        | Send (_) ->
            if send_rumor = true then
                let neighbour, sent_id = GetRandomNeighbour id 
                neighbour <! Rumor "this is a rumor"
                send_rumor <- false
                // printfn $"{id} -> {sent_id}"
            // printfn $"{id} has {count}"
            system.ActorSelection(@"akka://Project2/user/observer") <! Finish id
        | _ -> 
            printfn $"Node {id} receives Unknown msg"
        
        
        return! loop()
    }
    loop()

let PushSumNode (id:int) (mailbox: Actor<Message>) = 
    let mutable s:double = double id
    let mutable w:double = 1.0
    let mutable send = true
    let mutable count = 0
    let mutable ratio = s/w
    let mutable lastS = s
    let mutable lastW = w
    let rec loop() = actor{
        if count = 3 then
            printfn $"{ratio}"
        let! msg = mailbox.Receive()
        match msg with
        | Send (_) ->
            if lastS = s && lastW = w then
                ignore()
            else
                let mutable temp = s/w
                if Math.Abs(temp-ratio) <= (double 10.0)**(double -10.0) then 
                    count <- count + 1
                lastS <- s
                lastW <- w

            if count >= 3 then
                send <- false
                system.ActorSelection(@"akka://Project2/user/observer") <! Converge id
            
            else
                if send = true then
                    let neighbour, sent_id = GetRandomNeighbour id
                    neighbour <! PushSum {value = s/2.0; weight = w/2.0}
                    //printfn $"{id} -> {sent_id} : {s/2.0}, {w/2.0}"
                    s <- s/2.0
                    w <- w/2.0

            ratio <- s/w
            //printfn $"Node {id}, s = {s}, w = {w}, ratio = {ratio}"
            system.ActorSelection(@"akka://Project2/user/observer") <! Finish id
        
        | PushSum x ->
            s <- s + x.value
            w <- w + x.weight

        | _ -> 
            printfn $"Node {id} receives Unknown msg"       
               
        return! loop()
    }
    loop()

//---Starts From Here---------------------------------------------------------------------------------------------------------
if algorithm = "gossip" then
    for id in 1..nodeNum do
        let node = spawn system ("node"+ string id) <| RumorNode id
        //printfn $"{node.ToString()}"
        nodeList <- nodeList @ [node]

    let random = System.Random()
    let r = random.Next(nodeNum)
    printfn $"starts from {r+1}"
    nodeList.[r] <! Rumor "this is a rumor"

if algorithm = "push-sum" then
    for id in 1..nodeNum do
        let node = spawn system ("node"+ string id) <| PushSumNode id
        //printfn $"{node.ToString()}"
        nodeList <- nodeList @ [node]   

topologyMap topology

let observer = spawn system "observer" <| Observer nodeNum
sw.Start()
observer <! Send "send" 

while true do
    ignore()
    