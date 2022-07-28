using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/*
Name: Ian Taylor
Client Partner: Christian Thibeault
Date: 4/11/2022
Sources:

*/

namespace TCPserver
{
    class Program
    {
        class ship
        {

            Dictionary<KeyValuePair<int, int>, bool> _map;

            public ship()
            {
                _map = new Dictionary<KeyValuePair<int, int>, bool>();
            }

            public void insertLoc(int x, int y)
            {
                _map[new KeyValuePair<int, int>(x, y)] = false;
            }

            public bool checkHit(int x, int y)
            {
                KeyValuePair<int, int> loc = new KeyValuePair<int, int>(x, y);

                if (_map.ContainsKey(loc) == false) return false;

                _map[loc] = true;

                return true;
            }

            public bool isSunk()
            {
                foreach (var item in _map)
                {
                    if (item.Value == false) return false;
                }

                return true;
            }

        }

        class tile
        {
            public tile()
            {
                sh = null; hit = false;
            }

            public ship sh; // reference to ship 
            public bool hit; // true or false
        }

        struct Player
        {
            public IntPtr handle; // TcpClient.Client.Handle, should remain same on server. NOT same on client
            public int number;
            public List<ship> ships;
            public int sunk;
            public tile[,] board;
        }

        public static int x0 = 0;
        private int y0 = 0;
        private int x1 = 0;
        private int y1 = 0;
        private int x2 = 0;
        private int y2 = 0;
        private int x3 = 0;
        private int y3 = 0;
        private int x4 = 0;
        private int y4 = 0;

        private static int status = 0; // 0 = pre-game, 1 = playing, 2 = ply1 win, 3 = ply2 win
        private static ship toRemove = null;

        static void Main(string[] args)
        {
            TcpListener server = null;
            try
            {
                // Set the TcpListener on port 13000.
                Int32 port = 13000;

                string hostName = Dns.GetHostName();
                Console.WriteLine(hostName + " now running BattleSharp WinForms Server.");

                IPAddress localAddr = Dns.GetHostAddresses(hostName)[1]; // Name of PC

                string ipAnswer; // answer to using default IP
                int boardAnswer;
                bool chosen = false;
                Ping testping = new Ping();
                PingReply testreply;

                if (localAddr.AddressFamily == AddressFamily.InterNetwork) // Is it IPv4?
                {
                    Console.WriteLine("Detected local IP is: {0}", localAddr);
                    Console.WriteLine("Use this IP?");
                    Console.WriteLine("Y/N?");
                    while (chosen == false) // Keep asking until answered
                    {
                        ipAnswer = Console.ReadLine().ToLower();

                        if (ipAnswer == "y")
                        {
                            chosen = true;
                        }
                        else if (ipAnswer == "n")
                        {
                            Console.WriteLine("WARNING! Using a public IP requires port forwarding port 13000.");
                            Console.WriteLine("Please enter an IP: ");
                            try
                            {
                                localAddr = IPAddress.Parse(Console.ReadLine()); // Try to parse the answer
                            }
                            catch
                            {
                                localAddr = IPAddress.Parse("11.11.11.11"); // Give a junk IP if it can't even be parsed
                            }
                            testreply = testping.Send(localAddr); // Test the IP is real/connectable by pinging
                            if (testreply.Status != IPStatus.Success) // Failure condition
                            {
                                chosen = true;
                                while (testreply.Status != IPStatus.Success) // Keep asking if they keep giving invalid ip
                                {
                                    Console.WriteLine("IP is invalid.");
                                    Console.WriteLine("Please enter an IP: ");
                                    try
                                    {
                                        localAddr = IPAddress.Parse(Console.ReadLine());
                                    }
                                    catch
                                    {
                                        localAddr = IPAddress.Parse("11.11.11.11");
                                    }
                                    testreply = testping.Send(localAddr);
                                }
                            }
                            else if (testreply.Status == IPStatus.Success) // Yes! A working IP!
                            {
                                chosen = true;
                            }
                        }
                        else // If the first answer is not y or n
                        {
                            Console.WriteLine("Incorrect format. Y/N?");
                        }
                    }
                }
                else // Shouldn't happen, but if found IP is not IPv4...
                {
                    Console.WriteLine("No local network IPv4 detected.");
                    Console.WriteLine("WARNING! Using a public IP requires port forwarding port 13000.");
                    Console.WriteLine("Please enter an IP: ");

                    try
                    {
                        localAddr = IPAddress.Parse(Console.ReadLine());
                    }
                    catch
                    {
                        localAddr = IPAddress.Parse("11.11.11.11");
                    }
                    
                    testreply = testping.Send(localAddr);
                    
                    if (testreply.Status != IPStatus.Success)
                    {
                        while (testreply.Status != IPStatus.Success)
                        {
                            Console.WriteLine("IP is invalid.");
                            Console.WriteLine("Please enter an IP: ");
                            try
                            {
                                localAddr = IPAddress.Parse(Console.ReadLine());
                            }
                            catch
                            {
                                localAddr = IPAddress.Parse("11.11.11.11");
                            }
                            testreply = testping.Send(localAddr);
                        }
                    }
                }

                Console.WriteLine("What board size would you like to use?");
                Console.WriteLine("(HINT: 5 through 9 are valid. Board will be # by # spaces.)");
                try
                {
                    boardAnswer = int.Parse(Console.ReadLine());
                }
                catch
                {
                    boardAnswer = 0;
                }
                while (boardAnswer < 5 || boardAnswer > 9)
                {
                    Console.WriteLine("Incorrect input.");
                    Console.WriteLine("What board size would you like to use?");
                    Console.WriteLine("(HINT: 5 through 9 are valid. Board will be # by # spaces.)");
                    try
                    {
                        boardAnswer = int.Parse(Console.ReadLine());
                    }
                    catch
                    {
                        boardAnswer = 0;
                    }
                }


                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, port);

                // Start listening for client requests.
                server.Start();

                // Enter the listening loop.
                while (true)
                {
                    bool allowed = false;

                    TcpClient ply1 = null;
                    TcpClient ply2 = null;

                    while (!allowed)
                    {
                        Console.Write("Waiting for connections... ");

                        // Perform a blocking call to accept requests.
                        // You could also use server.AcceptSocket() here.
                        ply1 = server.AcceptTcpClient();
                        allowed = CheckConfig(ply1, boardAnswer);
                    }
                    Console.WriteLine("Player 1 connected!");

                    allowed = false;

                    while (!allowed)
                    {
                        Console.Write("Waiting for a second connection...");

                        // Perform a blocking call to accept requests.
                        // You could also use server.AcceptSocket() here.
                        ply2 = server.AcceptTcpClient();
                        allowed = CheckConfig(ply2, boardAnswer);
                    }
                    Console.WriteLine("Player 2 connected!");

                    // GET LOGIN MESSAGE AND CHECK IF CONFIG MATCHES

                    Console.WriteLine("Game starting...");

                    Player player1 = new Player();
                    Player player2 = new Player();

                    player1.board = new tile[boardAnswer, boardAnswer];

                    for (int i = 0; i < boardAnswer; i++)
                    {
                        for (int j = 0; j < boardAnswer; j++)
                        {
                            player1.board[i, j] = new tile();
                        }
                    }

                    player2.board = new tile[boardAnswer, boardAnswer];

                    for (int i = 0; i < boardAnswer; i++)
                    {
                        for (int j = 0; j < boardAnswer; j++)
                        {
                            player2.board[i, j] = new tile();
                        }
                    }

                    player1.handle = ply1.Client.Handle;
                    player1.number = 1;
                    player1.sunk = 0;
                    player2.handle = ply2.Client.Handle;
                    player2.number = 2;
                    player2.sunk = 0;

                    IntPtr turnHandle; // player handle of whose turn
                    int turn = 0; // total turns

                    Console.Write("Rolling turn order...");

                    Random coinflip = new Random(DateTime.Now.Second); // random # for turn order decision
                    if (coinflip.Next() % 2 == 0)
                    {
                        turnHandle = player1.handle;
                        Console.WriteLine("Player 1 goes first.");
                    }
                    else
                    {
                        turnHandle = player2.handle;
                        Console.WriteLine("Player 2 goes first.");
                    }


                    // Get player stream objects for reading and writing
                    NetworkStream stream1 = ply1.GetStream();
                    //Thread p1Stream = new Thread(new ThreadStart(ListenThread1)(stream1));

                    NetworkStream stream2 = ply2.GetStream();

                    // -------------- TURN ORDER / GAME LOGIC BELOW --------------
                    bool chosen1 = false;
                    bool chosen2 = false;

                    while (status == 0) // While choosing ships
                    {
                        player1.ships = new List<ship>();
                        player2.ships = new List<ship>();
                        while (chosen1 == false && chosen2 == false)
                        {
                            if (turnHandle == player1.handle)
                            {
                                if (Turn(ply1, ply2, player1, player2))
                                {
                                    chosen1 = true;
                                    turnHandle = player2.handle;
                                    Console.WriteLine(player1.number + " has placed a ship.");
                                }
                            }
                            else if (turnHandle == player2.handle)
                            {
                                if (Turn(ply2, ply1, player2, player1))
                                {
                                    chosen2 = true;
                                    turnHandle = player1.handle;
                                    Console.WriteLine(player2.number + " has placed a ship.");
                                }
                            }
                        }

                        status = 1;
                    }

                    while (status == 1) // While playing round
                    {
                        if (turnHandle == player1.handle)
                        {
                            if (Turn(ply1, ply2, player1, player2))
                            {
                                turnHandle = player2.handle;
                            }
                        }
                        else if (turnHandle == player2.handle)
                        {
                            if (Turn(ply2, ply1, player2, player1))
                            {
                                turnHandle = player1.handle;
                            }
                        }
                    }

                    // Shutdown and end connection
                    ply1.Close();
                    ply2.Close();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }

            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }

        /// <summary>
        /// Runs a game turn given the client, opposing client, player, and player's opponent who will perform/receive actions.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="clientOpp"></param>
        /// <param name="ply"></param>
        /// <param name="opp"></param>
        /// <returns>Bool: true if turn success, false if turn fail.</returns>
        private static bool Turn(TcpClient client, TcpClient clientOpp, Player ply, Player opp)
        {
            // Buffer for reading data
            byte[] bytes = new byte[256];
            string data = null;
            int i;

            NetworkStream stream = client.GetStream();
            NetworkStream oppstream = clientOpp.GetStream();

            string turnMsg = ("T" + ply.number.ToString());
            stream.Write(Encoding.ASCII.GetBytes(turnMsg));
            //oppstream.Write(Encoding.ASCII.GetBytes(turnMsg));
            Console.WriteLine("Player {0} turn", ply.number);

            /*
            // Loop to receive all the data sent by the client.
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                
            }
            */

            // Translate data bytes to a ASCII string.
            data = System.Text.Encoding.ASCII.GetString(bytes, 0, stream.Read(bytes, 0, bytes.Length));
            Console.WriteLine("Received: {0}", data);

            // Process the data sent by the client.
            data = data.ToUpper();

            byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);
            // Do different things depending on the type of action
            switch (data[0])
            {
                case ('P'): // Place ship
                    
                    for (int p = 1; p < data.Length; p++)
                    {
                        switch (data[p])
                        {
                            case ('A'):
                                ship aircraft = new();
                                aircraft.insertLoc(int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString()));
                                aircraft.insertLoc(int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString()));
                                aircraft.insertLoc(int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString()));
                                aircraft.insertLoc(int.Parse(data[p+7].ToString()), int.Parse(data[p+8].ToString()));
                                aircraft.insertLoc(int.Parse(data[p+9].ToString()), int.Parse(data[p+10].ToString()));
                                ply.board[int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString())].sh = aircraft;
                                ply.board[int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString())].sh = aircraft;
                                ply.board[int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString())].sh = aircraft;
                                ply.board[int.Parse(data[p+7].ToString()), int.Parse(data[p+8].ToString())].sh = aircraft;
                                ply.board[int.Parse(data[p+9].ToString()), int.Parse(data[p+10].ToString())].sh = aircraft;
                                ply.ships.Add(aircraft);
                                Console.WriteLine("Aircraft carrier placed by {0}", ply.number);
                                
                                break;
                            case ('B'):
                                ship battle = new();
                                battle.insertLoc(int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString()));
                                battle.insertLoc(int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString()));
                                battle.insertLoc(int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString()));
                                battle.insertLoc(int.Parse(data[p+7].ToString()), int.Parse(data[p+8].ToString()));
                                ply.board[int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString())].sh = battle;
                                ply.board[int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString())].sh = battle;
                                ply.board[int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString())].sh = battle;
                                ply.board[int.Parse(data[p+7].ToString()), int.Parse(data[p+8].ToString())].sh = battle;
                                ply.ships.Add(battle);
                                Console.WriteLine("Battleship placed by {0}", ply.number);
                                
                                break;
                            case ('S'):
                                ship sub = new();
                                sub.insertLoc(int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString()));
                                sub.insertLoc(int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString()));
                                sub.insertLoc(int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString()));
                                ply.board[int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString())].sh = sub;
                                ply.board[int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString())].sh = sub;
                                ply.board[int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString())].sh = sub;
                                ply.ships.Add(sub);
                                Console.WriteLine("Submarine placed by {0}", ply.number);
                                
                                break;
                            case ('C'):
                                ship cruiser = new();
                                cruiser.insertLoc(int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString()));
                                cruiser.insertLoc(int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString()));
                                cruiser.insertLoc(int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString()));
                                ply.board[int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString())].sh = cruiser;
                                ply.board[int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString())].sh = cruiser;
                                ply.board[int.Parse(data[p+5].ToString()), int.Parse(data[p+6].ToString())].sh = cruiser;
                                ply.ships.Add(cruiser);
                                Console.WriteLine("Cruiser placed by {0}", ply.number);
                                
                                break;
                            case ('D'):
                                ship destroyer = new();
                                destroyer.insertLoc(int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString()));
                                destroyer.insertLoc(int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString()));
                                ply.board[int.Parse(data[p+1].ToString()), int.Parse(data[p+2].ToString())].sh = destroyer;
                                ply.board[int.Parse(data[p+3].ToString()), int.Parse(data[p+4].ToString())].sh = destroyer;
                                ply.ships.Add(destroyer);
                                Console.WriteLine("Destroyer placed by {0}", ply.number);
                                
                                break;
                        }
                    }

                    byte[] res = new byte[1];
                    res[0] = Convert.ToByte(6);

                    // Send back a response.
                    stream.Write(res, 0, res.Length);
                    Console.WriteLine("Sent: {0}", "ACK");
                    return true;

                    break;
                case ('A'): // Attack
                    foreach (ship sh in opp.ships)
                    {
                        // If hit...
                        if (sh.checkHit(int.Parse(data[1].ToString()), int.Parse(data[2].ToString())))
                        {
                            opp.board[int.Parse(data[1].ToString()), int.Parse(data[2].ToString())].hit = true;
                            if (sh.isSunk())
                            {
                                opp.sunk++;
                                // If all sunk...
                                if (opp.sunk == opp.ships.Count)
                                {
                                    // PLAYER WINS
                                    status = ply.number + 1;
                                    string winSend = ("E" + ply.number.ToString() + 'W');
                                    stream.Write(Encoding.ASCII.GetBytes(winSend));
                                    oppstream.Write(Encoding.ASCII.GetBytes(winSend));
                                    Console.WriteLine("Sent: {0}", winSend);
                                    return true;
                                }
                                toRemove = sh;
                                string sunkSend = ("S" + opp.number.ToString() + data[1] + data[2]);
                                stream.Write(Encoding.ASCII.GetBytes(sunkSend));
                                oppstream.Write(Encoding.ASCII.GetBytes(sunkSend));
                                Console.WriteLine("Sent: {0}", sunkSend);
                                break;
                            }
                            else
                            {
                                string hitSend = ("H" + opp.number.ToString() + data[1] + data[2]);
                                stream.Write(Encoding.ASCII.GetBytes(hitSend));
                                oppstream.Write(Encoding.ASCII.GetBytes(hitSend));
                                Console.WriteLine("Sent: {0}", hitSend);
                                return true;
                            }
                        }                       
                    }

                    if (opp.ships.Contains(toRemove) && toRemove != null)
                    {
                        opp.ships.Remove(toRemove);
                        toRemove = null;
                        return true;
                    }

                    // If miss...

                    string missSend = ("M" + opp.number.ToString() + data[1] + data[2]);
                    stream.Write(Encoding.ASCII.GetBytes(missSend));
                    oppstream.Write(Encoding.ASCII.GetBytes(missSend));
                    Console.WriteLine("Sent: {0}", missSend);
                    return true;

                    break;
                case ('F'): // Forfeit
                    status = opp.number + 1;
                    string forSend = ("E" + opp.number.ToString() + 'W');
                    stream.Write(Encoding.ASCII.GetBytes(forSend));
                    oppstream.Write(Encoding.ASCII.GetBytes(forSend));
                    Console.WriteLine("Sent: {0}", forSend);
                    break;
            }

            return false;
        }

        private static bool CheckConfig(TcpClient client, int config)
        {
            // Buffer for reading data
            byte[] bytes = new byte[256];
            string data = null;
            int i;
            int plycon = 0;
            bool success = false;

            NetworkStream stream = client.GetStream();

            // Translate data bytes to a ASCII string.
            data = System.Text.Encoding.ASCII.GetString(bytes, 0, stream.Read(bytes, 0, bytes.Length));
            Console.WriteLine("Received: {0}", data);
            //Console.WriteLine("This should eventually hit 0: {0}", bytes.Length);

            plycon = int.Parse(Convert.ToString(data[1]));

            if (plycon != config)
            {
                byte[] msg = new byte[1];
                msg[0] = Convert.ToByte(21);
                // Send back a response.
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", "NAK");
            }
            else
            {
                byte[] msg = new byte[1];
                msg[0] = Convert.ToByte(6);

                // Send back a response.
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", "ACK");
            }

            //Console.WriteLine("This should eventually hit 0: {0}", bytes.Length);

            /*
            // Loop to receive all the data sent by the client.
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                
            }
            */
            

            if (plycon == config)
            {
                return true;
            }
            else
            {
                try
                {
                    stream.Close();
                    client.Close();
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
