#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp
open System.Threading 

let inputs = fsi.CommandLineArgs |> Array.tail
let numNodes = inputs.[0] |> int
let numMessages = inputs.[1] |> int
let system = ActorSystem.Create("project3")
let rand = Random()
let m = 16

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

let ranStr n = 
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in
    String(Array.init n (fun _ -> chars.[r.Next sz]))

let p2pNode (nodeId:int) (mailbox: Actor<_>) = 
    let myId = nodeId
    let mutable curFixFingerIndex = 1
    let mutable myFingerTable = Array.create m [||]
    let mutable predecessorNodeId = -1
    let mutable successorNodeId = nodeId
    let mutable totalHop = 0
    let mutable finisheMessage = 0

    let printInfo (key:int)= 
        printfn $"Node : {myId}"
        printfn $"successor is {successorNodeId}"
        printfn $"predecessor is {predecessorNodeId}"
        printfn "fingerTable"
        for i in 1..m do
            let finger = myFingerTable.[i-1].[0] 
            let fingerNode = myFingerTable.[i-1].[1] 
            printfn $"{finger}          {fingerNode}"
    
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match box message with
        | :? string as requestData->
            let content = System.Text.Encoding.ASCII.GetBytes requestData
            let bytes = Security.Cryptography.HashAlgorithm.Create("SHA1").ComputeHash(content)
            let targetKey = (int bytes.[bytes.Length-1]) + (int bytes.[bytes.Length-2]*(pown 2 8))
            let request = {TargetKey=targetKey; TargetKeyIndex=(-1); RequesterNodeId=myId; Hop=0}
            mailbox.Self<!request
        | :? int as request ->
            if request=1 then 
                let successorNode = system.ActorSelection("akka://project3/user/"+(string successorNodeId))
                let stabilizeSuccessorRequest = {RequesterNodeId=myId}
                successorNode <! stabilizeSuccessorRequest
            else 
                if curFixFingerIndex>m then
                    curFixFingerIndex <- 1
                let fixFingerRequest = {TargetFinger=myFingerTable.[curFixFingerIndex-1].[0]; TargetFingerIndex=curFixFingerIndex; RequesterNodeId=myId}
                mailbox.Self <! fixFingerRequest
                curFixFingerIndex <- curFixFingerIndex+1

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
                totalHop <- totalHop + hop
                finisheMessage <- finisheMessage + 1
                if finisheMessage=numMessages then
                    let calculateNode = system.ActorSelection("akka://project3/user/calculate")
                    calculateNode <! totalHop
                
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

        | _ ->
            printfn "Unkown message"
            

        return! loop()
    }
    loop()

let mutable nodeIds = []
let mutable nodes = []
let startTime = TimeSpan.FromSeconds(0.05)
let delayTimeForStabilize = TimeSpan.FromSeconds(0.01)
let delayTimeForFixfinger = TimeSpan.FromSeconds(0.02)

printfn "----------------------START CREATE RING----------------------"
for id in 1..numNodes do
    let mutable nodeId = rand.Next(pown 2 m)
    while List.contains nodeId nodeIds do
        nodeId <- rand.Next(pown 2 m)
    let node = spawn system (string nodeId) <| p2pNode nodeId
    nodes <- nodes @ [node]
    if nodeIds.Length=0 then
        printfn "nodes initializing..."
        //printfn $"NODE: {nodeId} starts initialization"
        node <! {RandomSearchNodeId=(-1); FirstOrNot=true}
        nodeIds <- nodeIds @ [nodeId]
        system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForStabilize,node,1)
        system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForFixfinger,node,2)
        Thread.Sleep(25)
    else
        //printfn $"NODE: {nodeId} starts initialization"
        let rnd = rand.Next(nodeIds.Length)
        let rndNodeIndex = nodeIds.[rnd]
        node <! {RandomSearchNodeId=rndNodeIndex; FirstOrNot=false}
        nodeIds <- nodeIds @ [nodeId]
        system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForStabilize,node,1)
        system.Scheduler.ScheduleTellRepeatedly(startTime,delayTimeForFixfinger,node,2)
        Thread.Sleep(25)
printfn $"all nodes initialized"
// This actor is used to receive and calculate the average hops
let calculateNode (mailbox: Actor<_>) = 
    let mutable totalHop = 0
    let mutable finisheNode = 0
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match box message with
        | :? int as hop ->
            totalHop <- totalHop+hop
            finisheNode <- finisheNode+1
            if finisheNode=numNodes then
                let averageHop = (float totalHop) / (float (numNodes*numMessages))
                printfn $"The average hop to deliver a message is {averageHop} hops"
                Environment.Exit 1  
        | _ ->
            printfn "Unkown message"
        return! loop()
    }
    loop()
let calculateNode1 = spawn system "calculate" <| calculateNode
// each node starts to request data from other node
// we generate random strings as data
Thread.Sleep(10000)
printfn "start request data"
for message in 1..numMessages do
    for nodeIndex in 1..numNodes do
        let data = ranStr 6
        nodes.[nodeIndex-1] <! data
    Thread.Sleep(1000)



