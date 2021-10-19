open System.Diagnostics

let sw = Stopwatch()
sw.Start()
while(int sw.ElapsedMilliseconds < 500) do
    printfn $"{sw.ElapsedMilliseconds}"

sw.Restart()
while(true) do

    printfn $"{sw.ElapsedMilliseconds}"
