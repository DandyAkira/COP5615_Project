open System

let key = Random()


for i in 1..100 do
    Console.WriteLine(key.Next(1, 11))