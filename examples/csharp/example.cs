
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets; // For SocketType, etc
using System.Text; // For Encoding

using ZeroTier; // For ZeroTier.Node, ZeroTier.Event, and ZeroTier.Socket

public class ExampleApp {

	ZeroTier.Node node;

	/**
	 * Initialize and start ZeroTier
	 */
	public void StartZeroTier(string configFilePath, ushort servicePort, ulong networkId)
	{
		node = new ZeroTier.Node(configFilePath, OnZeroTierEvent, servicePort);
		node.Start(); // Network activity only begins after calling Start()

		/* How you do this next part is up to you, but essentially we're waiting for the node
		to signal to us (via a ZeroTier.Event) that it has access to the internet and is
		able to talk to one of our root servers. As a convenience you can just periodically check
		IsOnline() instead of looking for the event via the callback. */
		while (!node.IsOnline()) { Thread.Sleep(100); }

		/* After the node comes online you may now join/leave networks. You will receive
		notifications via the callback function regarding the status of your join request as well
		as any subsequent network-related events such as the assignment of an IP address, added
		or removed routes, etc. */
		node.Join(networkId);

		/* Note that ZeroTier.Socket calls will fail if there are no routes available, for this
		reason we should wait to make those calls until the node has indicated to us that at
		least one network has been joined successfully. */
		while (!node.HasRoutes()) { Thread.Sleep(100); }
	}

	/**
	 * Stop ZeroTier
	 */
	public void StopZeroTier()
	{
		node.Stop();
	}

	/**
	 * Your application should process event messages and return control as soon as possible. Blocking
	 * or otherwise time-consuming operations are not reccomended here.
	 */
	public void OnZeroTierEvent(ZeroTier.Event e)
	{
		Console.WriteLine("Event.eventCode = {0} ({1})", e.EventCode, e.EventName);

		if (e.EventCode == ZeroTier.Constants.EVENT_NODE_ONLINE) {
			Console.WriteLine("Node is online");
			Console.WriteLine(" - Address (NodeId): " + node.NodeId.ToString("x16"));
		}

		if (e.EventCode == ZeroTier.Constants.EVENT_NETWORK_OK) {
			Console.WriteLine(" - Network ID: " + e.networkDetails.networkId.ToString("x16"));
		}
	}

	/**
	 * Example server
	 */
	public void YourServer(IPEndPoint localEndPoint) {
		string data = null;

		// Data buffer for incoming data.
		byte[] bytes = new Byte[1024];

		Console.WriteLine(localEndPoint.ToString());
		ZeroTier.Socket listener = new ZeroTier.Socket(AddressFamily.InterNetwork,
			SocketType.Stream, ProtocolType.Tcp );

		// Bind the socket to the local endpoint and
		// listen for incoming connections.

		try {
			listener.Bind(localEndPoint);
			listener.Listen(10);

			// Start listening for connections.
			while (true) {
				Console.WriteLine("Waiting for a connection...");
				// Program is suspended while waiting for an incoming connection.
				Console.WriteLine("accepting...");
				ZeroTier.Socket handler = listener.Accept();
				data = null;

				Console.WriteLine("accepted connection from: " + handler.RemoteEndPoint.ToString());

				// An incoming connection needs to be processed.
				while (true) {
					int bytesRec = handler.Receive(bytes);
					Console.WriteLine("Bytes received: {0}", bytesRec);
					data += Encoding.ASCII.GetString(bytes,0,bytesRec);

					if (bytesRec > 0) {
						Console.WriteLine( "Text received : {0}", data);
						break;
					}
				}
				// Echo the data back to the client.
				byte[] msg = Encoding.ASCII.GetBytes(data);

				handler.Send(msg);
				handler.Shutdown(SocketShutdown.Both);
				handler.Close();
			}

		} catch (ZeroTier.ZeroTierException e) {
			Console.WriteLine(e);
			Console.WriteLine("ServiveErrorCode={0} SocketErrorCode={1}", e.ServiceErrorCode, e.SocketErrorCode);
		}

		Console.WriteLine("\nPress ENTER to continue...");
		Console.Read();
	}

	/**
	 * Example client
	 */
	public void YourClient(IPEndPoint remoteServerEndPoint) {
		// Data buffer for incoming data.
		byte[] bytes = new byte[1024];

		// Connect to a remote device.
		try {
			// Create a TCP/IP  socket.
			ZeroTier.Socket sender = new ZeroTier.Socket(AddressFamily.InterNetwork,
				SocketType.Stream, ProtocolType.Tcp );

			// Connect the socket to the remote endpoint. Catch any errors.
			try {

				Console.WriteLine("Socket connecting to {0}...",
					remoteServerEndPoint.ToString());

				sender.Connect(remoteServerEndPoint);

				Console.WriteLine("Socket connected to {0}",
					sender.RemoteEndPoint.ToString());

				// Encode the data string into a byte array.
				byte[] msg = Encoding.ASCII.GetBytes("This is a test");

				// Send the data through the socket.
				int bytesSent = sender.Send(msg);

				// Receive the response from the remote device.
				int bytesRec = sender.Receive(bytes);
				Console.WriteLine("Echoed test = {0}",
					Encoding.ASCII.GetString(bytes,0,bytesRec));

				// Release the socket.
				sender.Shutdown(SocketShutdown.Both);
				sender.Close();

			} catch (ArgumentNullException ane) {
				Console.WriteLine("ArgumentNullException : {0}",ane.ToString());
			} catch (SocketException se) {
				Console.WriteLine("SocketException : {0}",se.ToString());
			} catch (ZeroTier.ZeroTierException e) {
				Console.WriteLine(e);
				Console.WriteLine("ServiveErrorCode={0} SocketErrorCode={1}", e.ServiceErrorCode, e.SocketErrorCode);
			}
		} catch (Exception e) {
			Console.WriteLine( e.ToString());
		}
	}
}

public class example
{
	static int Main(string[] args)
	{
		if (args.Length < 5 || args.Length > 6)
		{
			Console.WriteLine("\nPlease specify either client or server mode and required arguments:");
			Console.WriteLine(" Usage: example server <config_path> <ztServicePort> <nwid> <serverPort>");
			Console.WriteLine(" Usage: example client <config_path> <ztServicePort> <nwid> <remoteServerIp> <remoteServerPort>\n");
			return 1;
		}
		string configFilePath = args[1];
		ushort servicePort = (ushort)Int16.Parse(args[2]);
		ulong networkId = (ulong)Int64.Parse(args[3], System.Globalization.NumberStyles.HexNumber);

		ExampleApp exampleApp = new ExampleApp();

		if (args[0].Equals("server"))
		{
			Console.WriteLine("Server mode...");
			ushort serverPort = (ushort)Int16.Parse(args[4]);
			exampleApp.StartZeroTier(configFilePath, servicePort, networkId);
			IPAddress ipAddress = IPAddress.Parse("0.0.0.0");
			IPEndPoint localEndPoint = new IPEndPoint(ipAddress, serverPort);
			exampleApp.YourServer(localEndPoint);
		}

		if (args[0].Equals("client"))
		{
			Console.WriteLine("Client mode...");
			string serverIP = args[4];
			int port = Int16.Parse(args[5]);
			IPAddress ipAddress = IPAddress.Parse(serverIP);
			IPEndPoint remoteEndPoint = new IPEndPoint(ipAddress, port);
			exampleApp.StartZeroTier(configFilePath, servicePort, networkId);
			exampleApp.YourClient(remoteEndPoint);
		}
		exampleApp.StopZeroTier();
		return 0;
	}
}

