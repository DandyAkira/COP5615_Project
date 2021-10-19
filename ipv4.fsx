open System
open System.Net
open System.Net.NetworkInformation
open System.Net.Sockets

let localIP () =
    let networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                            |> Array.filter(fun iface -> iface.OperationalStatus.Equals(OperationalStatus.Up))

    let addresses = seq {
        for iface in networkInterfaces do
            for unicastAddr in iface.GetIPProperties().UnicastAddresses do
                yield unicastAddr.Address}

    let ipv4 = 
        addresses
        |> Seq.filter(fun addr -> addr.AddressFamily.Equals(AddressFamily.InterNetwork))
        |> Seq.filter(IPAddress.IsLoopback >> not)
        |> Seq.last    
    ipv4.ToString()



printfn $"{localIP()}"