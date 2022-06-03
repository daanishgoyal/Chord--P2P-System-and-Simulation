#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
open System
open Akka.Actor
open Akka.FSharp
open System.Security.Cryptography
open System.Collections.Generic

let numOfNodes = fsi.CommandLineArgs.[1] |> int
let numOfMessages = fsi.CommandLineArgs.[2] |> int
let hashLen = 160
let hashAlgo = "SHA1"
let mySystem = System.create "myChord" (Configuration.load())


type msgNode =
    | Setup of chordNodes: bigint [] * nodeIndex: int
    | BeginChord
    | Navigate of message: string * hopCount: int

type msgSup = 
    | Start of nodes:int * messages:int
    | SetupDone
    | InboundMessage of numHops: int

let randomStr = 
    let chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWUXYZ0123456789"
    let charsLen = chars.Length
    let random = System.Random()

    fun len -> 
        let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
        new System.String(randomChars)

let searchInNodes (arr:bigint []) key k =
    let mutable nxt = arr.Length
    let mutable prev = 0
    while prev < nxt do
        let cen = prev + (nxt-prev)/2
        if arr.[int <| cen]<key then
            prev <- cen + 1
        else
            nxt <- cen;    
    nxt + k

let hashedStr (input: string, algo: string) =
    let temp1 =
        input 
            |> System.Text.Encoding.UTF8.GetBytes
            |> HashAlgorithm.Create(algo).ComputeHash
    let hashedStr = "0" + (temp1
                                |> Seq.map (fun c -> c.ToString "x2")
                                |> Seq.reduce (+))
    hashedStr

let ChordActor (mailbox : Actor<_>) =
    let myPath = "akka://myChord/user/supervisor/"
    let mutable pred = ""
    let mutable fingerTable = Array2D.init hashLen 2 (fun _ _ -> "")
    let myChordAct = mailbox.Context.Self.Path.Name
    let predClosestNode id =
        let mutable index = hashLen-1
        if myChordAct < id then
            while (fingerTable.[index, 1] >= id || fingerTable.[index, 1] <= myChordAct) do
                index <- (index - 1)
        else
            while fingerTable.[index, 1] >= id && fingerTable.[index, 1] <= myChordAct do
                index <- (index - 1)
        fingerTable.[index, 1]
        
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with

        | BeginChord ->
            for i in 1 .. numOfMessages do
                let randomString10 = randomStr(10) 
                let hashedMsg = hashedStr(randomString10, hashAlgo)  
                mailbox.Self <! Navigate(hashedMsg, -1)


        | Setup(chordNodes, nodeIndex) ->
            let sizeChord =  bigint (2.0**160.0)
            let currNode = chordNodes.[nodeIndex]
            let totalNodes = chordNodes.Length
            pred <- chordNodes.[(totalNodes + nodeIndex - 1) % totalNodes].ToString("x2")
            if pred.Length < 41 then
                pred <- (String.replicate (41-pred.Length) "0") + pred
            
            for i in 1 .. 160 do
                let mutable nextBig = bigint 0
                let k = 0
                let temp2 = currNode + bigint (2.0 ** float (i-1))
                let myfingInt = temp2 % sizeChord
                let mutable myfing = myfingInt.ToString("x2")
                if myfingInt > currNode then
                    nextBig <- chordNodes.[searchInNodes chordNodes.[(nodeIndex+1)%totalNodes..] myfingInt (nodeIndex+1)%totalNodes]
                else
                    nextBig <- chordNodes.[searchInNodes chordNodes.[..nodeIndex] myfingInt 0]
                if myfing.Length < 41 then
                    myfing <- (String.replicate (41-myfing.Length) "0") + myfing
                let mutable fingerValue = nextBig.ToString("x2")
                if fingerValue.Length < 41 then
                    fingerValue <- (String.replicate (41-fingerValue.Length) "0") + fingerValue
                fingerTable.[i-1, 0] <- myfing
                fingerTable.[i-1, 1] <- fingerValue
               
            mailbox.Sender() <! SetupDone
      
        | Navigate(hashedMsg, hopCount) ->
            let numOfHops = hopCount + 1
            if (myChordAct < pred && (hashedMsg > pred || hashedMsg <= myChordAct))  || (hashedMsg > pred && hashedMsg <= myChordAct) then
                mailbox.Context.Parent <! InboundMessage(numOfHops)
            else
                let mutable path1 = ""
                let succedN = fingerTable.[0, 1]
                if (hashedMsg > myChordAct && hashedMsg <= succedN) 
                    || (succedN< myChordAct && (hashedMsg> myChordAct || hashedMsg<= succedN)) then
                    path1 <- myPath+succedN
                else
                    let nextnode = predClosestNode(hashedMsg)
                    path1 <- myPath+nextnode 
                    
                let actorRef = select path1 mySystem 
                actorRef <! Navigate(hashedMsg, numOfHops) 
                
        return! loop()
    }
    loop()

let Supervisor (mailbox : Actor<_>) =
    let mutable myChordList = new List<IActorRef>()
    let mutable avgHops = 0
    let totalReq = numOfNodes * numOfMessages
    printfn "total number of requests are: %i" totalReq
    let mutable nodeCount = 0
    let mutable setupCount = 0
    let mutable countm = 0
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | Start(nodes, messages) -> let pf = "randomxyzab"
                                    nodeCount <- nodes
                                    let valNode = [for i in 1 .. nodes do yield hashedStr(pf+string(i), hashAlgo)] |> List.sort
                                    let nodeinInt = [|for i in valNode -> bigint.Parse(i, System.Globalization.NumberStyles.HexNumber)|]
                                    valNode |> List.iter (fun tonode -> myChordList.Add (spawn mailbox.Context tonode ChordActor))
                                    myChordList |> Seq.iteri (fun i myNodeChord -> 
                                         myNodeChord <! Setup(nodeinInt, i))
                                    

        | SetupDone ->  setupCount <- setupCount + 1
                        if setupCount = nodeCount then
                            printfn "ChordRing Formed"
                            myChordList |> Seq.iteri (fun i myNodeChord -> 
                                    myNodeChord <! BeginChord)
                            
        | InboundMessage(hops) ->
            avgHops <- avgHops + hops
            countm <- countm + 1
            printfn "Request: %i Converged in hops: %i " countm hops
            if countm = totalReq then
                avgHops <- avgHops / totalReq
                printfn "Avg hops for %i nodes with  %i requests per node: %i" numOfNodes numOfMessages avgHops
                mailbox.Context.System.Terminate() |> ignore             

        return! loop()
    }
    loop()

let supervisor = spawn mySystem "supervisor" Supervisor
supervisor <! Start(numOfNodes, numOfMessages)
mySystem.WhenTerminated.Wait()