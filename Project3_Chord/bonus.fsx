#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp
open System.Threading 

let inputs = fsi.CommandLineArgs |> Array.tail
let numNodes = inputs.[0] |> int
let system = ActorSystem.Create("project3")
let rand = Random()
let m = 4
let timeslot= 100


type FixFingerRequestInfo = {
    TargetFinger : int 
    TargetFingerIndex : int 
    RequesterNodeId : int
}

type FixFingerResponseInfo = {
    TargetFinger : int 
    TargetFingerIndex : int 
    ResultNodeId : int
}

type RequestInfo = {
    TargetKey : int
    TargetKeyIndex : int
    RequesterNodeId : int
    Hop : int
}

type ResultInfo = {
    TargetKey : int
    TargetKeyIndex : int
    ResultNodeId : int
    Hop : int
}

type UpdatePredecessorNotification = {
    PotentialPredecessor : int
}

type StabilizeSuccessorRequest = {
    RequesterNodeId : int
}

type StabilizeSuccessorResponse = {
    PotentialSuccessor : int
}

type InitializationInfo = {
    RandomSearchNodeId : int
    FirstOrNot : bool
}

type SetSuccessorListRequest = {
    OriginalNodeID: int
    Hop: int
}

type SetSuccessorListResponse = {
    Closeness: int
    ResponseNodeID: int
}

type MSG = {
    From: int
}
type RESP = {
    res: int
}

let p2pNode (nodeId:int) (mailbox: Actor<_>) = 
    let myId = nodeId
    let mutable curFixFingerIndex = 1
    let mutable myFingerTable = Array.create m [||]
    let mutable predecessorNodeId = -1
    let mutable successorNodeId = nodeId
    let mutable successorList = Array.create (int numNodes/2) -1
    let length = Array.length successorList
    let mutable flag = true
    let sw = System.Diagnostics.Stopwatch()
    

    let printInfo (key:int)= 
        printfn $"Node : {myId}"
        printfn $"predecessor is {predecessorNodeId}"
        printfn $"successor is {successorNodeId}"
        printfn "%A" successorList        
        printfn "fingerTable"
        for i in 1..m do
            let finger = myFingerTable.[i-1].[0] 
            let fingerNode = myFingerTable.[i-1].[1] 
            printfn $"{finger}          {fingerNode}"
    let rec loop() = actor{
        if flag then
            if (int sw.ElapsedMilliseconds) > 500 then
                sw.Reset()
                printfn $"Node {successorNodeId} may fail"
                let mutable index = Array.IndexOf(successorList, successorNodeId)
                if index>=0 && index < successorList.Length-1 then
                    successorNodeId <- successorList.[index+1]
                    printfn $"Node {myId}'s successor changed to {successorNodeId}"
                else
                    printfn "all successors of Node {myId} have failed"
                    successorNodeId <- myId
                    //需要完善
            let! message = mailbox.Receive()
            match box message with
            | :? string ->
                printInfo myId

            | :? int as request ->
                if request=1 then 
                    let successorNode = system.ActorSelection("akka://project3/user/"+(string successorNodeId))
                    let stabilizeSuccessorRequest = {RequesterNodeId=myId}
                    successorNode <! stabilizeSuccessorRequest
                if request = 3 then
                    let successorNode = system.ActorSelection("akka://project3/user/" + (string successorNodeId))
                    let SetSuccessorListRequest = {OriginalNodeID=myId; Hop = length}
                    successorNode <! SetSuccessorListRequest
                if request = 4 then
                    let successorNode = system.ActorSelection("akka://project3/user/"+(string successorNodeId))
                    let MSG = {From = myId}
                    successorNode <! MSG
                    printfn $"{myId} send-> {successorNodeId}"
                    sw.Start()
                if request = 0 then
                    flag <- false                    
                else 
                    if curFixFingerIndex>m then
                        curFixFingerIndex <- 1
                    let fixFingerRequest = {TargetFinger=myFingerTable.[curFixFingerIndex-1].[0]; TargetFingerIndex=curFixFingerIndex; RequesterNodeId=myId}
                    mailbox.Self <! fixFingerRequest
                    curFixFingerIndex <- curFixFingerIndex+1
            
            | :? SetSuccessorListRequest as info ->
                if info.Hop > 1 then
                    let SetSuccessorListRequest = {OriginalNodeID=info.OriginalNodeID; Hop = info.Hop-1}
                    let successorNode = system.ActorSelection("akka://project3/user/" + (string successorNodeId))
                    successorNode <! SetSuccessorListRequest
                    let SetSuccessorListResponse = {Closeness=info.Hop; ResponseNodeID = myId}
                    let originalNode = system.ActorSelection("akka://project3/user/" + (string info.OriginalNodeID))
                    originalNode <! SetSuccessorListResponse

                if info.Hop = 1 then
                    let SetSuccessorListResponse = {Closeness=info.Hop; ResponseNodeID = myId}
                    let originalNode = system.ActorSelection("akka://project3/user/" + (string info.OriginalNodeID))
                    originalNode <! SetSuccessorListResponse
            
            | :? SetSuccessorListResponse as response ->
                let closeness = response.Closeness
                let responseID = response.ResponseNodeID
                Array.set successorList (length - closeness) responseID


            | :? RequestInfo as requestInfo ->
                let targetKey = requestInfo.TargetKey
                let requesterNodeId = requestInfo.RequesterNodeId
                let targetKeyIndex = requestInfo.TargetKeyIndex
                let mutable hop = requestInfo.Hop

                let requesterNode = system.ActorSelection("akka://project3/user/"+(string requesterNodeId))
                if hop<>(-1) then
                    hop <- hop+1
                if myId=successorNodeId || targetKey=myId then 
                    let resultInfo = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex;ResultNodeId=myId; Hop=hop}
                    requesterNode <! resultInfo
                elif (myId<successorNodeId && targetKey>myId && targetKey<=successorNodeId)
                    ||(myId>successorNodeId && (targetKey>myId || targetKey<=successorNodeId)) then
                    let resultInfo = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex; ResultNodeId=successorNodeId; Hop=hop}
                    requesterNode <! resultInfo
                else 
                    let mutable countdown = m-1
                    let mutable breakLoop = false
                    while countdown>=0 && not breakLoop do
                        let tempFingerNodeId = myFingerTable.[countdown].[1]
                        if (myId<tempFingerNodeId && (targetKey>=tempFingerNodeId || myId>targetKey)) 
                           ||(myId>tempFingerNodeId && (targetKey>=tempFingerNodeId && myId>targetKey)) then
                            let tempFingerNode = system.ActorSelection("akka://project3/user/"+(string tempFingerNodeId))
                            let forwardRequest = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex; RequesterNodeId=requesterNodeId; Hop=hop}
                            tempFingerNode <! forwardRequest
                            breakLoop <- true
                        elif myId=tempFingerNodeId then
                            let resultInfo = {TargetKey=targetKey; TargetKeyIndex=targetKeyIndex;ResultNodeId=tempFingerNodeId; Hop=hop}
                            requesterNode <! resultInfo
                        countdown <- countdown-1
            | :? ResultInfo as resultInfo ->
                let targetKey = resultInfo.TargetKey
                let targetKeyIndex = resultInfo.TargetKeyIndex
                let resultNodeId = resultInfo.ResultNodeId
                let hop = resultInfo.Hop 
                if hop<>(-1) then
                    printfn $"Node:{myId} find the {targetKey} at the node {resultNodeId}"
                elif targetKeyIndex<>(-1) then
                    if targetKeyIndex=1 then
                        successorNodeId <- resultNodeId
                    myFingerTable.[targetKeyIndex-1].[1] <- resultNodeId

            | :? FixFingerRequestInfo as fixFingerRequestInfo ->
                let targetFinger = fixFingerRequestInfo.TargetFinger
                let targetFingerIndex = fixFingerRequestInfo.TargetFingerIndex
                let requesterNodeId = fixFingerRequestInfo.RequesterNodeId

                let requesterNode = system.ActorSelection("akka://project3/user/"+(string requesterNodeId))
                if myId=successorNodeId || targetFinger=myId then 
                    let resultInfo = {TargetFinger=targetFinger; TargetFingerIndex=targetFingerIndex;ResultNodeId=myId}
                    requesterNode <! resultInfo
                elif (myId<successorNodeId && targetFinger>myId && targetFinger<=successorNodeId)
                    ||(myId>successorNodeId && (targetFinger>myId || targetFinger<=successorNodeId)) then
                    let resultInfo = {TargetFinger=targetFinger; TargetFingerIndex=targetFingerIndex;ResultNodeId=successorNodeId}
                    requesterNode <! resultInfo
                else 
                    let mutable countdown = m-1
                    let mutable breakLoop = false
                    while countdown>=0 && not breakLoop do
                        let tempFingerNodeId = myFingerTable.[countdown].[1]
                        if (myId<tempFingerNodeId && (targetFinger>=tempFingerNodeId || myId>targetFinger)) 
                           ||(myId>tempFingerNodeId && (targetFinger>=tempFingerNodeId && myId>targetFinger)) then
                            let tempFingerNode = system.ActorSelection("akka://project3/user/"+(string tempFingerNodeId))
                            let forwardRequest = {TargetFinger=targetFinger; TargetFingerIndex=targetFingerIndex; RequesterNodeId=requesterNodeId}
                            tempFingerNode <! forwardRequest
                            breakLoop <- true
                        elif myId=tempFingerNodeId then
                            let resultInfo = {TargetFinger=targetFinger; TargetFingerIndex=targetFingerIndex;ResultNodeId=tempFingerNodeId}
                            requesterNode <! resultInfo
                        countdown <- countdown-1
            | :? FixFingerResponseInfo as fixFingerResponseInfo ->
                let targetFinger = fixFingerResponseInfo.TargetFinger
                let targetFingerIndex = fixFingerResponseInfo.TargetFingerIndex
                let resultNodeId = fixFingerResponseInfo.ResultNodeId
                myFingerTable.[targetFingerIndex-1].[1] <- resultNodeId

            | :? StabilizeSuccessorRequest as stabilizeSuccessorRequest ->
                let requesterNodeId = stabilizeSuccessorRequest.RequesterNodeId
                let requesterNode = system.ActorSelection("akka://project3/user/"+(string requesterNodeId))
                if predecessorNodeId=(-1) then
                    predecessorNodeId <- myId
                let stabilizeSuccessorResponse = {PotentialSuccessor=predecessorNodeId}
                requesterNode <! stabilizeSuccessorResponse
            | :? StabilizeSuccessorResponse as stabilizeSuccessorResponse ->
                let potentialSuccessor = stabilizeSuccessorResponse.PotentialSuccessor
                if potentialSuccessor<>successorNodeId then
                    if myId=successorNodeId then
                        successorNodeId <- potentialSuccessor
                    if (myId<successorNodeId && potentialSuccessor>myId && potentialSuccessor<successorNodeId) 
                       || (myId>successorNodeId && (potentialSuccessor>myId || potentialSuccessor<successorNodeId))then
                        successorNodeId <- potentialSuccessor
                let updatePredecessorNotification = {PotentialPredecessor=myId}
                let successorNode = system.ActorSelection("akka://project3/user/"+(string successorNodeId))
                successorNode <! updatePredecessorNotification
            | :? UpdatePredecessorNotification as updatePredecessorNotification ->
                let potentialPredecessor = updatePredecessorNotification.PotentialPredecessor
                if predecessorNodeId<>potentialPredecessor then
                    if myId=predecessorNodeId || predecessorNodeId=(-1) then
                        predecessorNodeId <- potentialPredecessor 
                    if (predecessorNodeId<myId && potentialPredecessor>predecessorNodeId && potentialPredecessor<myId) 
                       ||(predecessorNodeId>myId && (potentialPredecessor>predecessorNodeId || potentialPredecessor<myId))then
                        predecessorNodeId <- potentialPredecessor 

            | :? InitializationInfo as initialization ->
                let randomSearchNodeId = initialization.RandomSearchNodeId
                let firstOrNot = initialization.FirstOrNot
                for i in 1..m do
                    let insertKey = (myId + pown 2 (i-1)) % (pown 2 m)
                    myFingerTable.[i-1] <- [|insertKey;myId|]
                if not firstOrNot then
                    let randomSearchNode = system.ActorSelection("akka://project3/user/"+(string randomSearchNodeId))
                    for i in 1..m do
                        let requestKey = (myId + pown 2 (i-1)) % (pown 2 m)
                        let requestInfo = {TargetKey=requestKey; TargetKeyIndex=i; RequesterNodeId=myId; Hop=(-1)}
                        randomSearchNode <! requestInfo
            
            | :? MSG as msg ->
                let RESP = {res = myId}
                let respondTo = system.ActorSelection("akka://project3/user/"+(string msg.From))
                respondTo <! RESP
                printfn $"{myId} reply-> {msg.From}"

            | :? RESP as response ->
                sw.Reset()

            | _ ->
                printfn "Unknown message"
            
            return! loop()
    }
    loop()

let mutable nodeIds = []
let mutable nodes = []
for id in 1..numNodes do
    let mutable nodeId = rand.Next(pown 2 m)
    while List.contains nodeId nodeIds do
        nodeId <- rand.Next(pown 2 m)
    let node = spawn system (string nodeId) <| p2pNode nodeId
    nodes <- nodes @ [node]
    if nodeIds.Length=0 then
        printfn $"NODE: {nodeId} starts initialization"
        node <! {RandomSearchNodeId=(-1); FirstOrNot=true}
        nodeIds <- nodeIds @ [nodeId]
        Thread.Sleep(timeslot)
    else
        printfn $"NODE: {nodeId} starts initialization"
        let rnd = rand.Next(nodeIds.Length)
        let rndNodeIndex = nodeIds.[rnd]
        node <! {RandomSearchNodeId=rndNodeIndex; FirstOrNot=false}
        nodeIds <- nodeIds @ [nodeId]
        Thread.Sleep(timeslot)


for i in 1..m do
    for j in 1..numNodes do
        let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[j-1]))
        node <! 2  
        Thread.Sleep(timeslot)

for i in 1..numNodes do
    for j in 1..numNodes do
        let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[j-1]))
        node <! 1
    Thread.Sleep(timeslot)

for i in 1..m do
    for j in 1..numNodes do
        let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[j-1]))
        node <! 2
        Thread.Sleep(timeslot)

for i in 1..numNodes do
    let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[i-1]))
    node <! 3
Thread.Sleep(timeslot)

for i in 1..numNodes do
    let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[i-1]))
    node <! "dsd"
    Thread.Sleep(timeslot)

printfn $"----------send msg-----------------------------------------------"

for i in 1..numNodes do
    let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[i-1]))
    node <! 4
    Thread.Sleep(timeslot)

printfn $"----------------------------------------------------------"

let fail = system.ActorSelection("akka://project3/user/"+(string nodeIds.[2]))
fail <! 0
let a = fail.PathString
printfn $"node {a} quits from the ring"

printfn "press a key to continue"
Console.ReadKey() |> ignore

for i in 1..numNodes do
    let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[i-1]))
    node <! 4
    Thread.Sleep(500)

printfn "press a key to continue"
Console.ReadKey() |> ignore
printfn $"----------------------------------------------------------"
for i in 1..numNodes do
    let node = system.ActorSelection("akka://project3/user/"+(string nodeIds.[i-1]))
    node <! 4
    Thread.Sleep(timeslot)