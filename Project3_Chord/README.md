# COP5615 Project3 P2P: Chord

#### Team member:

​	Yandi Ma	UFID: 81094691

​	Zhongkai Sun	UFID: 78885317



#### Run Code Instruction

```F#
dotnet fsi .\Chord.fsx <nodeNum> <RequestNum>
```

​	for example

```F#
dotnet fsi .\Chord.fsx 5 10
```

#### What is Working

​	We have two kinds of actors, the actors who is mainly running is P2P Node, they form a ring and send and reply message to each other, there is still  one calculate actor, the only function of it is to receive the message of saying how many hops an P2P Node used to send and reply message and calculate the average number, it never affects the running procedure of P2P Nodes.

#### Running Result and Output

ex.

```F#
> dotnet fsi .\Chord.fsx 100 40 

----------------------START CREATE RING----------------------
nodes initializing...
all nodes initialized
start request data
The average hop to deliver a message is 3.69225 hops
```

​	The average number of hops is 3.67 and 1/2log~2~100=3.32, so we can say they are pretty similar, so the average lookup costs O(logN)

​	Below are the results of some other conditions.

| Num of Nodes | Num of Requests | Average Hops |
| :----------: | :-------------: | :----------: |
|     100      |       40        |   3.69225    |
|     200      |       60        |    4.554     |
|     500      |       100       |   5.11034    |
|     700      |       100       |    5.583     |
|     1000     |       100       |   5.74439    |
|     2500     |       150       |    5.7668    |



#### Largest Network

​	In this project, the largest network our PC can run is 5000 numbers of nodes with 200 numbers of requests, the program quits itself.



#### Bonus

​	To work with nodes failure, we construct a list of successor nodes for each node as described in the paper. The length of the list is not bigger than half of the number of nodes. If a node α find his successor node s don't respond to its request, then α may assume that node s has failed, and will choose the next nearest live node as its successor.

​	In the program, we first let all the nodes initialize and several rounds of stabilization to robust form a ring, and let them start transferring request and response. Then we manually disabled a node, then at some point in time, one node will realize its successor may have failed. Then it will automatically change its successor to the next nearest live node in its successor list.

#### Run Code

```F#
dotnet fsi .\bonus.fsx <nodeNum>
```



#### Running Result

​	Here we generate a ring of 6 nodes (for the intuition of the example).

​	Here in this program we delete the input of <RequestNum> to avoid the program ending to fast before the failure or some node noticing the failure.

```F#
dotnet fsi .\bonus.fsx 6 

<output>
NODE: 3 starts initialization
NODE: 7 starts initialization
NODE: 12 starts initialization
NODE: 14 starts initialization
NODE: 11 starts initialization
NODE: 15 starts initialization

// since nodeNum = 6, then each node has a successor list of length 3

Node : 7
predecessor is 3
successor is 11
[|11; 12; 14|]
fingerTable
8          11
9          11
11          11
15          15

// start sending messages

3 send-> 7		7 reply-> 3
7 send-> 11		11 reply-> 7
12 send-> 14	14 reply-> 12
14 send-> 15	15 reply-> 14
11 send-> 12	12 reply-> 11
15 send-> 3		3 reply-> 15

// node 12 quits from the ring

/user/12 <! quit


3 send-> 7		7 reply-> 3
7 send-> 11		11 reply-> 7
14 send-> 15	15 reply-> 14
11 send-> 12
15 send-> 3		3 reply-> 15

Node 12 may fail
Node 11's successor changed to 14

3 send-> 7		7 reply-> 3
7 send-> 11		11 reply-> 7
14 send-> 15	15 reply-> 14
11 send-> 14	14 reply-> 11 //node 11 will send message to its new successor
15 send-> 3		3 reply-> 15
```

