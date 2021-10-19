open System.IO
open System.Security.Cryptography

for arg in fsi.CommandLineArgs |> Seq.skip 1 do
    printf $"Calculating sha256 of {arg}\n  " 
    System.Text.Encoding.ASCII.GetBytes arg |> (new SHA256Managed()).ComputeHash |> Seq.iter (printf "%x")
    printfn ""