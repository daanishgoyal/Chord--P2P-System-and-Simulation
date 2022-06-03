# Project 3: Chord-P2P System and Simulation


Daanish Goyal
UFID: 1767-3302
daanishgoyal@ufl.edu

## Overview
In this project, simulation of chord protocol using FSharp Akka.net framework is done. The algorithm is mentioned in this paper - https://pdos.csail.mit.edu/papers/ton:chord/paper-ton.pdf

## Chord Protocol
Chord is a protocol and an algorithm for peer-to-peer distributed hash table. A distributed hash table is used to store the key-value pairs, where each key represents a node or data item in the system. A node stores values of all the data items with the keys for which it is responsible for. Chord protocol specifies how the nodes are positioned by assigning specific keys to them, and how a node can locate a value for a given key by first locating the node responsible for that key.

### To run:

    `dotnet fsi Program.fsx numNodes numReq`
    
    
    
Implemented the network join and routing as described in the Chord paper using Actor Modeling (AKKA Framework) and it prints the average number of hops that have to be traversed to deliver a message.


##### Project was working for 1000 nodes with 5 requests each, So total requests generated= 50000.
